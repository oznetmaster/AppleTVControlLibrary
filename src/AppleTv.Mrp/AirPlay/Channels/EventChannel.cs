// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

namespace AppleTvControlLibrary.Mrp.AirPlay.Channels;

/// <summary>
/// The AirPlay 2 event channel. Only set up to satisfy the receiver's requirement that it
/// exists; MRP-over-AirPlay does not use it for anything beyond acknowledging requests.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="EventChannel"/> class.</remarks>
/// <param name="outputKey">The key used to encrypt outgoing data.</param>
/// <param name="inputKey">The key used to decrypt incoming data.</param>
// pyatv/protocols/airplay/channels.py (EventChannel, BaseEventChannel) — line 34-92 as of pyatv 0.18.0
public sealed class EventChannel (byte[] outputKey, byte[] inputKey) : AbstractHapChannel(outputKey, inputKey)
	{

	/// <summary>Handle received data that was put in the buffer.</summary>
	// pyatv/protocols/airplay/channels.py (EventChannel.handle_received) — line 62-92 as of pyatv 0.18.0
	protected override void HandleReceived ()
		{
		while (Buffer.Count > 0)
			{
			byte[] bufferArray = [.. Buffer];
			if (!HttpMessages.TryParseRequest (bufferArray, out HttpRequest? request, out byte[] rest))
				{
				break;
				}

			Buffer.Clear ();
			Buffer.AddRange (rest);

			// pyatv/protocols/airplay/channels.py — line 71-89 as of pyatv 0.18.0: send a positive
			// response to satisfy the other end of the channel.
			var headers = new Dictionary<string, string> (System.StringComparer.OrdinalIgnoreCase)
				{
				["Content-Length"] = "0",
				["Audio-Latency"] = "0",
				};

			if (request!.Headers.TryGetValue ("Server", out string? server))
				{
				headers["Server"] = server;
				}

			if (request.Headers.TryGetValue ("CSeq", out string? cseq))
				{
				headers["CSeq"] = cseq;
				}

			var response = new HttpResponse (request.Protocol, request.Version, 200, "OK", headers, []);
			Send (HttpMessages.FormatResponse (response));
			}
		}
	}
