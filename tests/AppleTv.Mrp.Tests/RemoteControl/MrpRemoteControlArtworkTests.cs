// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.Connection;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Protocol;
using AppleTvControlLibrary.Mrp.RemoteControl;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTv.Mrp.Tests.RemoteControlTests;

/// <summary>
/// Unit tests for <see cref="MrpRemoteControl.GetArtworkAsync"/>'s remote-vs-local artwork
/// selection and caching, ported from pyatv's <c>MrpMetadata.artwork()</c> behavior.
/// </summary>
// pyatv/protocols/mrp/__init__.py (MrpMetadata.artwork / _fetch_remote_artwork / _fetch_local_artwork) — line 504-598 as of pyatv 0.18.0
[TestClass]
public class MrpRemoteControlArtworkTests
	{
	private const string CLIENT_ID = "client_id";
	private const string PLAYER_ID = "player_id";

	private sealed class PassthroughConnection : IMrpFrameConnection
		{
		public IMrpConnectionListener? Listener
			{
			get;
			set;
			}

		public void EnableEncryption (byte[] outputKey, byte[] inputKey)
			{
			}

		public byte[] BuildMessage (byte[] data) => data;
		}

	private sealed class StubHttpMessageHandler (Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
		{
		public int RequestCount
			{
			get;
			private set;
			}

		protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
			{
			RequestCount++;
			return Task.FromResult (respond (request));
			}
		}

	private static MrpPlayerStateManager CreatePlayerStateManagerWithMetadata (Action<ContentItemMetadata> configureMetadata)
		{
		var psm = new MrpPlayerStateManager ();

		var client = new NowPlayingClient { BundleIdentifier = CLIENT_ID };
		var player = new NowPlayingPlayer { Identifier = PLAYER_ID };
		var playerPath = new PlayerPath { Client = client, Player = player };

		var metadata = new ContentItemMetadata ();
		configureMetadata (metadata);

		var setState = new SetStateMessage
			{
			PlayerPath = playerPath,
			PlaybackQueue = new PlaybackQueue { Location = 0 },
			};
		setState.PlaybackQueue.ContentItems.Add (new ContentItem { Identifier = "item-1", Metadata = metadata });

		var setStateEnvelope = new ProtocolMessage { Type = ProtocolMessage.Types.Type.SetStateMessage };
		setStateEnvelope.SetExtension (SetStateMessageExtensions.SetStateMessage, setState);
		psm.MessageReceived (setStateEnvelope);

		// SetStateMessage alone only updates the named client's player; psm.Playing (and therefore
		// MrpRemoteControl, which reads it) only reflects state that is also marked as the current
		// now-playing client, mirroring pyatv's separate "now playing" notifications.
		var nowPlayingClientEnvelope = new ProtocolMessage { Type = ProtocolMessage.Types.Type.SetNowPlayingClientMessage };
		nowPlayingClientEnvelope.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = client });
		psm.MessageReceived (nowPlayingClientEnvelope);

		var nowPlayingPlayerEnvelope = new ProtocolMessage { Type = ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage };
		nowPlayingPlayerEnvelope.SetExtension (SetNowPlayingPlayerMessageExtensions.SetNowPlayingPlayerMessage, new SetNowPlayingPlayerMessage { PlayerPath = playerPath });
		psm.MessageReceived (nowPlayingPlayerEnvelope);

		return psm;
		}

	private static MrpRemoteControl CreateRemoteControl (MrpPlayerStateManager psm, HttpMessageHandler handler)
		{
		MrpProtocol protocol = new (new PassthroughConnection (), new SrpAuthHandler (), new MrpInfoSettings ());
		return new MrpRemoteControl (protocol, psm, new HttpClient (handler));
		}

	[TestMethod]
	public async Task GetArtworkAsyncReturnsNullWhenNoArtworkMetadataPresentAsync ()
		{
		MrpPlayerStateManager psm = CreatePlayerStateManagerWithMetadata (metadata => { });
		var handler = new StubHttpMessageHandler (_ => throw new InvalidOperationException ("Should not be called"));
		MrpRemoteControl remoteControl = CreateRemoteControl (psm, handler);

		(byte[] Data, string? MimeType)? result = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);

		Assert.IsNull (result);
		Assert.AreEqual (0, handler.RequestCount);
		}

	[TestMethod]
	public async Task GetArtworkAsyncFetchesRemoteArtworkFromArtworkURLAsync ()
		{
		byte[] expectedBytes = [1, 2, 3, 4];
		MrpPlayerStateManager psm = CreatePlayerStateManagerWithMetadata (metadata =>
			{
			metadata.ArtworkAvailable = true;
			metadata.ArtworkURL = "https://example.com/art.png";
			});

		var handler = new StubHttpMessageHandler (request =>
			{
			Assert.AreEqual ("https://example.com/art.png", request.RequestUri!.ToString ());
			var response = new HttpResponseMessage (HttpStatusCode.OK)
				{
				Content = new ByteArrayContent (expectedBytes),
				};
			response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue ("image/png");
			return response;
			});

		MrpRemoteControl remoteControl = CreateRemoteControl (psm, handler);

		(byte[] Data, string? MimeType)? result = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);

		Assert.IsNotNull (result);
		CollectionAssert.AreEqual (expectedBytes, result!.Value.Data);
		Assert.AreEqual ("image/png", result.Value.MimeType);
		Assert.AreEqual (1, handler.RequestCount);
		}

	[TestMethod]
	public async Task GetArtworkAsyncCachesResultAndDoesNotRefetchAsync ()
		{
		byte[] expectedBytes = [9, 9, 9];
		MrpPlayerStateManager psm = CreatePlayerStateManagerWithMetadata (metadata =>
			{
			metadata.ArtworkAvailable = true;
			metadata.ArtworkURL = "https://example.com/art.png";
			metadata.ContentIdentifier = "content-1";
			});

		var handler = new StubHttpMessageHandler (_ => new HttpResponseMessage (HttpStatusCode.OK)
			{
			Content = new ByteArrayContent (expectedBytes),
			});
		MrpRemoteControl remoteControl = CreateRemoteControl (psm, handler);

		(byte[] Data, string? MimeType)? first = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);
		(byte[] Data, string? MimeType)? second = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);

		Assert.IsNotNull (first);
		Assert.IsNotNull (second);
		Assert.AreEqual (1, handler.RequestCount);
		}

	[TestMethod]
	public async Task GetArtworkAsyncFallsBackToLocalArtworkWhenRemoteFetchFailsAsync ()
		{
		// No artworkIdentifier/artworkURL at all, but artworkAvailable is true: the only path left
		// is the in-band MRP fetch (GetLocalArtworkAsync), which will time out against a protocol
		// with no real transport — this must surface as null, not an unhandled exception.
		MrpPlayerStateManager psm = CreatePlayerStateManagerWithMetadata (metadata =>
			{
			metadata.ArtworkAvailable = true;
			});

		MrpProtocol protocol = new (new PassthroughConnection (), new SrpAuthHandler (), new MrpInfoSettings ())
			{
			ResponseTimeout = TimeSpan.FromMilliseconds (50),
			};
		var handler = new StubHttpMessageHandler (_ => throw new InvalidOperationException ("Should not be called"));
		var remoteControl = new MrpRemoteControl (protocol, psm, new HttpClient (handler));

		(byte[] Data, string? MimeType)? result = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);

		Assert.IsNull (result);
		}

	[TestMethod]
	public async Task GetArtworkAsyncReturnsNullWhenHttpRequestFailsAsync ()
		{
		MrpPlayerStateManager psm = CreatePlayerStateManagerWithMetadata (metadata =>
			{
			metadata.ArtworkAvailable = true;
			metadata.ArtworkURL = "https://example.com/art.png";
			});

		var handler = new StubHttpMessageHandler (_ => throw new HttpRequestException ("network down"));
		MrpProtocol protocol = new (new PassthroughConnection (), new SrpAuthHandler (), new MrpInfoSettings ())
			{
			ResponseTimeout = TimeSpan.FromMilliseconds (50),
			};
		var remoteControl = new MrpRemoteControl (protocol, psm, new HttpClient (handler));

		(byte[] Data, string? MimeType)? result = await remoteControl.GetArtworkAsync ().ConfigureAwait (false);

		Assert.IsNull (result);
		}
	}
