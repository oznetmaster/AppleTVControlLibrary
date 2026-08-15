// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Google.Protobuf;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.Connection;
using AppleTvControlLibrary.Mrp.Protobuf;
using System.Collections.Generic;

namespace AppleTvControlLibrary.Mrp.Protocol;

/// <summary>
/// Device power state, mirroring pyatv's <c>PowerState</c> constant.
/// </summary>
// pyatv/const.py (PowerState) — line 101-111 as of pyatv 0.18.0
public enum MrpPowerState
	{
	/// <summary>Power state is not determinable. pyatv/const.py — line 104-105 as of pyatv 0.18.0</summary>
	Unknown = 0,
	/// <summary>Device is turned off (standby). pyatv/const.py — line 107-108 as of pyatv 0.18.0</summary>
	Off = 1,
	/// <summary>Device is turned on. pyatv/const.py — line 110-111 as of pyatv 0.18.0</summary>
	On = 2,
	}

/// <summary>
/// Internal protocol state, mirroring pyatv's <c>ProtocolState</c>.
/// </summary>
// pyatv/protocols/mrp/protocol.py (ProtocolState) — line 40-56 as of pyatv 0.18.0
public enum MrpProtocolState
	{
	/// <summary>Not connected. pyatv/protocols/mrp/protocol.py — line 43-44 as of pyatv 0.18.0</summary>
	NotConnected = 0,
	/// <summary>Connecting. pyatv/protocols/mrp/protocol.py — line 46-47 as of pyatv 0.18.0</summary>
	Connecting = 1,
	/// <summary>Connected but not yet ready. pyatv/protocols/mrp/protocol.py — line 49-50 as of pyatv 0.18.0</summary>
	Connected = 2,
	/// <summary>Ready to send/receive commands. pyatv/protocols/mrp/protocol.py — line 52-53 as of pyatv 0.18.0</summary>
	Ready = 3,
	/// <summary>Stopped. pyatv/protocols/mrp/protocol.py — line 55-56 as of pyatv 0.18.0</summary>
	Stopped = 4,
	}

/// <summary>
/// Listener interface for unsolicited MRP messages, i.e. any message that does not correlate
/// to an outstanding <see cref="MrpProtocol.SendAndReceiveAsync"/> call.
/// </summary>
// pyatv/core/protocol.py (MessageDispatcher.listen_to) — line 88-95 as of pyatv 0.18.0
public interface IMrpProtocolListener
	{
	/// <summary>An unsolicited message was received from the device.</summary>
	/// <param name="message">The decoded message.</param>
	// pyatv/protocols/mrp/protocol.py (dispatch) — line 150, 295 as of pyatv 0.18.0
	void MessageReceived (ProtocolMessage message);
	}

