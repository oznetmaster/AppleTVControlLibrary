// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protobuf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTv.Mrp.Tests.PlayerStateTests;

/// <summary>
/// Unit tests for <see cref="MrpPlayerStateManager"/>, <see cref="MrpClient"/>, and
/// <see cref="MrpPlayerState"/>, ported from pyatv's client/player management tests.
/// </summary>
// pyatv/protocols/mrp/tests/test_player_state.py (as tests/protocols/mrp/test_player_state.py) — as of pyatv 0.18.0
[TestClass]
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

	[TestMethod]
	public void GetClientAndPlayerReturnsPathIdentity ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (PlayerId1, player.Identifier);
		Assert.AreEqual (PlayerName1, player.DisplayName);

		MrpClient client = psm.GetClient (inner.PlayerPath.Client);
		Assert.AreEqual (ClientId1, client.BundleIdentifier);
		Assert.AreEqual (ClientName1, client.DisplayName);
		}

	[TestMethod]
	public void NoMetadataReturnsNull ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.IsNull (player.Metadata);
		}

	[TestMethod]
	public void MetadataSingleItem ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.AreEqual ("item", player.Metadata?.Title);
		}

	[TestMethod]
	public void MetadataMultipleItemsUsesLocation ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item1");
		msg = AddMetadataItem (msg, location: 1, title: "item2");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.AreEqual ("item2", player.Metadata?.Title);
		}

	[TestMethod]
	public void MetadataNoItemIdentifierIsNull ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.IsNull (player.ItemIdentifier);
		}

	[TestMethod]
	public void MetadataItemIdentifierUpdatesWithLocation ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, identifier: "id1", title: "item1");
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.AreEqual ("id1", player.ItemIdentifier);

		msg = AddMetadataItem (msg, location: 1, identifier: "id2", title: "item2");
		psm.MessageReceived (msg);

		player = psm.GetPlayer (msg.GetExtension (SetStateMessageExtensions.SetStateMessage).PlayerPath);
		Assert.AreEqual ("id2", player.ItemIdentifier);
		}

	[TestMethod]
	public void GetMetadataFieldReadsScalarFields ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		msg = AddMetadataItem (msg, title: "item", playCount: 123);
		psm.MessageReceived (msg);

		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual ("item", player.MetadataField<string> ("title"));
		Assert.AreEqual (123, player.MetadataField<int?> ("playCount"));
		}

	[TestMethod]
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
		Assert.AreEqual ("new title", player.MetadataField<string> ("title"));
		Assert.AreEqual (1111, player.MetadataField<int?> ("playCount"));
		}

	[TestMethod]
	public void GetCommandInfoLooksUpByCommand ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.SupportedCommands = new SupportedCommands ();
		inner.SupportedCommands.SupportedCommands_.Add (new CommandInfo { Command = Command.Pause });
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.IsNull (player.CommandInfoFor (Command.Play));
		Assert.IsNotNull (player.CommandInfoFor (Command.Pause));
		}

	[TestMethod]
	public void PlaybackStateWithoutRatePassesThrough ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = msg.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Paused;
		msg = AddMetadataItem (msg);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Paused, player.PlaybackStateValue);

		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		psm.MessageReceived (msg);

		player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing, player.PlaybackStateValue);
		}

	[TestMethod]
	public void PlaybackStatePlayingWithFullRate ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 1.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing, player.PlaybackStateValue);
		}

	[TestMethod]
	public void PlaybackStateSeekingWithDoubleRate ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 2.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Seeking, player.PlaybackStateValue);
		}

	[TestMethod]
	public void PlaybackStatePlayingWithZeroRateIsStillPlaying ()
		{
		(MrpPlayerStateManager psm, _) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		SetStateMessage inner = setState.GetExtension (SetStateMessageExtensions.SetStateMessage);
		inner.PlaybackState = AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing;
		ProtocolMessage msg = AddMetadataItem (setState, playbackRate: 0.0f);
		psm.MessageReceived (msg);

		MrpPlayerState player = psm.GetPlayer (inner.PlayerPath);
		Assert.AreEqual (AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing, player.PlaybackStateValue);
		}

	[TestMethod]
	public void ChangeListenerCanBeClearedAndReassigned ()
		{
		var manager = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		manager.Listener = listener;
		Assert.AreEqual (listener, manager.Listener);

		manager.Listener = null;
		Assert.IsNull (manager.Listener);
		}

	[TestMethod]
	public void SetNowPlayingClientNotifiesListener ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		msg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (msg);

		Assert.AreEqual (1, listener.CallCount);
		Assert.AreEqual (ClientId1, psm.Client?.BundleIdentifier);
		}

	[TestMethod]
	public void SetNowPlayingPlayerWithNoClientDoesNotNotify ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (msg);

		Assert.AreEqual (0, listener.CallCount);
		Assert.IsFalse (psm.Playing.IsValid);
		Assert.IsTrue (string.IsNullOrEmpty (psm.Playing.DisplayName));
		}

	[TestMethod]
	public void SetNowPlayingPlayerForActiveClientNotifiesAndUpdatesActivePlayer ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (msg);

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (PlayerId1, psm.Playing.Identifier);
		Assert.AreEqual (PlayerName1, psm.Playing.DisplayName);
		}

	[TestMethod]
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

		Assert.AreEqual (DefaultPlayer, psm.Playing.Identifier);
		Assert.AreEqual ("Default Name", psm.Playing.DisplayName);
		}

	[TestMethod]
	public void SetStateCallsActiveListenerRepeatedly ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage setState = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (setState);

		Assert.AreEqual (1, listener.CallCount);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.AreEqual (2, listener.CallCount);

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		Assert.AreEqual (3, listener.CallCount);

		psm.MessageReceived (setState);

		Assert.AreEqual (4, listener.CallCount);
		}

	[TestMethod]
	public void ContentItemUpdateCallsActiveListenerRepeatedly ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		Assert.AreEqual (1, listener.CallCount);

		ProtocolMessage updateItem = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.UpdateContentItemMessage));
		UpdateContentItemMessage updateItemInner = updateItem.GetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage);
		updateItemInner.ContentItems.Add (new ContentItem ());
		updateItem.SetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage, updateItemInner);
		psm.MessageReceived (updateItem);

		Assert.AreEqual (2, listener.CallCount);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.AreEqual (3, listener.CallCount);

		ProtocolMessage nowPlaying = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage));
		psm.MessageReceived (nowPlaying);

		Assert.AreEqual (4, listener.CallCount);

		psm.MessageReceived (updateItem);

		Assert.AreEqual (5, listener.CallCount);
		}

	[TestMethod]
	public void UpdateClientChangesDisplayName ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.AreEqual (1, listener.CallCount);
		Assert.IsNull (psm.Client?.DisplayName);

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

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (ClientName1, psm.Client?.DisplayName);
		}

	[TestMethod]
	public void RemoveActiveClientClearsActiveClient ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (ClientId1, psm.Client?.BundleIdentifier);

		ProtocolMessage remove = MrpMessages.Create (ProtocolMessage.Types.Type.RemoveClientMessage);
		remove.SetExtension (RemoveClientMessageExtensions.RemoveClientMessage, new RemoveClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (remove);

		Assert.AreEqual (3, listener.CallCount);
		Assert.IsNull (psm.Client);
		}

	[TestMethod]
	public void RemoveNotActiveClientDoesNothing ()
		{
		(MrpPlayerStateManager psm, StubListener listener) = CreateManager ();
		ProtocolMessage msg = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage));
		psm.MessageReceived (msg);

		ProtocolMessage clientMsg = MrpMessages.Create (ProtocolMessage.Types.Type.SetNowPlayingClientMessage);
		clientMsg.SetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage, new SetNowPlayingClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId1 } });
		psm.MessageReceived (clientMsg);

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (ClientId1, psm.Client?.BundleIdentifier);

		ProtocolMessage remove = MrpMessages.Create (ProtocolMessage.Types.Type.RemoveClientMessage);
		remove.SetExtension (RemoveClientMessageExtensions.RemoveClientMessage, new RemoveClientMessage { Client = new NowPlayingClient { BundleIdentifier = ClientId2 } });
		psm.MessageReceived (remove);

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (ClientId1, psm.Client?.BundleIdentifier);
		}

	[TestMethod]
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

		Assert.AreEqual (PlayerId1, psm.Playing.Identifier);

		ProtocolMessage remove = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.RemovePlayerMessage));
		psm.MessageReceived (remove);

		Assert.AreEqual (4, listener.CallCount);
		Assert.IsFalse (psm.Playing.IsValid);
		}

	[TestMethod]
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

		Assert.AreEqual (2, listener.CallCount);
		Assert.AreEqual (PlayerId1, psm.Playing.Identifier);

		ProtocolMessage remove = SetPath (MrpMessages.Create (ProtocolMessage.Types.Type.RemovePlayerMessage));
		psm.MessageReceived (remove);

		Assert.AreEqual (3, listener.CallCount);
		Assert.AreEqual (DefaultPlayer, psm.Playing.Identifier);
		}

	[TestMethod]
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

		Assert.IsNotNull (player.CommandInfoFor (Command.Play));
		Assert.AreEqual (2, listener.CallCount);
		}
	}
