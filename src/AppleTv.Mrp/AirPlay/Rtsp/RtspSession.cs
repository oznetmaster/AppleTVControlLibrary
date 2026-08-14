// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

namespace AppleTvControlLibrary.Mrp.AirPlay.Rtsp;

/// <summary>
/// Implementation of the RTSP protocol subset used by AirPlay 2 remote-control sessions
/// (setting up event/data channels for tunneling MRP). Audio-streaming specific members of
/// pyatv's <c>RtspSession</c> (ANNOUNCE, RECORD's SDP body, digest auth, metadata/artwork) are
/// intentionally not ported since MRP-over-AirPlay only needs SETUP + feedback.
/// </summary>
// pyatv/support/rtsp.py (RtspSession) — line 68-330 as of pyatv 0.18.0
public sealed class RtspSession
	{
	// pyatv/support/rtsp.py (USER_AGENT) — line 18 as of pyatv 0.18.0
	private const string UserAgent = "AirPlay/550.10";

	private readonly HttpConnection _connection;
	private readonly Random _random = new ();
	private int _cseq;

	/// <summary>Initializes a new instance of the <see cref="RtspSession"/> class.</summary>
	/// <param name="connection">The (already pair-verified and, if applicable, encrypted) control connection.</param>
	// pyatv/support/rtsp.py (__init__) — line 71-79 as of pyatv 0.18.0
	public RtspSession (HttpConnection connection)
		{
		_connection = connection;

		// pyatv/support/rtsp.py — line 76-78 as of pyatv 0.18.0
		SessionId = (uint)_random.Next (int.MinValue, int.MaxValue);
		DacpId = NextUInt64 (_random).ToString ("X", CultureInfo.InvariantCulture);
		ActiveRemote = (uint)_random.Next (int.MinValue, int.MaxValue);
		}

	/// <summary>Gets the RTSP session identifier used to build <see cref="Uri"/>.</summary>
	public uint SessionId { get; }

	/// <summary>Gets the DACP-ID header value used for every exchange.</summary>
	public string DacpId { get; }

	/// <summary>Gets the Active-Remote header value used for every exchange.</summary>
	public uint ActiveRemote { get; }

	/// <summary>Gets the URI used for session requests.</summary>
	// pyatv/support/rtsp.py (uri) — line 81-84 as of pyatv 0.18.0
	public string Uri => $"rtsp://{_connection.LocalIp}/{SessionId}";

	/// <summary>Send a SETUP message.</summary>
	/// <param name="body">The plist request body.</param>
	/// <returns>The response.</returns>
	// pyatv/support/rtsp.py (setup) — line 169-175 as of pyatv 0.18.0
	public Task<HttpResponse> SetupAsync (Claunia.PropertyList.NSDictionary body) =>
		ExchangeAsync ("SETUP", body: body);

	/// <summary>Send a RECORD message.</summary>
	/// <returns>The response.</returns>
	// pyatv/support/rtsp.py (record) — line 177-183 as of pyatv 0.18.0
	public Task<HttpResponse> RecordAsync () => ExchangeAsync ("RECORD");

	/// <summary>Send a feedback (keep-alive) request.</summary>
	/// <param name="allowError">If <see langword="true"/>, a non-2xx response is returned rather than throwing.</param>
	/// <returns>The response.</returns>
	// pyatv/support/rtsp.py (feedback) — line 240-242 as of pyatv 0.18.0
	public Task<HttpResponse> FeedbackAsync (bool allowError = false) =>
		ExchangeAsync ("POST", uri: "/feedback", allowError: allowError);

	/// <summary>Send an RTSP/HTTP request with the standard AirPlay control headers and correlate the response by CSeq.</summary>
	/// <param name="method">The request method.</param>
	/// <param name="uri">An optional explicit request URI; defaults to <see cref="Uri"/>.</param>
	/// <param name="body">An optional plist request body.</param>
	/// <param name="allowError">If <see langword="true"/>, a non-2xx response is returned rather than throwing.</param>
	/// <param name="protocol">The protocol/version string.</param>
	/// <param name="cancellationToken">A token used to cancel the exchange.</param>
	/// <returns>The response.</returns>
	// pyatv/support/rtsp.py (exchange) — line 244-311 as of pyatv 0.18.0
	public async Task<HttpResponse> ExchangeAsync (
		string method,
		string? uri = null,
		Claunia.PropertyList.NSDictionary? body = null,
		bool allowError = false,
		string protocol = "RTSP/1.0",
		CancellationToken cancellationToken = default)
		{
		int cseq = _cseq;
		_cseq += 1;

		var headers = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase)
			{
			["CSeq"] = cseq.ToString (System.Globalization.CultureInfo.InvariantCulture),
			["DACP-ID"] = DacpId,
			["Active-Remote"] = ActiveRemote.ToString (System.Globalization.CultureInfo.InvariantCulture),
			["Client-Instance"] = DacpId,
			};

		byte[]? encodedBody = null;
		string? contentType = null;
		if (body is not null)
			{
			// pyatv/support/rtsp.py — line 275-279 as of pyatv 0.18.0
			contentType = "application/x-apple-binary-plist";
			encodedBody = PlistBody.Encode (body);
			}

		HttpResponse resp = await _connection.SendAndReceiveAsync (
			method,
			uri ?? Uri,
			protocol: protocol,
			userAgent: UserAgent,
			contentType: contentType,
			headers: headers,
			body: encodedBody,
			allowError: allowError,
			cancellationToken: cancellationToken).ConfigureAwait (false);

		// pyatv/support/rtsp.py — line 296-306 as of pyatv 0.18.0: pyatv correlates responses by CSeq
		// against a table of outstanding requests, since the response to a different in-flight CSeq
		// can arrive first. This port only ever has one exchange in flight at a time (each call awaits
		// its own response before the next is issued), so the underlying HttpConnection's own
		// request/response correlation already provides the same guarantee without needing a
		// separate CSeq-keyed wait table.
		return resp;
		}

	private static ulong NextUInt64 (Random random)
		{
		var bytes = new byte[8];
		random.NextBytes (bytes);
		return BitConverter.ToUInt64 (bytes, 0);
		}
	}
