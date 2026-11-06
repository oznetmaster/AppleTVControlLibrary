// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Http;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.FakeDevice;
using AppleTvControlLibrary.Tlv8;

using NUnit.Framework;

namespace AppleTv.Mrp.Tests.AirPlay;

[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public sealed class AirPlayHandshakeTests
	{
	[TestCase (false)]
	[TestCase (true)]
	public async Task AbandonedRequestClosesConnectionAndReconnectCanReadItsOwnResponseAsync (bool cancel)
		{
		using var loopback = new LoopbackPeer ();
		using HttpConnection connection = await loopback.ConnectAsync ();
		using var cancellation = new CancellationTokenSource ();
		Task<HttpResponse> first = connection.SendAndReceiveAsync ("GET", "/first",
			timeout: TimeSpan.FromSeconds (cancel ? 8 : 2), cancellationToken: cancellation.Token);
		Assert.That ((await loopback.ReadRequestAsync ()).Path, Is.EqualTo ("/first"));
		Task<HttpResponse> second = connection.SendAndReceiveAsync ("GET", "/second", timeout: TimeSpan.FromSeconds (8));
		Assert.That ((await loopback.ReadRequestAsync ()).Path, Is.EqualTo ("/second"));

		if (cancel)
			{
			cancellation.Cancel ();
			await Assert.ThatAsync (async () => await first, Throws.InstanceOf<OperationCanceledException> ());
			}
		else
			await Assert.ThrowsAsync<TimeoutException> (async () => await first);

		// The peer never sent either reply. A late first reply can no longer satisfy
		// the second waiter, and no further request can go down the old socket.
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await second);
		await Assert.ThrowsAsync<InvalidOperationException> (async () => await connection.GetAsync ("/unusable"));

		using HttpConnection retry = await loopback.ConnectAsync ();
		Task<HttpResponse> third = retry.GetAsync ("/reconnect");
		Assert.That ((await loopback.ReadRequestAsync ()).Path, Is.EqualTo ("/reconnect"));
		await loopback.ReplyAsync (Encoding.ASCII.GetBytes ("own response"));
		Assert.That (Encoding.ASCII.GetString ((await third).Body), Is.EqualTo ("own response"));
		}

	[TestCase (false)]
	[TestCase (true)]
	public async Task PairVerifyOnlyEnablesEncryptionAfterValidFinalReplyAsync (bool reject)
		{
		var fake = new FakeMrpDevice ();
		var setup = new MrpPairSetupProcedure (request => Task.FromResult (fake.HandleMessage (request)), new SrpAuthHandler ());
		await setup.StartPairingAsync ();
		HapCredentials credentials = await setup.FinishPairingAsync (FakeMrpDevice.PIN_CODE);
		using var loopback = new LoopbackPeer ();
		using HttpConnection connection = await loopback.ConnectAsync ();
		Task<AirPlayHapPairVerifyProcedure> verify = AirPlayConnectionAuth.VerifyConnectionAsync (credentials, connection);
		await loopback.ReplyAsync (Tlv8.WriteTlv (await ExchangeWithFakeAsync (loopback, fake, "/pair-verify")));
		var final = await ExchangeWithFakeAsync (loopback, fake, "/pair-verify");
		if (reject)
			final[(int)TlvValue.Error] = [2];
		await loopback.ReplyAsync (Tlv8.WriteTlv (final));

		if (reject)
			{
			await Assert.ThrowsAsync<AuthenticationException> (async () => await verify);
			Assert.That (connection.SendProcessor, Is.Null);
			Assert.That (connection.ReceiveProcessor, Is.Null);
			}
		else
			{
			await verify;
			Assert.That (connection.SendProcessor, Is.Not.Null);
			Assert.That (connection.ReceiveProcessor, Is.Not.Null);
			}
		}

	[TestCase (false)]
	[TestCase (true)]
	public async Task PairSetupRequiresValidServerProofAsync (bool reject)
		{
		var fake = new FakeMrpDevice ();
		using var loopback = new LoopbackPeer ();
		using HttpConnection connection = await loopback.ConnectAsync ();
		var setup = new AirPlayHapPairSetupProcedure (connection, new SrpAuthHandler ());
		Task start = setup.StartPairingAsync ();
		Assert.That ((await loopback.ReadRequestAsync ()).Path, Is.EqualTo ("/pair-pin-start"));
		await loopback.ReplyAsync ([]);
		await loopback.ReplyAsync (Tlv8.WriteTlv (await ExchangeWithFakeAsync (loopback, fake, "/pair-setup")));
		await start;

		Task<HapCredentials> finish = setup.FinishPairingAsync (FakeMrpDevice.PIN_CODE);
		var m4 = await ExchangeWithFakeAsync (loopback, fake, "/pair-setup");
		if (reject)
			m4[(int)TlvValue.Proof][0] ^= 1;
		await loopback.ReplyAsync (Tlv8.WriteTlv (m4));
		if (reject)
			{
			await Assert.ThrowsAsync<AuthenticationException> (async () => await finish);
			Assert.That (fake.HasPaired, Is.False);
			}
		else
			{
			await loopback.ReplyAsync (Tlv8.WriteTlv (await ExchangeWithFakeAsync (loopback, fake, "/pair-setup")));
			Assert.That ((await finish).Type, Is.EqualTo (AuthenticationType.Hap));
			Assert.That (fake.HasPaired, Is.True);
			}
		}

	private static async Task<Dictionary<int, byte[]>> ExchangeWithFakeAsync (LoopbackPeer peer, FakeMrpDevice fake, string path)
		{
		HttpRequest request = await peer.ReadRequestAsync ();
		Assert.That (request.Path, Is.EqualTo (path));
		return MrpMessages.GetPairingData (fake.HandleMessage (MrpMessages.CryptoPairing (Tlv8.ReadTlv (request.Body))));
		}

	private sealed class LoopbackPeer : IDisposable
		{
		private readonly TcpListener _listener = new (IPAddress.Loopback, 0);
		private TcpClient? _peer;
		private byte[] _pending = [];

		public LoopbackPeer () => _listener.Start ();

		public async Task<HttpConnection> ConnectAsync ()
			{
			_peer?.Dispose ();
			_pending = [];
			using var deadline = new CancellationTokenSource (TimeSpan.FromSeconds (5));
			var accepted = _listener.AcceptTcpClientAsync ();
			var connection = await HttpConnection.ConnectAsync ("127.0.0.1", ((IPEndPoint)_listener.LocalEndpoint).Port, deadline.Token);
			_peer = await accepted;
			return connection;
			}

		public async Task<HttpRequest> ReadRequestAsync ()
			{
			byte[] chunk = new byte[2048];
			Task deadline = Task.Delay (TimeSpan.FromSeconds (5));
			HttpRequest? request;
			byte[] rest;
			while (!HttpMessages.TryParseRequest (_pending, out request, out rest))
				{
				Task<int> read = _peer!.GetStream ().ReadAsync (chunk, 0, chunk.Length);
				if (await Task.WhenAny (read, deadline) != read)
					throw new TimeoutException ("Loopback server did not receive a complete request.");
				int count = await read;
				if (count == 0)
					throw new EndOfStreamException ();
				var joined = new byte[_pending.Length + count];
				_pending.CopyTo (joined, 0);
				Array.Copy (chunk, 0, joined, _pending.Length, count);
				_pending = joined;
				}
			_pending = rest;
			return request!;
			}

		public Task ReplyAsync (byte[] body)
			{
			byte[] response = HttpMessages.FormatResponse (new HttpResponse ("HTTP", "1.1", 200, "OK", new Dictionary<string, string> (), body));
			return _peer!.GetStream ().WriteAsync (response, 0, response.Length);
			}

		public void Dispose ()
			{
			_peer?.Dispose ();
			_listener.Stop ();
			}
		}
	}
