// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Threading;
using System.Threading.Tasks;

using Claunia.PropertyList;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Channels;
using AppleTvControlLibrary.Mrp.AirPlay.Http;
using AppleTvControlLibrary.Mrp.AirPlay.Rtsp;

namespace AppleTvControlLibrary.Mrp.AirPlay;

/// <summary>
/// Client identity fields reported to the receiver during AirPlay 2 session setup.
/// </summary>
/// <remarks>
/// A minimal analogue of pyatv's <c>InfoSettings</c> (pydantic model) exposing only the fields
/// <see cref="Ap2Session"/> needs. pyatv/settings.py — line 41-44 as of pyatv 0.18.0 gives the
/// library's own default values (a generic iPhone identity); this port keeps the same defaults
/// so a fresh setup behaves identically to pyatv out of the box.
/// </remarks>
// pyatv/settings.py (InfoSettings) — line 76-88 as of pyatv 0.18.0
public sealed class Ap2InfoSettings
	{
	/// <summary>Gets or sets the client name reported to the device.</summary>
	public string Name { get; set; } = "pyatv";

	/// <summary>Gets or sets the client MAC address reported to the device.</summary>
	// pyatv/settings.py (DEFUALT_MAC) — line 39 as of pyatv 0.18.0
	public string Mac { get; set; } = "02:70:79:61:74:76";

	/// <summary>Gets or sets the client model reported to the device.</summary>
	// pyatv/settings.py (DEFAULT_MODEL) — line 41 as of pyatv 0.18.0
	public string Model { get; set; } = "iPhone10,6";

	/// <summary>Gets or sets the client device identifier reported to the device.</summary>
	// pyatv/settings.py (DEFAULT_DEVICE_ID) — line 40 as of pyatv 0.18.0
	public string DeviceId { get; set; } = "FF:70:79:61:74:76";

	/// <summary>Gets or sets the client OS name reported to the device.</summary>
	// pyatv/settings.py (DEFAULT_OS_NAME) — line 42 as of pyatv 0.18.0
	public string OsName { get; set; } = "iPhone OS";

	/// <summary>Gets or sets the client OS build reported to the device.</summary>
	// pyatv/settings.py (DEFAULT_OS_BUILD) — line 43 as of pyatv 0.18.0
	public string OsBuild { get; set; } = "18G82";

	/// <summary>Gets or sets the client OS version reported to the device.</summary>
	// pyatv/settings.py (DEFAULT_OS_VERSION) — line 44 as of pyatv 0.18.0
	public string OsVersion { get; set; } = "14.7.1";
	}

