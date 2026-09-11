// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protobuf;

using NUnit.Framework;

namespace AppleTv.Mrp.Tests.PlayerStateTests;

/// <summary>
/// Unit tests for <see cref="MrpPlayerStateManager"/>, <see cref="MrpClient"/>, and
/// <see cref="MrpPlayerState"/>, ported from pyatv's client/player management tests.
/// </summary>
// pyatv/protocols/mrp/tests/test_player_state.py (as tests/protocols/mrp/test_player_state.py) — as of pyatv 0.18.0
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class MrpPlayerStateManagerTests
	{
	private const string ClientId1 = "client_id_1";
	private const string ClientName1 = "client_name_1";
	private const string ClientId2 = "client_id_2";
	private const string PlayerId1 = "player_id_1";
	private const string PlayerName1 = "player_name_1";
	private const string DefaultPlayer = MrpPlayerStateManager.DefaultPlayerId;

	private sealed class StubListener : IMrpPlayerStateListener
		{
		public int CallCount
			{
			get;
			private set;
			}

		public void StateUpdated () => CallCount++;
		}

	private static ProtocolMessage SetPath (
		ProtocolMessage message,
		string clientId = ClientId1,
		string? clientName = ClientName1,
		string playerId = PlayerId1,
		string? playerName = PlayerName1)
		{
		var playerPath = new PlayerPath
			{
			Client = new NowPlayingClient { BundleIdentifier = clientId },
			Player = new NowPlayingPlayer { Identifier = playerId },
			};

		if (!string.IsNullOrEmpty (clientName))
			{
			playerPath.Client.DisplayName = clientName;
			}

		if (!string.IsNullOrEmpty (playerName))
			{
			playerPath.Player.DisplayName = playerName;
			}

		switch (message.Type)
			{
			case ProtocolMessage.Types.Type.SetStateMessage:
				message.SetExtension (SetStateMessageExtensions.SetStateMessage, new SetStateMessage { PlayerPath = playerPath });
				break;
			case ProtocolMessage.Types.Type.UpdateContentItemMessage:
				message.SetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage, new UpdateContentItemMessage { PlayerPath = playerPath });
				break;
			case ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage:
				message.SetExtension (SetNowPlayingPlayerMessageExtensions.SetNowPlayingPlayerMessage, new SetNowPlayingPlayerMessage { PlayerPath = playerPath });
				break;
			case ProtocolMessage.Types.Type.RemovePlayerMessage:
				message.SetExtension (RemovePlayerMessageExtensions.RemovePlayerMessage, new RemovePlayerMessage { PlayerPath = playerPath });
				break;
			case ProtocolMessage.Types.Type.SetDefaultSupportedCommandsMessage:
				message.SetExtension (SetDefaultSupportedCommandsMessageExtensions.SetDefaultSupportedCommandsMessage, new SetDefaultSupportedCommandsMessage { PlayerPath = playerPath });
				break;
			default:
				throw new System.NotSupportedException (message.Type.ToString ());
			}

		return message;
		}

	private static ProtocolMessage AddMetadataItem (ProtocolMessage message, int location = 0, string? identifier = null, string? title = null, float? playbackRate = null, int? playCount = null)
		{
		SetStateMessage inner = message.GetExtension (SetStateMessageExtensions.SetStateMessage);
		if (inner.PlaybackQueue is null)
			{
			inner.PlaybackQueue = new PlaybackQueue ();
			}

		inner.PlaybackQueue.Location = location;

		var item = new ContentItem
			{
			Metadata = new ContentItemMetadata (),
			};

		if (identifier is not null)
			{
			item.Identifier = identifier;
			}

		if (title is not null)
			{
			item.Metadata.Title = title;
			}

		if (playbackRate is not null)
			{
			item.Metadata.PlaybackRate = playbackRate.Value;
			}

		if (playCount is not null)
			{
			item.Metadata.PlayCount = playCount.Value;
			}

		inner.PlaybackQueue.ContentItems.Add (item);
		return message;
		}

	private static (MrpPlayerStateManager Manager, StubListener Listener) CreateManager ()
		{
		var manager = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		manager.Listener = listener;
		return (manager, listener);
		}

	[Test]
	public void GetClientAndPlayerReturnsPathIdentity ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.Identifier, Is.EqualTo (PlayerId1));
		Assert.That (player.DisplayName, Is.EqualTo (PlayerName1));

		MrpClient client = psm.GetClient (inner.PlayerPath.Client);
		Assert.That (client.BundleIdentifier, Is.EqualTo (ClientId1));
		Assert.That (client.DisplayName, Is.EqualTo (ClientName1));
		}

	[Test]
	public void NoMetadataReturnsNull ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.Metadata, Is.Null);
		}

	[Test]
	public void MetadataSingleItem ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.Metadata?.Title, Is.EqualTo ("item"));
		}

	[Test]
	public void MetadataMultipleItemsUsesLocation ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item1");
		msg = AddMetadataItem (msg, location: 1, title: "item2");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.Metadata?.Title, Is.EqualTo ("item2"));
		}

	[Test]
	public void MetadataNoItemIdentifierIsNull ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.ItemIdentifier, Is.Null);
		}

	[Test]
	public void MetadataItemIdentifierUpdatesWithLocation ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, identifier: "id1", title: "item1");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.ItemIdentifier, Is.EqualTo ("id1"));

		msg = AddMetadataItem (msg, location: 1, identifier: "id2", title: "item2");
		psm.MessageReceived (msg);

		player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.That (player.ItemIdentifier, Is.EqualTo ("id2"));
		}

	[Test]
	public void GetMetadataFieldReadsScalarFields ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item", playCount: 123);
		psm.MessageReceived (msg);

		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.MetadataField<string> ("title"), Is.EqualTo ("item"));
		Assert.That (player.MetadataField<int?> ("playCount"), Is.EqualTo (123));
		}

	[Test]
	public void ContentItemUpdateMergesMetadata ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, identifier: "id", title: "item", playCount: 123);
		psm.MessageReceived (msg);

		ProtocolMessage update = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.UpdateContentItemMessage));
		UpdateContentItemMessage updateInner = update.GetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage);
		var item = new ContentItem
			{
			Identifier = "id",
			Metadata = new ContentItemMetadata
				{
				Title = "new title",
				PlayCount = 1111,
				},
			};
		updateInner.ContentItems.Add (item);
		psm.MessageReceived (update);

		MrpPlayerState player = psm.GetPlayer (updateInner.PlayerPath);
		Assert.That (player.MetadataField<string> ("title"), Is.EqualTo ("new title"));
		Assert.That (player.MetadataField<int?> ("playCount"), Is.EqualTo (1111));
		}

	[Test]
	public void GetCommandInfoLooksUpByCommand ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.SupportedCommands = new SupportedCommands ();
		inner.SupportedCommands.SupportedCommands_.Add (new CommandInfo { Command = Command.Pause });
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.CommandInfoFor (Command.Play), Is.Null);
		Assert.That (player.CommandInfoFor (Command.Pause), Is.Not.Null);
		}

	[Test]
	public void PlaybackStateWithoutRatePassesThrough ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Paused;
		msg = AddMetadataItem (msg);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.PlaybackStateValue, Is.EqualTo (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Paused));

		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		psm.MessageReceived (msg);

		player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.PlaybackStateValue, Is.EqualTo (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing));
		}

	[Test]
	public void PlaybackStatePlayingWithFullRate ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 1.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.PlaybackStateValue, Is.EqualTo (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing));
		}

	[Test]
	public void PlaybackStateSeekingWithDoubleRate ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 2.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.PlaybackStateValue, Is.EqualTo (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Seeking));
		}

	[Test]
	public void PlaybackStatePlayingWithZeroRateIsStillPlaying ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 0.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.That (player.PlaybackStateValue, Is.EqualTo (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing));
		}

	[Test]
	public void ChangeListenerCanBeClearedAndReassigned ()
		{
		var manager = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		manager.Listener = listener;
		Assert.That (manager.Listener, Is.EqualTo (listener));

		manager.Listener = null;
		Assert.That (manager.Listener, Is.Null);
		}

	[Test]
	public void SetNowPlayingClientNotifiesListener ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		msg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (msg);

		Assert.That (listener.CallCount, Is.EqualTo (1));
		Assert.That (psm.Client?.BundleIdentifier, Is.EqualTo (ClientId1));
		}

	[Test]
	public void SetNowPlayingPlayerWithNoClientDoesNotNotify ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (msg);

		Assert.That (listener.CallCount, Is.EqualTo (0));
		Assert.That (psm.Playing.IsValid, Is.False);
		Assert.That (string.IsNullOrEmpty (psm.Playing.DisplayName), Is.True);
		}

	[Test]
	public void SetNowPlayingPlayerForActiveClientNotifiesAndUpdatesActivePlayer ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (msg);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Playing.Identifier, Is.EqualTo (PlayerId1));
		Assert.That (psm.Playing.DisplayName, Is.EqualTo (PlayerName1));
		}

	[Test]
	public void DefaultPlayerUsedWhenOnlyClientSet ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage defaultMsg = SetPath (
			MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage),
			playerId: DefaultPlayer,
			playerName: "Default Name");
		psm.MessageReceived (defaultMsg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (psm.Playing.Identifier, Is.EqualTo (DefaultPlayer));
		Assert.That (psm.Playing.DisplayName, Is.EqualTo ("Default Name"));
		}

	[Test]
	public void SetStateCallsActiveListenerRepeatedly ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (setState);

		Assert.That (listener.CallCount, Is.EqualTo (1));

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (2));

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		Assert.That (listener.CallCount, Is.EqualTo (3));

		psm.MessageReceived (setState);

		Assert.That (listener.CallCount, Is.EqualTo (4));
		}

	[Test]
	public void ContentItemUpdateCallsActiveListenerRepeatedly ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		Assert.That (listener.CallCount, Is.EqualTo (1));

		ProtocolMessage updateItem = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.UpdateContentItemMessage));
		UpdateContentItemMessage updateItemInner = updateItem.GetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage);
		updateItemInner.ContentItems.Add (new ContentItem ());
		updateItem.SetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage, updateItemInner);
		psm.MessageReceived (updateItem);

		Assert.That (listener.CallCount, Is.EqualTo (2));

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (3));

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		Assert.That (listener.CallCount, Is.EqualTo (4));

		psm.MessageReceived (updateItem);

		Assert.That (listener.CallCount, Is.EqualTo (5));
		}

	[Test]
	public void UpdateClientChangesDisplayName ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (1));
		Assert.That (psm.Client?.DisplayName, Is.Null);

		ProtocolMessage update = MrpMessages.Create (ProtocolMessage.Types.Type.UpdateClientMessage);
		update.SetExtension (UpdateClientMessageExtensions.UpdateClientMessage, new UpdateClientMessage
			{
			Client = new NowPlayingClient
				{
				BundleIdentifier = ClientId1,
				DisplayName = ClientName1,
				},
			});
		psm.MessageReceived (update);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Client?.DisplayName, Is.EqualTo (ClientName1));
		}

	[Test]
	public void RemoveActiveClientClearsActiveClient ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Client?.BundleIdentifier, Is.EqualTo (ClientId1));

		ProtocolMessage remove = MrpMessages.Create (ProtocolMessage.Types.Type.RemoveClientMessage);
		remove.SetExtension (RemoveClientMessageExtensions.RemoveClientMessage, new RemoveClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (remove);

		Assert.That (listener.CallCount, Is.EqualTo (3));
		Assert.That (psm.Client, Is.Null);
		}

	[Test]
	public void RemoveNotActiveClientDoesNothing ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Client?.BundleIdentifier, Is.EqualTo (ClientId1));

		ProtocolMessage remove = MrpMessages.Create (ProtocolMessage.Types.Type.RemoveClientMessage);
		remove.SetExtension (RemoveClientMessageExtensions.RemoveClientMessage, new RemoveClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId2 } });
		psm.MessageReceived (remove);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Client?.BundleIdentifier, Is.EqualTo (ClientId1));
		}

	[Test]
	public void RemoveActivePlayerInvalidatesPlaying ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		Assert.That (psm.Playing.Identifier, Is.EqualTo (PlayerId1));

		ProtocolMessage remove = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.RemovePlayerMessage));
		psm.MessageReceived (remove);

		Assert.That (listener.CallCount, Is.EqualTo (4));
		Assert.That (psm.Playing.IsValid, Is.False);
		}

	[Test]
	public void RemoveActivePlayerRevertsToDefault ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage), playerId: DefaultPlayer);
		psm.MessageReceived (msg);

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.That (listener.CallCount, Is.EqualTo (2));
		Assert.That (psm.Playing.Identifier, Is.EqualTo (PlayerId1));

		ProtocolMessage remove = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.RemovePlayerMessage));
		psm.MessageReceived (remove);

		Assert.That (listener.CallCount, Is.EqualTo (3));
		Assert.That (psm.Playing.Identifier, Is.EqualTo (DefaultPlayer));
		}

	[Test]
	public void SetDefaultSupportedCommandsAppliesToPlayer ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = MrpMessages.Create (ProtocolMessage.Types.Type.SetDefaultSupportedCommandsMessage);
		var inner = new SetDefaultSupportedCommandsMessage
			{
			SupportedCommands = new SupportedCommands (),
			PlayerPath = new PlayerPath { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } },
			};
		inner.SupportedCommands.SupportedCommands_.Add (new CommandInfo { Command = Command.Play });
		msg.SetExtension (SetDefaultSupportedCommandsMessageExtensions.SetDefaultSupportedCommandsMessage, inner);
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		var playerPath = new PlayerPath
			{
			Client = new NowPlayingClient { BundleIdentifier = ClientId1 },
			Player = new NowPlayingPlayer { Identifier = PlayerId1 },
			};
		MrpPlayerState player = psm.GetPlayer (playerPath);

		Assert.That (player.CommandInfoFor (Command.Play), Is.Not.Null);
		Assert.That (listener.CallCount, Is.EqualTo (2));
		}
	}