// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;

namespace AppleTvControlLibrary.Protocol;

/// <summary>
/// Type of an OPACK message exchanged over a Companion connection.
/// </summary>
// pyatv/protocols/companion/protocol.py (MessageType) — line 54-59 as of pyatv 0.18.0
public enum MessageType
	{
	/// <summary>An unsolicited event. pyatv/protocols/companion/protocol.py — line 57 as of pyatv 0.18.0</summary>
	Event = 1,
	/// <summary>A request expecting a response. pyatv/protocols/companion/protocol.py — line 58 as of pyatv 0.18.0</summary>
	Request = 2,
	/// <summary>A response to a previously sent request. pyatv/protocols/companion/protocol.py — line 59 as of pyatv 0.18.0</summary>
	Response = 3,
	}

/// <summary>
/// Listener interface for a Companion protocol instance.
/// </summary>
// pyatv/protocols/companion/protocol.py (CompanionProtocolListener) — line 65-69 as of pyatv 0.18.0
public interface ICompanionProtocolListener
	{
	/// <summary>An event was received from the remote device.</summary>
	/// <param name="eventName">The event identifier (the message's <c>_i</c> field).</param>
	/// <param name="data">The event content (the message's <c>_c</c> field).</param>
	// pyatv/protocols/companion/protocol.py — line 68 as of pyatv 0.18.0
	void EventReceived (string eventName, Dictionary<object, object?> data);
	}

/// <summary>
/// Event data for <see cref="CompanionProtocol.ConnectionFaulted"/> and
/// <see cref="CompanionApi.ConnectionClosed"/>.
/// </summary>
/// <remarks>
/// Mirrors pyatv's <c>DeviceListener.connection_lost(exc)</c> / <c>connection_closed()</c>
/// distinction (see <c>pyatv/interface.py</c>): a <see langword="null"/> <see cref="Exception"/>
/// means the connection was closed cleanly (the equivalent of pyatv's <c>connection_closed()</c>),
/// while a non-null value means it was lost unexpectedly (<c>connection_lost(exc)</c>).
/// </remarks>
public sealed class ConnectionClosedEventArgs : EventArgs
	{
	/// <summary>Initializes a new instance of the <see cref="ConnectionClosedEventArgs"/> class.</summary>
	/// <param name="exception">The exception that caused the connection to be lost, or <see langword="null"/> for a clean close.</param>
	public ConnectionClosedEventArgs (Exception? exception)
		{
		Exception = exception;
		}

	/// <summary>
	/// Gets the exception that caused the connection to be lost, or <see langword="null"/> if the
	/// connection was closed cleanly/expectedly (e.g. via <see cref="CompanionProtocol.Dispose"/>
	/// being an explicit teardown is still reported with an exception; only an intentional,
	/// non-faulting close reports <see langword="null"/>).
	/// </summary>
	public Exception? Exception
		{
		get;
		}
	}

