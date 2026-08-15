// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

namespace AppleTvControlLibrary.Mrp.PlayerState;

/// <summary>
/// Listener interface for <see cref="MrpPushUpdater"/>, mirroring pyatv's
/// <c>PushListener</c> (playstatus_update/playstatus_error) callback pair.
/// </summary>
// pyatv/interface.py (PushListener) — referenced from protocols/mrp/__init__.py (MrpPushUpdater) as of pyatv 0.18.0
public interface IMrpPushUpdaterListener
	{
	/// <summary>Called with the state of the currently active player whenever it changes.</summary>
	/// <param name="playing">The current player state.</param>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.state_updated -> listener.playstatus_update) — line 734-738 as of pyatv 0.18.0
	void PlaystatusUpdate (MrpPlayerState playing);

	/// <summary>Called when producing an update failed for a reason other than cancellation.</summary>
	/// <param name="updater">The updater that failed to produce an update.</param>
	/// <param name="exception">The exception describing the failure.</param>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.state_updated -> listener.playstatus_error) — line 741-743 as of pyatv 0.18.0
	void PlaystatusError (MrpPushUpdater updater, Exception exception);
	}

/// <summary>
/// Raised by <see cref="MrpPushUpdater.Start"/> when no <see cref="MrpPushUpdater.Listener"/> has
/// been set, mirroring pyatv's <c>NoAsyncListenerError</c>.
/// </summary>
// pyatv/exceptions.py (NoAsyncListenerError) as of pyatv 0.18.0
public class MrpNoAsyncListenerException : Exception
	{
	/// <summary>Initializes a new instance of the <see cref="MrpNoAsyncListenerException"/> class.</summary>
	public MrpNoAsyncListenerException () : base ("No listener set for push updates")
		{
		}

	/// <summary>Initializes a new instance of the <see cref="MrpNoAsyncListenerException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	public MrpNoAsyncListenerException (string message) : base (message)
		{
		}

	/// <summary>Initializes a new instance of the <see cref="MrpNoAsyncListenerException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MrpNoAsyncListenerException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}

/// <summary>
/// Forwards <see cref="MrpPlayerStateManager"/> updates for the active client/player to a
/// registered <see cref="IMrpPushUpdaterListener"/>, mirroring pyatv's <c>MrpPushUpdater</c>.
/// </summary>
/// <remarks>
/// Unlike pyatv, which installs itself as <c>PlayerStateManager.listener</c> and receives a
/// no-argument callback (reading <c>self.psm.playing</c> itself), this type both installs itself
/// as <see cref="IMrpPlayerStateListener"/> and does the "read playing" step, since that is exactly
/// what pyatv's <c>state_updated()</c> does synchronously before dispatch.
/// </remarks>
/// <remarks>Initializes a new instance of the <see cref="MrpPushUpdater"/> class.</remarks>
/// <param name="psm">The player state manager to source updates from.</param>
// pyatv/protocols/mrp/__init__.py (MrpPushUpdater) — line 698-743 as of pyatv 0.18.0
public sealed class MrpPushUpdater (MrpPlayerStateManager psm) : IMrpPlayerStateListener
	{

	/// <summary>Gets or sets the listener notified of push updates and errors.</summary>
	public IMrpPushUpdaterListener? Listener
		{
		get;
		set;
		}

	/// <summary>Gets a value indicating whether this instance is currently registered to receive push updates.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.active) — line 712-715 as of pyatv 0.18.0
	public bool Active => ReferenceEquals (psm.Listener, this);

	/// <summary>
	/// Starts forwarding push updates from the device to <see cref="Listener"/>, immediately
	/// delivering the current state once.
	/// </summary>
	/// <exception cref="MrpNoAsyncListenerException">No <see cref="Listener"/> has been set.</exception>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.start) — line 717-728 as of pyatv 0.18.0
	public void Start ()
		{
		if (Listener is null)
			{
			throw new MrpNoAsyncListenerException ();
			}

		if (Active)
			{
			return;
			}

		psm.Listener = this;

		// pyatv/protocols/mrp/__init__.py — line 728 as of pyatv 0.18.0: "asyncio.ensure_future(self.state_updated())".
		// Deliver the current state once immediately upon starting, matching the fire-and-forget
		// initial push. Any failure here is a valid operational outcome (e.g. no player state has
		// arrived yet) and is routed to the listener rather than thrown from Start().
		StateUpdated ();
		}

	/// <summary>Stops forwarding push updates to <see cref="Listener"/>.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.stop) — line 730-732 as of pyatv 0.18.0
	public void Stop ()
		{
		if (Active)
			{
			psm.Listener = null;
			}
		}

	/// <inheritdoc/>
	// pyatv/protocols/mrp/__init__.py (MrpPushUpdater.state_updated) — line 734-743 as of pyatv 0.18.0
	public void StateUpdated ()
		{
		try
			{
			Listener?.PlaystatusUpdate (psm.Playing);
			}
		catch (Exception ex)
			{
			// pyatv/protocols/mrp/__init__.py — line 741-743 as of pyatv 0.18.0: any exception other
			// than cancellation while building/posting an update is a valid (if unwelcome) outcome —
			// e.g. transient inconsistent state during a client/player transition — and is reported to
			// the listener rather than left to escape as an unhandled exception.
			Listener?.PlaystatusError (this, ex);
			}
		}
	}
