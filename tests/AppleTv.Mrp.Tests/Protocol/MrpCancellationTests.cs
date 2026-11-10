// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Connection;
using AppleTvControlLibrary.Mrp.Protocol;

using NUnit.Framework;

namespace AppleTv.Mrp.Tests.ProtocolTests;

[TestFixture]
public sealed class MrpCancellationTests
	{
	[Test]
	public async Task PreCancelledOperationsNeverSendAndCancellationIsNotReportedAsTimeout ()
		{
		using var protocol = new MrpProtocol (new FakeConnection (), new SrpAuthHandler (), new MrpInfoSettings ());
		protocol.AsyncSender = bytes => { protocol.MessageReceived (bytes); return Task.CompletedTask; };
		await protocol.StartAsync (skipInitialMessages: true);
		int sends = 0;
		using var cancelled = new CancellationTokenSource ();
		cancelled.Cancel ();
		protocol.AsyncSender = _ => { Interlocked.Increment (ref sends); return Task.CompletedTask; };
		await Assert.CatchAsync<OperationCanceledException> (async () => await protocol.SendAsync (new (), cancelled.Token));
		await Assert.CatchAsync<OperationCanceledException> (async () => await protocol.SendAndReceiveAsync (new (), cancellationToken: cancelled.Token));
		Assert.That (sends, Is.Zero);
		using var waiting = new CancellationTokenSource ();
		Task receive = protocol.SendAndReceiveAsync (new (), cancellationToken: waiting.Token);
		waiting.Cancel ();
		await Assert.CatchAsync<OperationCanceledException> (async () => await receive);
		Assert.That (sends, Is.EqualTo (1));
		}
	private sealed class FakeConnection : IMrpFrameConnection
		{
		public IMrpConnectionListener? Listener
			{
			get; set;
			}
		public byte[] BuildMessage (byte[] data) => data;
		public void EnableEncryption (byte[] outputKey, byte[] inputKey) => throw new InvalidOperationException ("The fixture never pairs or enables encryption.");
		}
	}