/// <summary>
/// High-level session for AirPlay 2: opens a control connection, runs pair-verify, and sets up
/// the event and data-stream channels used to carry an MRP tunnel.
/// </summary>
// pyatv/protocols/airplay/ap2_session.py (AP2Session) — line 34-201 as of pyatv 0.18.0
public sealed class Ap2Session : IDisposable
	{
	// pyatv/protocols/airplay/ap2_session.py (EVENTS_SALT/EVENTS_WRITE_INFO/EVENTS_READ_INFO) — line 28-30 as of pyatv 0.18.0
	private const string EventsSalt = "Events-Salt";
	private const string EventsWriteInfo = "Events-Write-Encryption-Key";
	private const string EventsReadInfo = "Events-Read-Encryption-Key";

	// pyatv/protocols/airplay/ap2_session.py (DATASTREAM_SALT/DATASTREAM_OUTPUT_INFO/DATASTREAM_INPUT_INFO) — line 32-34 as of pyatv 0.18.0
	private const string DataStreamSalt = "DataStream-Salt"; // seed must be appended
	private const string DataStreamOutputInfo = "DataStream-Output-Encryption-Key";
	private const string DataStreamInputInfo = "DataStream-Input-Encryption-Key";

	// pyatv/protocols/airplay/ap2_session.py (FEEDBACK_INTERVAL) — line 25 as of pyatv 0.18.0: "This is what iOS uses"
	private static readonly TimeSpan FeedbackInterval = TimeSpan.FromSeconds (2.0);

	private readonly string _address;
	private readonly int _controlPort;
	private readonly HapCredentials _credentials;
	private readonly Ap2InfoSettings _info;
	private readonly Random _random = new ();

	private HttpConnection? _connection;
	private AirPlayHapPairVerifyProcedure? _verifier;
	private RtspSession? _rtsp;
	private EventChannel? _eventChannel;
	private CancellationTokenSource? _feedbackCts;
	private Task? _feedbackTask;

	/// <summary>Initializes a new instance of the <see cref="Ap2Session"/> class.</summary>
	/// <param name="address">The remote AirPlay control address.</param>
	/// <param name="controlPort">The remote AirPlay control port.</param>
	/// <param name="credentials">The previously-paired HAP credentials for the device.</param>
	/// <param name="info">Client identity reported to the device during setup.</param>
	// pyatv/protocols/airplay/ap2_session.py (__init__) — line 42-56 as of pyatv 0.18.0
	public Ap2Session (string address, int controlPort, HapCredentials credentials, Ap2InfoSettings? info = null)
		{
		_address = address;
		_controlPort = controlPort;
		_credentials = credentials;
		_info = info ?? new Ap2InfoSettings ();
		}

	/// <summary>Gets the data-stream channel that carries the MRP tunnel, once <see cref="SetupRemoteControlAsync"/> has completed.</summary>
	public DataStreamChannel? DataChannel { get; private set; }

	/// <summary>Raised when the periodic feedback keep-alive stops because the connection was closed cleanly.</summary>
	// pyatv/protocols/airplay/ap2_session.py (start_keep_alive._finish_func) — line 84-85 as of pyatv 0.18.0
	public event Action? KeepAliveFinished;

	/// <summary>Raised when the periodic feedback keep-alive fails after exhausting retries.</summary>
	// pyatv/protocols/airplay/ap2_session.py (start_keep_alive._failure_func) — line 87-88 as of pyatv 0.18.0
	public event Action<Exception>? KeepAliveFailed;

	/// <summary>Open the control connection and perform pair-verify.</summary>
	/// <param name="cancellationToken">A token used to cancel the connection attempt.</param>
	// pyatv/protocols/airplay/ap2_session.py (connect) — line 58-68 as of pyatv 0.18.0
	public async Task ConnectAsync (CancellationToken cancellationToken = default)
		{
		_connection = await HttpConnection.ConnectAsync (_address, _controlPort, cancellationToken).ConfigureAwait (false);
		_verifier = await AirPlayConnectionAuth.VerifyConnectionAsync (_credentials, _connection).ConfigureAwait (false);
		_rtsp = new RtspSession (_connection);
		}

	/// <summary>Set up the event channel, RECORD the session, then set up the data channel used to carry MRP.</summary>
	/// <param name="cancellationToken">A token used to cancel the exchanges.</param>
	// pyatv/protocols/airplay/ap2_session.py (setup_remote_control) — line 70-77 as of pyatv 0.18.0
	public async Task SetupRemoteControlAsync (CancellationToken cancellationToken = default)
		{
		if (_connection is null || _rtsp is null || _verifier is null)
			{
			throw new InvalidOperationException ("not connected to remote");
			}

		await SetupEventChannelAsync (_connection.RemoteIp, cancellationToken).ConfigureAwait (false);
		await _rtsp.RecordAsync ().ConfigureAwait (false);
		await SetupDataChannelAsync (_connection.RemoteIp, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Start sending periodic RTSP <c>/feedback</c> keep-alive requests on the control connection.
	/// </summary>
	/// <remarks>
	/// Without this, the Apple TV closes the control (and therefore data-stream) connection a few
	/// seconds after RECORD completes. Must be called once <see cref="SetupRemoteControlAsync"/>
	/// has completed.
	/// </remarks>
	// pyatv/protocols/airplay/ap2_session.py (start_keep_alive) — line 82-105 as of pyatv 0.18.0
	public void StartKeepAlive ()
		{
		if (_rtsp is null)
			{
			throw new InvalidOperationException ("not connected to remote");
			}

		_feedbackCts = new CancellationTokenSource ();
		_feedbackTask = FeedbackLoopAsync (_feedbackCts.Token);
		}

	// pyatv/core/protocol.py (heartbeater) — line 35-75 as of pyatv 0.18.0, specialized for the
	// AirPlay feedback sender (retries=HEARTBEAT_RETRIES=1, interval=FEEDBACK_INTERVAL=2.0s).
	private async Task FeedbackLoopAsync (CancellationToken cancellationToken)
		{
		int attempts = 0;
		try
			{
			while (true)
				{
				try
					{
					await Task.Delay (FeedbackInterval, cancellationToken).ConfigureAwait (false);
					await _rtsp!.FeedbackAsync ().ConfigureAwait (false);
					attempts = 0;
					}
				catch (OperationCanceledException)
					{
					return;
					}
				catch (Exception ex)
					{
					attempts++;
					// pyatv/core/protocol.py — line 41 as of pyatv 0.18.0: HEARTBEAT_RETRIES = 1 (one
					// regular attempt plus one retry) before treating this as a failure.
					if (attempts > 1)
						{
						KeepAliveFailed?.Invoke (ex);
						return;
						}
					}
				}
			}
		finally
			{
			if (!cancellationToken.IsCancellationRequested)
				{
				KeepAliveFinished?.Invoke ();
				}
			}
		}

	// pyatv/protocols/airplay/ap2_session.py (_setup) — line 118-121 as of pyatv 0.18.0
	private async Task<NSDictionary> SetupAsync (NSDictionary body)
		{
		HttpResponse resp = await _rtsp!.SetupAsync (body).ConfigureAwait (false);
		return PlistBody.Decode (resp.Body);
		}

	// pyatv/protocols/airplay/ap2_session.py (_setup_event_channel) — line 123-149 as of pyatv 0.18.0
	private async Task SetupEventChannelAsync (string address, CancellationToken cancellationToken)
		{
		var body = new NSDictionary ();
		body.Add ("isRemoteControlOnly", true);
		body.Add ("osName", _info.OsName);
		body.Add ("sourceVersion", "550.10");
		body.Add ("timingProtocol", "None");
		body.Add ("model", _info.Model);
		body.Add ("deviceID", _info.DeviceId);
		body.Add ("osVersion", _info.OsVersion);
		body.Add ("osBuildVersion", _info.OsBuild);
		body.Add ("macAddress", _info.Mac);
		body.Add ("sessionUUID", Guid.NewGuid ().ToString ().ToUpperInvariant ());
		body.Add ("name", _info.Name);

		NSDictionary resp = await SetupAsync (body).ConfigureAwait (false);
		int eventPort = ((NSNumber)resp["eventPort"]).ToInt ();

		// pyatv/protocols/airplay/ap2_session.py — line 143-144 as of pyatv 0.18.0: read/write info
		// reversed here as the connection originates from the receiver.
		(byte[] outputKey, byte[] inputKey) = _verifier!.EncryptionKeys (EventsSalt, EventsReadInfo, EventsWriteInfo);
		var eventChannel = new EventChannel (outputKey, inputKey);
		await eventChannel.ConnectAsync (address, eventPort, cancellationToken).ConfigureAwait (false);
		_eventChannel = eventChannel;
		}

	// pyatv/protocols/airplay/ap2_session.py (_setup_data_channel) — line 151-179 as of pyatv 0.18.0
	private async Task SetupDataChannelAsync (string address, CancellationToken cancellationToken)
		{
		// A 64 bit random seed is included and used as part of the salt in encryption.
		var seedBytes = new byte[8];
		_random.NextBytes (seedBytes);
		ulong seed = BitConverter.ToUInt64 (seedBytes, 0);

		var stream = new NSDictionary ();
		stream.Add ("controlType", 2);
		stream.Add ("channelID", Guid.NewGuid ().ToString ().ToUpperInvariant ());
		stream.Add ("seed", (long)seed);
		stream.Add ("clientUUID", Guid.NewGuid ().ToString ().ToUpperInvariant ());
		stream.Add ("type", 130);
		stream.Add ("wantsDedicatedSocket", true);
		stream.Add ("clientTypeUUID", "1910A70F-DBC0-4242-AF95-115DB30604E1");

		var streams = new NSArray (1);
		streams.Add (stream);

		var body = new NSDictionary ();
		body.Add ("streams", streams);

		NSDictionary resp = await SetupAsync (body).ConfigureAwait (false);
		var respStreams = (NSArray)resp["streams"];
		var respStream = (NSDictionary)respStreams[0];
		int dataPort = ((NSNumber)respStream["dataPort"]).ToInt ();

		(byte[] outputKey, byte[] inputKey) = _verifier!.EncryptionKeys (DataStreamSalt + seed, DataStreamOutputInfo, DataStreamInputInfo);
		var dataChannel = new DataStreamChannel (outputKey, inputKey);
		await dataChannel.ConnectAsync (address, dataPort, cancellationToken).ConfigureAwait (false);
		DataChannel = dataChannel;
		}

	/// <summary>Close all open connections.</summary>
	// pyatv/protocols/airplay/ap2_session.py (stop) — line 181-201 as of pyatv 0.18.0
	public void Dispose ()
		{
		_feedbackCts?.Cancel ();
		_feedbackCts?.Dispose ();
		_feedbackCts = null;
		_feedbackTask = null;

		DataChannel?.Dispose ();
		_eventChannel?.Dispose ();
		_connection?.Dispose ();
		}
	}