/// <summary>
/// Raised when a Companion-style protocol exchange fails. Reused across MRP for parity with the
/// Companion library's exception surface.
/// </summary>
// pyatv/exceptions.py (ProtocolError) — line 31-33 as of pyatv 0.18.0
public class MrpProtocolException : Exception
	{
	/// <summary>Initializes a new instance of the <see cref="MrpProtocolException"/> class.</summary>
	public MrpProtocolException ()
		{
		}

	/// <summary>Initializes a new instance of the <see cref="MrpProtocolException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	public MrpProtocolException (string message) : base (message)
		{
		}

	/// <summary>Initializes a new instance of the <see cref="MrpProtocolException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	public MrpProtocolException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}

/// <summary>
/// Settings needed to build the initial DEVICE_INFORMATION message. A minimal analogue of
/// pyatv's <c>InfoSettings</c> (pydantic model), exposing only the fields <see cref="MrpProtocol"/>
/// itself needs.
/// </summary>
// pyatv/settings.py (InfoSettings) — line 78-88 as of pyatv 0.18.0
public sealed class MrpInfoSettings
	{
	/// <summary>Gets or sets the client name reported to the device. pyatv/settings.py — line 81 as of pyatv 0.18.0</summary>
	public string Name
		{
		get;
		set;
		} = "AppleTvControlLibrary";

	/// <summary>Gets or sets the OS build version reported to the device. pyatv/settings.py — line 87 as of pyatv 0.18.0</summary>
	public string OsBuild
		{
		get;
		set;
		} = "20K71";
	}

/// <summary>
/// Protocol logic related to MRP: connects, performs the initial DEVICE_INFORMATION exchange,
/// enables encryption via pair-verify, sends the post-encryption bootstrap messages, and
/// provides request/response correlation plus unsolicited-message dispatch on top of the
/// underlying <see cref="IMrpFrameConnection"/>.
/// </summary>
/// <remarks>
/// This type has no socket I/O of its own; a caller supplies <see cref="AsyncSender"/> to
/// transmit framed bytes and feeds inbound bytes to the underlying <see cref="IMrpFrameConnection"/>
/// (via <see cref="Connection"/>), mirroring the transport-agnostic design already used by
/// AppleTv.Companion's <c>CompanionProtocol</c>.
/// </remarks>
// pyatv/protocols/mrp/protocol.py (MrpProtocol) — line 93-295 as of pyatv 0.18.0
public sealed class MrpProtocol : IMrpConnectionListener, IDisposable
	{
	private readonly SrpAuthHandler _srp;
	private readonly MrpInfoSettings _info;
	private readonly ConcurrentDictionary<string, TaskCompletionSource<ProtocolMessage>> _outstanding = new ();
	private readonly SemaphoreSlim _sendGate = new (1, 1);
	private CancellationTokenSource? _heartbeatCts;

	/// <summary>Initializes a new instance of the <see cref="MrpProtocol"/> class.</summary>
	/// <param name="connection">The underlying framed connection. May be an AirPlay-tunneled connection, or historically the retired raw-TCP <c>MrpConnection</c> (see <c>archive/mrp-tcp-transport</c>).</param>
	/// <param name="srp">The SRP handler used for pair-verify and key derivation.</param>
	/// <param name="info">Client information reported in the initial DEVICE_INFORMATION message.</param>
	// pyatv/protocols/mrp/protocol.py (__init__) — line 104-121 as of pyatv 0.18.0
	public MrpProtocol (IMrpFrameConnection connection, SrpAuthHandler srp, MrpInfoSettings info)
		{
		Connection = connection;
		_srp = srp;
		_info = info;
		Connection.Listener = this;
		}

	/// <summary>Gets the underlying connection, so a transport can feed it received bytes.</summary>
	public IMrpFrameConnection Connection
		{
		get;
		}

	/// <summary>Gets or sets the credentials used to enable encryption. Set externally when reusing
	/// previously-paired credentials rather than pairing fresh (mirrors pyatv checking
	/// <c>self.service.credentials</c>). pyatv/protocols/mrp/protocol.py — line 137-140 as of pyatv 0.18.0</summary>
	public HapCredentials? Credentials { get; set; }

	/// <summary>Gets or sets the asynchronous callback that transmits fully-built frames.</summary>
	public Func<byte[], Task>? AsyncSender
		{
		get;
		set;
		}

	/// <summary>
	/// Gets the last-known device power state, derived from <c>DeviceInfoMessage.logicalDeviceCount</c>
	/// on every DEVICE_INFO_MESSAGE / DEVICE_INFO_UPDATE_MESSAGE seen so far.
	/// </summary>
	// pyatv/protocols/mrp/__init__.py (MrpPower.power_state / _get_power_state) — line 651-695 as of pyatv 0.18.0
	public MrpPowerState PowerState
		{
		get;
		private set;
		} = MrpPowerState.Unknown;

	/// <summary>
	/// Raised when <see cref="PowerState"/> changes as a result of an inbound DEVICE_INFO_MESSAGE or
	/// DEVICE_INFO_UPDATE_MESSAGE. Carries the previous and new power state, in that order.
	/// </summary>
	// pyatv/protocols/mrp/__init__.py (MrpPower._update_power_state, listener.powerstate_update) — line 673-684 as of pyatv 0.18.0
	public event Action<MrpPowerState, MrpPowerState>? PowerStateChanged;

	// pyatv/protocols/mrp/__init__.py (MrpPower._get_power_state) — line 686-695 as of pyatv 0.18.0
	private static MrpPowerState GetPowerState (ProtocolMessage message)
		{
		if (!message.HasExtension (DeviceInfoMessageExtensions.DeviceInfoMessage))
			{
			return MrpPowerState.Unknown;
			}

		DeviceInfoMessage info = message.GetExtension (DeviceInfoMessageExtensions.DeviceInfoMessage);
		return !info.HasLogicalDeviceCount ? MrpPowerState.Unknown : info.LogicalDeviceCount >= 1 ? MrpPowerState.On : MrpPowerState.Off;
		}

	// pyatv/protocols/mrp/__init__.py (MrpPower._update_power_state) — line 673-684 as of pyatv 0.18.0
	private void UpdatePowerState (ProtocolMessage message)
		{
		if (message.Type is not ProtocolMessage.Types.Type.DeviceInfoMessage and not ProtocolMessage.Types.Type.DeviceInfoUpdateMessage)
			{
			return;
			}

		MrpPowerState oldState = PowerState;
		MrpPowerState newState = GetPowerState (message);
		PowerState = newState;

		if (newState != oldState)
			{
			PowerStateChanged?.Invoke (oldState, newState);
			}
		}

	/// <summary>Gets or sets the listener notified when an unsolicited message is dispatched.</summary>
	public IMrpProtocolListener? Listener
		{
		get;
		set;
		}

	/// <summary>
	/// Raised for every inbound message, before correlation/dispatch, carrying the raw message
	/// type number and full serialized payload.
	/// </summary>
	/// <remarks>
	/// This is a diagnostic-only hook, not a port of anything in pyatv. Its purpose is to make
	/// message types absent from the vendored pyatv 0.18.0 <c>.proto</c> set observable instead of
	/// silently dropped: <c>ProtocolMessage.Type</c> declares <c>SET_READY_STATE_MESSAGE = 36</c>
	/// and <c>UPDATE_ACTIVE_SYSTEM_ENDPOINT_MESSAGE = 77</c> with no corresponding extension
	/// message/field anywhere in the tree, and the enum itself skips 13, 14 and 45 entirely, so
	/// pyatv has no name — and therefore no handler — for whatever Apple sends under those numbers.
	/// <see cref="Listener"/>/<see cref="IMrpProtocolListener"/> only ever surfaces
	/// <see cref="ProtocolMessage"/> instances decoded against <see cref="MrpExtensions.Registry"/>,
	/// which is sufficient for known extension fields but does not by itself make an unnamed type
	/// number easy to spot; this event exists purely so a consumer can log every
	/// <c>(type, raw bytes)</c> pair unconditionally while investigating unknown wire traffic.
	/// </remarks>
	public event Action<int, byte[]>? RawMessageReceived;

	/// <summary>Gets the current protocol state.</summary>
	// pyatv/protocols/mrp/protocol.py (self._state) — line 121 as of pyatv 0.18.0
	public MrpProtocolState State
		{
		get;
		private set;
		} = MrpProtocolState.NotConnected;

	/// <summary>Gets or sets how long to wait for a response before <see cref="SendAndReceiveAsync"/> throws
	/// a <see cref="MrpProtocolException"/>.</summary>
	public TimeSpan ResponseTimeout
		{
		get;
		set;
		} = TimeSpan.FromSeconds (5);

	/// <summary>
	/// Send the initial DEVICE_INFORMATION message, enable encryption if credentials are set, and
	/// run the post-encryption bootstrap sequence.
	/// </summary>
	/// <param name="skipInitialMessages">If <see langword="true"/>, stop right after DEVICE_INFORMATION
	/// (used by proxy-style reuse of a protocol object). pyatv/protocols/mrp/protocol.py — line 154-155 as of pyatv 0.18.0</param>
	/// <param name="cancellationToken">A token that cancels the bootstrap exchanges.</param>
	// pyatv/protocols/mrp/protocol.py (start) — line 123-172 as of pyatv 0.18.0
	public async Task StartAsync (bool skipInitialMessages = false, CancellationToken cancellationToken = default)
		{
		if (State != MrpProtocolState.NotConnected)
			{
			throw new InvalidOperationException ($"Invalid state: {State}");
			}

		State = MrpProtocolState.Connecting;

		try
			{
			State = MrpProtocolState.Connected;

			// pyatv/protocols/mrp/protocol.py — line 142-146 as of pyatv 0.18.0: the first message must
			// always be DEVICE_INFORMATION, otherwise the device will not respond with anything.
			ProtocolMessage deviceInfo = await SendAndReceiveAsync (
				MrpMessages.DeviceInformation (_info.Name, _info.OsBuild, System.Text.Encoding.UTF8.GetString (_srp.PairingId)),
				generateIdentifier: true,
				cancellationToken).ConfigureAwait (false);

			// pyatv/protocols/mrp/protocol.py — line 148-150 as of pyatv 0.18.0: distribute the device
			// information to listeners, since send_and_receive stops that propagation.
			Listener?.MessageReceived (deviceInfo);

			if (skipInitialMessages)
				{
				return;
				}

			await EnableEncryptionAsync (cancellationToken).ConfigureAwait (false);

			// pyatv/protocols/mrp/protocol.py — line 159-161 as of pyatv 0.18.0: this should be the
			// first message sent after encryption has been enabled.
			await SendAsync (MrpMessages.SetConnectionState (), cancellationToken).ConfigureAwait (false);

			// pyatv/protocols/mrp/protocol.py — line 163-165 as of pyatv 0.18.0: subscribe to updates
			// at this stage.
			// nowPlaying: true — the WPF remote's now-playing/artwork display depends on receiving
			// SET_STATE_MESSAGE/UPDATE_CONTENT_ITEM_MESSAGE updates, which pyatv's default config
			// (nowPlaying=false) suppresses. pyatv/protocols/mrp/messages.py (client_updates_config)
			// — line 82-97 as of pyatv 0.18.0: nowPlaying is an independent subscription flag.
			_ = await SendAndReceiveAsync (MrpMessages.ClientUpdatesConfig (nowPlaying: true), generateIdentifier: true, cancellationToken).ConfigureAwait (false);
			_ = await SendAndReceiveAsync (MrpMessages.GetKeyboardSession (), generateIdentifier: true, cancellationToken).ConfigureAwait (false);
			}
		catch
			{
			Stop ();
			throw;
			}

		State = MrpProtocolState.Ready;
		}

	// pyatv/protocols/mrp/protocol.py (_enable_encryption) — line 207-221 as of pyatv 0.18.0
	private async Task EnableEncryptionAsync (CancellationToken cancellationToken)
		{
		// Encryption can be enabled whenever credentials are available but only after
		// DEVICE_INFORMATION has been sent.
		if (Credentials is null)
			{
			return;
			}

		var pairVerify = new MrpPairVerifyProcedure (
			(message) => SendAndReceiveAsync (message, generateIdentifier: false, cancellationToken),
			_srp,
			Credentials);

		bool verified = await pairVerify.VerifyCredentialsAsync ().ConfigureAwait (false);
		if (!verified)
			{
			throw new AuthenticationException ("Failed to verify credentials");
			}

		(byte[] outputKey, byte[] inputKey) = pairVerify.EncryptionKeys (
			MrpProtocolConstants.SrpSalt, MrpProtocolConstants.SrpOutputInfo, MrpProtocolConstants.SrpInputInfo);
		Connection.EnableEncryption (outputKey, inputKey);
		}

	/// <summary>Disconnect from the device, failing any outstanding requests.</summary>
	// pyatv/protocols/mrp/protocol.py (stop) — line 174-186 as of pyatv 0.18.0
	public void Stop ()
		{
		_heartbeatCts?.Cancel ();
		_heartbeatCts = null;

		var fault = new MrpProtocolException ("Connection stopped while awaiting a response");
		foreach (KeyValuePair<string, TaskCompletionSource<ProtocolMessage>> pending in _outstanding)
			{
			if (_outstanding.TryRemove (pending.Key, out TaskCompletionSource<ProtocolMessage>? completion))
				{
				_ = completion.TrySetException (fault);
				}
			}

		State = MrpProtocolState.Stopped;
		}

	/// <summary>Send a message and expect no response.</summary>
	/// <param name="message">The message to send.</param>
	/// <param name="cancellationToken">A token that cancels waiting to send.</param>
	// pyatv/protocols/mrp/protocol.py (send) — line 223-231 as of pyatv 0.18.0
	public async Task SendAsync (ProtocolMessage message, CancellationToken cancellationToken = default)
		{
		if (State is not (MrpProtocolState.Connected or MrpProtocolState.Ready))
			{
			throw new InvalidOperationException ($"Invalid state: {State}");
			}

		await TransmitAsync (message, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>Send a message and wait for a response.</summary>
	/// <param name="message">The message to send.</param>
	/// <param name="generateIdentifier">Whether to generate and set a new <c>identifier</c> on the
	/// message before sending (some messages, like crypto pairing, never carry one, and are instead
	/// correlated by message type since only one such exchange can ever be outstanding).</param>
	/// <param name="cancellationToken">A token that cancels waiting to send or receive a response.</param>
	/// <returns>The response message received from the device.</returns>
	// pyatv/protocols/mrp/protocol.py (send_and_receive) — line 233-260 as of pyatv 0.18.0
	public async Task<ProtocolMessage> SendAndReceiveAsync (ProtocolMessage message, bool generateIdentifier = true, CancellationToken cancellationToken = default)
		{
		if (State is not (MrpProtocolState.Connected or MrpProtocolState.Ready))
			{
			throw new InvalidOperationException ($"Invalid state: {State}");
			}

		// pyatv/protocols/mrp/protocol.py — line 246-257 as of pyatv 0.18.0: some messages respond
		// with the same identifier used in the request; others (e.g. crypto pairing) never include
		// one, and are instead correlated with a "fake" identifier built from the message type,
		// since only one such exchange can ever be outstanding at a time.
		string identifier;
		if (generateIdentifier)
			{
			identifier = Guid.NewGuid ().ToString ().ToUpperInvariant ();
			message.Identifier = identifier;
			}
		else
			{
			identifier = "type_" + (int)message.Type;
			}

		var completion = new TaskCompletionSource<ProtocolMessage> (TaskCreationOptions.RunContinuationsAsynchronously);
		_outstanding[identifier] = completion;

		try
			{
			await TransmitAsync (message, cancellationToken).ConfigureAwait (false);
			}
		catch
			{
			_ = _outstanding.TryRemove (identifier, out _);
			throw;
			}

		Task completed = await Task.WhenAny (completion.Task, Task.Delay (ResponseTimeout, cancellationToken)).ConfigureAwait (false);
		if (completed != completion.Task)
			{
			_ = _outstanding.TryRemove (identifier, out _);
			throw new MrpProtocolException ($"No response received for identifier {identifier} (sent as {message.Type})");
			}

		return await completion.Task.ConfigureAwait (false);
		}

	private async Task TransmitAsync (ProtocolMessage message, CancellationToken cancellationToken)
		{
		if (AsyncSender is null)
			{
			throw new InvalidOperationException ($"{nameof (AsyncSender)} must be set before sending frames");
			}

		byte[] frame = Connection.BuildMessage (message.ToByteArray ());
		await AsyncSender (frame).ConfigureAwait (false);
		_ = cancellationToken;
		}

	/// <summary>A complete, decrypted message was received from the device.</summary>
	/// <param name="data">The serialized protobuf message bytes.</param>
	// pyatv/protocols/mrp/protocol.py (message_received) — line 283-295 as of pyatv 0.18.0
	public void MessageReceived (byte[] data)
		{
		ProtocolMessage message = ProtocolMessage.Parser.WithExtensionRegistry (MrpExtensions.Registry).ParseFrom (data);

		// pyatv/protocols/mrp/__init__.py — line 642-645 as of pyatv 0.18.0: MrpPower listens to both
		// DEVICE_INFO_MESSAGE and DEVICE_INFO_UPDATE_MESSAGE unconditionally, ahead of send_and_receive
		// correlation, so the power state stays current even when a DEVICE_INFO_MESSAGE is consumed as
		// the reply to StartAsync's initial exchange rather than dispatched to Listener.
		UpdatePowerState (message);

		// Diagnostic-only, not from pyatv: fires unconditionally, before correlation/dispatch, so
		// message types with no vendored .proto (e.g. SET_READY_STATE_MESSAGE = 36,
		// UPDATE_ACTIVE_SYSTEM_ENDPOINT_MESSAGE = 77, or the unnamed 13/14/45) are still observable
		// by raw type number and payload rather than silently absorbed by the extension registry.
		RawMessageReceived?.Invoke ((int)message.Type, data);

		// pyatv/protocols/mrp/protocol.py — line 285-293 as of pyatv 0.18.0: if the message
		// identifier is outstanding, then someone is waiting for the response, so save it here.
		string identifier = !string.IsNullOrEmpty (message.Identifier) ? message.Identifier : "type_" + (int)message.Type;
		if (_outstanding.TryRemove (identifier, out TaskCompletionSource<ProtocolMessage>? completion))
			{
			_ = completion.TrySetResult (message);
			}
		else
			{
			Listener?.MessageReceived (message);
			}
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		Stop ();
		_sendGate.Dispose ();
		}
	}
