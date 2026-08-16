// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Mrp.PlayerState;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTv.Mrp.Tests.PlayerStateTests;

/// <summary>
/// Unit tests for <see cref="MrpPushUpdater"/>, ported from pyatv's <c>MrpPushUpdater</c>
/// active/start/stop/error semantics.
/// </summary>
// pyatv/protocols/mrp/__init__.py (MrpPushUpdater) — line 698-743 as of pyatv 0.18.0
[TestClass]
public class MrpPushUpdaterTests
	{
	private sealed class StubListener : IMrpPushUpdaterListener
		{
		public List<MrpPlayerState> Updates
			{
			get;
			} = [];

		public List<Exception> Errors
			{
			get;
			} = [];

		public void PlaystatusUpdate (MrpPlayerState playing) => Updates.Add (playing);

		public void PlaystatusError (MrpPushUpdater updater, Exception exception) => Errors.Add (exception);
		}

	[TestMethod]
	public void NotActiveInitially ()
		{
		var psm = new MrpPlayerStateManager ();
		var updater = new MrpPushUpdater (psm);

		Assert.IsFalse (updater.Active);
		}

	[TestMethod]
	public void StartWithoutListenerThrowsNoAsyncListenerException ()
		{
		var psm = new MrpPlayerStateManager ();
		var updater = new MrpPushUpdater (psm);

		_ = Assert.ThrowsExactly<MrpNoAsyncListenerException> (updater.Start);
		}

	[TestMethod]
	public void StartRegistersAsPlayerStateManagerListenerAndDeliversCurrentState ()
		{
		var psm = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		var updater = new MrpPushUpdater (psm) { Listener = listener };

		updater.Start ();

		Assert.IsTrue (updater.Active);
		Assert.AreSame (updater, psm.Listener);
		Assert.AreEqual (1, listener.Updates.Count);
		Assert.AreEqual (0, listener.Errors.Count);
		}

	[TestMethod]
	public void StartIsIdempotentWhenAlreadyActive ()
		{
		var psm = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		var updater = new MrpPushUpdater (psm) { Listener = listener };

		updater.Start ();
		updater.Start ();

		// One delivery for the initial start; the second Start() call is a no-op because Active is
		// already true, mirroring pyatv's "if self.active: return".
		Assert.AreEqual (1, listener.Updates.Count);
		}

	[TestMethod]
	public void StopClearsPlayerStateManagerListener ()
		{
		var psm = new MrpPlayerStateManager ();
		var listener = new StubListener ();
		var updater = new MrpPushUpdater (psm) { Listener = listener };
		updater.Start ();

		updater.Stop ();

		Assert.IsFalse (updater.Active);
		Assert.IsNull (psm.Listener);
		}

	[TestMethod]
	public void StopWhenNotActiveDoesNotClearAnotherListener ()
		{
		var psm = new MrpPlayerStateManager ();
		var updater = new MrpPushUpdater (psm);

		// Simulate some other listener owning the PSM.
		var otherUpdater = new MrpPushUpdater (psm) { Listener = new StubListener () };
		otherUpdater.Start ();

		updater.Stop ();

		Assert.AreSame (otherUpdater, psm.Listener);
		}

	[TestMethod]
	public void StateUpdatedForwardsPlayingStateToListener ()
		{
		var psm = new MrpPlayerStateManager ();

		// Establish an active client/player so psm.Playing returns a stable reference rather than a
		// fresh default MrpPlayerState on every access.
		var client = new AppleTvControlLibrary.Mrp.Protobuf.NowPlayingClient { BundleIdentifier = "client_id" };
		var setClientEnvelope = new AppleTvControlLibrary.Mrp.Protobuf.ProtocolMessage
			{
			Type = AppleTvControlLibrary.Mrp.Protobuf.ProtocolMessage.Types.Type.SetNowPlayingClientMessage,
			};
		setClientEnvelope.SetExtension (
			AppleTvControlLibrary.Mrp.Protobuf.SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage,
			new AppleTvControlLibrary.Mrp.Protobuf.SetNowPlayingClientMessage { Client = client });
		psm.MessageReceived (setClientEnvelope);

		var listener = new StubListener ();
		var updater = new MrpPushUpdater (psm) { Listener = listener };

		updater.StateUpdated ();

		Assert.AreEqual (1, listener.Updates.Count);
		Assert.AreEqual ("client_id", listener.Updates[0].Parent?.BundleIdentifier);
		}

	[TestMethod]
	public void StateUpdatedRoutesListenerExceptionToPlaystatusErrorInsteadOfThrowing ()
		{
		var psm = new MrpPlayerStateManager ();
		var updater = new MrpPushUpdater (psm);
		var thrown = new InvalidOperationException ("boom");
		Exception? captured = null;
		MrpPushUpdater? capturedUpdater = null;

		updater.Listener = new ThrowingListener (thrown, (u, ex) =>
			{
			capturedUpdater = u;
			captured = ex;
			});

		// Must not throw: a listener failure while producing an update is a valid operational
		// outcome, not a bug in MrpPushUpdater itself.
		updater.StateUpdated ();

		Assert.AreSame (updater, capturedUpdater);
		Assert.AreSame (thrown, captured);
		}

	private sealed class ThrowingListener (Exception toThrow, Action<MrpPushUpdater, Exception> onError) : IMrpPushUpdaterListener
		{
		public void PlaystatusUpdate (MrpPlayerState playing) => throw toThrow;

		public void PlaystatusError (MrpPushUpdater updater, Exception exception) => onError (updater, exception);
		}
	}