/// <summary>
/// Raised when a Companion protocol exchange fails.
/// </summary>
// pyatv/exceptions.py (ProtocolError) — line 31-33 as of pyatv 0.18.0
public class ProtocolException : Exception
	{
	/// <summary>Initializes a new instance of the <see cref="ProtocolException"/> class.</summary>
	public ProtocolException ()
		{
		}

	/// <summary>Initializes a new instance of the <see cref="ProtocolException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	public ProtocolException (string message) : base (message)
		{
		}

	/// <summary>Initializes a new instance of the <see cref="ProtocolException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	public ProtocolException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}

/// <summary>
/// Identifies a pending exchange either by XID (regular OPACK messages) or by the
/// <see cref="FrameType"/> that is expected in response (auth messages, which never have an XID
/// since parallel authentication attempts are impossible).
/// </summary>
// pyatv/protocols/companion/protocol.py (FrameIdType) — line 44-49 as of pyatv 0.18.0
public readonly struct FrameIdentifier : IEquatable<FrameIdentifier>
	{
	private readonly int? _xid;
	private readonly FrameType? _frameType;

	private FrameIdentifier (int? xid, FrameType? frameType)
		{
		_xid = xid;
		_frameType = frameType;
		}

	/// <summary>Creates a <see cref="FrameIdentifier"/> keyed by XID.</summary>
	public static FrameIdentifier FromXid (int xid) => new (xid, null);

	/// <summary>Creates a <see cref="FrameIdentifier"/> keyed by frame type.</summary>
	public static FrameIdentifier FromFrameType (FrameType frameType) => new (null, frameType);

	/// <inheritdoc/>
	public bool Equals (FrameIdentifier other) => _xid == other._xid && _frameType == other._frameType;

	/// <inheritdoc/>
	public override bool Equals (object? obj) => obj is FrameIdentifier other && Equals (other);

	/// <inheritdoc/>
	public override int GetHashCode () => (_xid, _frameType).GetHashCode ();

	/// <summary>Determines whether two <see cref="FrameIdentifier"/> instances are equal.</summary>
	public static bool operator == (FrameIdentifier left, FrameIdentifier right) => left.Equals (right);

	/// <summary>Determines whether two <see cref="FrameIdentifier"/> instances are not equal.</summary>
	public static bool operator != (FrameIdentifier left, FrameIdentifier right) => !left.Equals (right);

	/// <inheritdoc/>
	public override string ToString ()
		{
		return _xid is not null ? $"XID={_xid}" : $"FrameType={_frameType}";
		}
	}

/// <summary>
/// Protocol logic related to Companion: frame dispatch, XID/frame-type based response
/// correlation, and OPACK-level send/receive plumbing built on top of <see cref="CompanionConnection"/>.
/// </summary>
/// <remarks>
/// Unlike pyatv, which relies on asyncio futures (<c>SharedData</c>) to await a response, this
/// port uses synchronous callback-based correlation: sending a frame is expected to synchronously
/// (or, for a real transport, eventually) result in <c>CompanionConnection.FrameReceived</c> being
/// invoked with the matching response, at which point the originally registered continuation is
/// invoked. This
/// keeps the type usable both by an in-memory test harness (as used for WP5/WP6 validation) and by
/// a future asynchronous socket transport without changing the correlation logic itself.
/// </remarks>
// pyatv/protocols/companion/protocol.py (CompanionProtocol) — line 72-234 as of pyatv 0.18.0
public sealed class CompanionProtocol : IDisposable, IAsyncDisposable
	{
	// pyatv/protocols/companion/protocol.py — line 40-42 as of pyatv 0.18.0
	/// <summary>SRP HKDF salt used when deriving Companion encryption keys.</summary>
	public const string SRP_SALT = "";
	/// <summary>SRP HKDF info string for the client's outbound encryption key.</summary>
	public const string SRP_OUTPUT_INFO = "ClientEncrypt-main";
	/// <summary>SRP HKDF info string for the client's inbound encryption key.</summary>
	public const string SRP_INPUT_INFO = "ServerEncrypt-main";

	private readonly CompanionConnection _connection;
	private readonly SrpAuthHandler _srp;
	// A real transport delivers frames on a background read thread/task while exchanges are
	// issued from the caller's thread, so this table needs to be safe for concurrent access
	// (unlike the in-memory fake-device harness, where everything runs on one thread).
	private readonly System.Collections.Concurrent.ConcurrentDictionary<FrameIdentifier, TaskCompletionSource<Dictionary<object, object?>>> _pending = new ();
	private readonly SemaphoreSlim _sendGate = new (1, 1);
	private readonly SemaphoreSlim _authGate = new (1, 1);
	private readonly object _eventDispatchLock = new ();
	private Task _eventDispatch = Task.CompletedTask;

	// pyatv/protocols/companion/protocol.py (self._xid: int = randint(0, 2**16) — line 89 as of pyatv 0.18.0)
	private uint _xid;

	/// <summary>Initializes a new instance of the <see cref="CompanionProtocol"/> class.</summary>
	/// <param name="connection">The underlying framed connection.</param>
	/// <param name="srp">The SRP handler used for pair-verify.</param>
	// pyatv/protocols/companion/protocol.py (__init__) — line 77-92 as of pyatv 0.18.0
	public CompanionProtocol (CompanionConnection connection, SrpAuthHandler srp)
		{
		_connection = connection;
		_srp = srp;
		_xid = (uint)new Random ().Next (0, 65536);
		_connection.FrameReceived += (sender, frameType, data) => OnFrameReceived (frameType, data);
		_connection.Faulted += (sender, exception) =>
			{
			FaultPendingRequests (exception);
			ConnectionFaulted?.Invoke (this, new ConnectionClosedEventArgs (exception));
			};
		}

	/// <summary>
	/// Raised when the underlying <see cref="CompanionConnection"/> enters its terminal faulted
	/// state, whether due to an unexpected transport/decrypt/dispatch failure, a clean remote
	/// close, or disposal. See <see cref="ConnectionClosedEventArgs.Exception"/> to distinguish
	/// an unexpected loss from an expected close.
	/// </summary>
	// pyatv/protocols/companion/connection.py (connection_lost) — line 161-167 as of pyatv 0.18.0
	public event EventHandler<ConnectionClosedEventArgs>? ConnectionFaulted;

	/// <summary>Gets or sets the listener notified when an event frame is received.</summary>
	public ICompanionProtocolListener? Listener
		{
		get;
		set;
		}

	/// <summary>Gets a callback invoked whenever a fully-built frame needs to be transmitted.</summary>
	/// <remarks>
	/// This decouples <see cref="CompanionProtocol"/> from any specific transport; a caller
	/// (production socket code, or a test harness driving a fake device in-memory) is
	/// responsible for actually delivering the bytes and, eventually, feeding any response back
	/// in via <c>CompanionConnection.ReceiveData</c>.
	/// </remarks>
	[Obsolete ("Use AsyncSender instead.")]
	public Action<byte[]>? Sender
		{
		get;
		set;
		}

	/// <summary>Gets or sets the asynchronous callback that transmits fully-built frames.</summary>
	public Func<byte[], Task>? AsyncSender
		{
		get;
		set;
		}

	/// <summary>Exchange an auth frame (<c>PS_*</c> or <c>PV_*</c>).</summary>
	/// <param name="frameType">The frame type to send.</param>
	/// <param name="data">The message content.</param>
	/// <returns>The decoded OPACK response.</returns>
	// pyatv/protocols/companion/protocol.py (exchange_auth) — line 125-141 as of pyatv 0.18.0
	[Obsolete ("Use ExchangeAuthAsync instead.")]
	public Dictionary<object, object?> ExchangeAuth (FrameType frameType, Dictionary<string, object?> data)
		{
		return ExchangeAuthAsync (frameType, data).ConfigureAwait (false).GetAwaiter ().GetResult ();
		}

	/// <summary>Asynchronously exchanges an auth frame (<c>PS_*</c> or <c>PV_*</c>).</summary>
	public async Task<Dictionary<object, object?>> ExchangeAuthAsync (FrameType frameType, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
		{
		// pyatv/protocols/companion/protocol.py — line 132-140 as of pyatv 0.18.0: *_Start is only used for the first
		// message, then *_Next is used for remaining messages (even the response to the first).
		FrameType identifier = frameType switch
			{
			FrameType.PS_Start => FrameType.PS_Next,
			FrameType.PV_Start => FrameType.PV_Next,
			_ => frameType,
			};

		await _authGate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			return await ExchangeGenericAsync (frameType, data, FrameIdentifier.FromFrameType (identifier), cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			_authGate.Release ();
			}
		}

	/// <summary>Send data as OPACK and decode the result as OPACK.</summary>
	/// <param name="frameType">The frame type to send.</param>
	/// <param name="data">The message content.</param>
	/// <returns>The decoded OPACK response.</returns>
	// pyatv/protocols/companion/protocol.py (exchange_opack) — line 143-153 as of pyatv 0.18.0
	[Obsolete ("Use ExchangeOpackAsync instead.")]
	public Dictionary<object, object?> ExchangeOpack (FrameType frameType, Dictionary<string, object?> data)
		{
		return ExchangeOpackAsync (frameType, data).ConfigureAwait (false).GetAwaiter ().GetResult ();
		}

	/// <summary>Asynchronously sends OPACK data and decodes the OPACK response.</summary>
	public Task<Dictionary<object, object?>> ExchangeOpackAsync (FrameType frameType, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
		{
		return ExchangeGenericAsync (frameType, data, identifier: null, cancellationToken);
		}

	/// <summary>
	/// Gets or sets how long to wait for a response before <see cref="ExchangeAuth"/> or
	/// <see cref="ExchangeOpack"/> throws a <see cref="ProtocolException"/>.
	/// </summary>
	/// <remarks>
	/// Unlike the in-memory fake device used for WP5/WP6 validation (where
	/// <c>CompanionConnection.FrameReceived</c> fires synchronously, inline with the send
	/// call), a real socket transport delivers the response from a separate read thread/task,
	/// asynchronously with respect to the caller of <see cref="ExchangeAuth"/>/
	/// <see cref="ExchangeOpack"/>. This wait handle lets both usages share the same
	/// correlation logic without changing it: the fake device signals it before the wait ever
	/// blocks, while a real transport signals it once the background read loop processes the
	/// matching frame.
	/// </remarks>
	public TimeSpan ResponseTimeout
		{
		get;
		set;
		} = TimeSpan.FromSeconds (10);

	// pyatv/protocols/companion/protocol.py (_exchange_generic_opack) — line 155-176 as of pyatv 0.18.0
	private async Task<Dictionary<object, object?>> ExchangeGenericAsync (FrameType frameType, Dictionary<string, object?> data, FrameIdentifier? identifier, CancellationToken cancellationToken)
		{
		var completion = new TaskCompletionSource<Dictionary<object, object?>> (TaskCreationOptions.RunContinuationsAsynchronously);
		FrameIdentifier actualIdentifier;
		await _sendGate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (identifier is null)
				{
				uint xid = _xid++;
				data["_x"] = (long)xid;
				actualIdentifier = FrameIdentifier.FromXid (unchecked ((int)xid));
				}
			else
				{
				actualIdentifier = identifier.Value;
				}

			_pending[actualIdentifier] = completion;
			try
				{
				System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Sending {frameType}, awaiting response for {actualIdentifier}");
				await SendOpackCoreAsync (frameType, data, cancellationToken).ConfigureAwait (false);
				}
			catch
				{
				_pending.TryRemove (actualIdentifier, out _);
				throw;
				}
			}
		finally
			{
			_sendGate.Release ();
			}

		Dictionary<object, object?> result;
		Task completed = await Task.WhenAny (completion.Task, Task.Delay (ResponseTimeout, cancellationToken)).ConfigureAwait (false);
		if (completed.IsCanceled)
			{
			_pending.TryRemove (actualIdentifier, out _);
			throw new OperationCanceledException (cancellationToken);
			}
		if (completed != completion.Task)
			{
			_pending.TryRemove (actualIdentifier, out _);
			System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Timed out after {ResponseTimeout} waiting for {actualIdentifier} (sent as {frameType})");
			throw new ProtocolException ($"No response received for {actualIdentifier} (sent as {frameType})");
			}

		result = await completion.Task.ConfigureAwait (false);

		// pyatv/protocols/companion/protocol.py — line 173-174 as of pyatv 0.18.0
		if (result.TryGetValue ("_em", out object? errorMessage))
			{
			throw new ProtocolException ($"Command failed: {errorMessage}");
			}

		return result;
		}

	private void FaultPendingRequests (Exception? exception)
		{
		Exception fault = exception ?? new ProtocolException ("Connection faulted");
		foreach (var pending in _pending)
			{
			if (_pending.TryRemove (pending.Key, out var completion))
				{
				completion.TrySetException (new ProtocolException ("Connection faulted while awaiting a response", fault));
				}
			}
		}

	/// <summary>Send data encoded with OPACK, adding an XID if not already present.</summary>
	/// <param name="frameType">The frame type to send.</param>
	/// <param name="data">The message content.</param>
	// pyatv/protocols/companion/protocol.py (send_opack) — line 178-186 as of pyatv 0.18.0
	[Obsolete ("Use SendOpackAsync instead.")]
	public void SendOpack (FrameType frameType, Dictionary<string, object?> data)
		{
		SendOpackAsync (frameType, data).ConfigureAwait (false).GetAwaiter ().GetResult ();
		}

	/// <summary>Asynchronously sends data encoded with OPACK, adding an XID if not already present.</summary>
	/// <param name="frameType">The frame type to send.</param>
	/// <param name="data">The message content.</param>
	/// <param name="cancellationToken">A token that cancels waiting to send or receive a response.</param>
	/// <returns>A task that completes after the transport accepts the frame.</returns>
	public async Task SendOpackAsync (FrameType frameType, Dictionary<string, object?> data, CancellationToken cancellationToken = default)
		{
		await _sendGate.WaitAsync (cancellationToken).ConfigureAwait (false);
		try
			{
			if (!data.ContainsKey ("_x"))
				{
				data["_x"] = (long)_xid++;
				}

			await SendOpackCoreAsync (frameType, data, cancellationToken).ConfigureAwait (false);
			}
		finally
			{
			_sendGate.Release ();
			}
		}

	private async Task SendOpackCoreAsync (FrameType frameType, Dictionary<string, object?> data, CancellationToken cancellationToken)
		{
		#pragma warning disable CS0618
		if (AsyncSender is null && Sender is null)
			{
			throw new InvalidOperationException ($"{nameof (AsyncSender)} must be set before sending frames");
			}
		#pragma warning restore CS0618

		byte[] frame = _connection.BuildFrame (frameType, AppleTvControlLibrary.Opack.Opack.Pack (data));
		try
			{
			if (AsyncSender is not null)
				{
				await AsyncSender (frame).ConfigureAwait (false);
				}
			else
				{
				#pragma warning disable CS0618
				Sender! (frame);
				#pragma warning restore CS0618
				}
			}
		catch (Exception ex)
			{
			_connection.Fault (ex);
			throw new ProtocolException ("Frame transport failed; the session has been faulted", ex);
			}
		}

	// pyatv/protocols/companion/protocol.py (frame_received) — line 188-207 as of pyatv 0.18.0
	private static readonly FrameType[] AuthFrames =
		{
		FrameType.PS_Start, FrameType.PS_Next, FrameType.PV_Start, FrameType.PV_Next,
		};

	private static readonly FrameType[] OpackFrames =
		{
		FrameType.U_OPACK, FrameType.E_OPACK, FrameType.P_OPACK,
		};

	private void OnFrameReceived (FrameType frameType, byte[] data)
		{
		System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Received frame {frameType} ({data.Length} bytes)");

		bool isAuth = Array.IndexOf (AuthFrames, frameType) >= 0;
		bool isOpack = Array.IndexOf (OpackFrames, frameType) >= 0;

		if (!isAuth && !isOpack)
			{
			System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Ignoring frame {frameType}: not an auth or OPACK frame");
			return;
			}

		object? unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (data, out _);
		if (unpacked is not Dictionary<object, object?> opackData)
			{
			System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Ignoring frame {frameType}: payload did not decode to an OPACK dictionary");
			return;
			}

		if (isAuth)
			{
			HandleAuth (frameType, opackData);
			}
		else
			{
			HandleOpack (opackData);
			}
		}

	// pyatv/protocols/companion/protocol.py (_handle_auth) — line 209-215 as of pyatv 0.18.0
	private void HandleAuth (FrameType frameType, Dictionary<object, object?> opackData)
		{
		var identifier = FrameIdentifier.FromFrameType (frameType);
		if (_pending.TryRemove (identifier, out var continuation))
			{
			continuation.TrySetResult (opackData);
			}
		else
			{
			System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Received auth frame {frameType} but nothing was waiting for it (identifier {identifier})");
			}
		}

	// pyatv/protocols/companion/protocol.py (_handle_opack) — line 217-234 as of pyatv 0.18.0
	private void HandleOpack (Dictionary<object, object?> opackData)
		{
		object? messageType = opackData.TryGetValue ("_t", out object? mt) ? mt : null;
		long? messageTypeValue = ToLong (messageType);

		if (messageTypeValue == (long)MessageType.Event)
			{
			string eventName = (string)opackData["_i"]!;
			var content = opackData.TryGetValue ("_c", out object? c) && c is Dictionary<object, object?> dict
				? dict
				: new Dictionary<object, object?> ();
			DispatchEvent (eventName, content);
			}
		else if (messageTypeValue == (long)MessageType.Response)
			{
			long? xid = ToLong (opackData.TryGetValue ("_x", out object? x) ? x : null);
			if (xid is not null)
				{
				var identifier = FrameIdentifier.FromXid ((int)xid.Value);
				if (_pending.TryRemove (identifier, out var continuation))
					{
					continuation.TrySetResult (opackData);
					}
				else
					{
					System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Received response for XID={xid.Value} but nothing was waiting for it");
					}
				}
			else
				{
				System.Diagnostics.Debug.WriteLine ("[CompanionProtocol] Received a Response-type OPACK frame with no _x (XID) field");
				}
			}
		}

	private void DispatchEvent (string eventName, Dictionary<object, object?> content)
		{
		lock (_eventDispatchLock)
			{
			_eventDispatch = _eventDispatch.ContinueWith (
				_ =>
					{
					try
						{
						Listener?.EventReceived (eventName, content);
						}
					catch (Exception ex)
						{
						System.Diagnostics.Debug.WriteLine ($"[CompanionProtocol] Event listener failed for {eventName}: {ex}");
						}
					},
				CancellationToken.None,
				TaskContinuationOptions.None,
				TaskScheduler.Default);
			}
		}

	private static long? ToLong (object? value)
		{
		return value switch
			{
			null => null,
			long l => l,
			int i => i,
			AppleTvControlLibrary.Opack.SizedInteger si => si.Value,
			_ => Convert.ToInt64 (value, CultureInfo.InvariantCulture),
			};
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		_connection.Fault (new ObjectDisposedException (nameof (CompanionProtocol)));
		_authGate.Dispose ();
		_sendGate.Dispose ();
		}

	/// <summary>Asynchronously faults pending exchanges and disposes this protocol instance.</summary>
	public ValueTask DisposeAsync ()
		{
		Dispose ();
		return default;
		}
	}
