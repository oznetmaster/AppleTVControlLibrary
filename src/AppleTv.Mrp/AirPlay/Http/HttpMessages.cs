// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AppleTvControlLibrary.Mrp.AirPlay.Http;

/// <summary>
/// A generic HTTP response message, as used by the RTSP-over-HTTP control connection of an
/// AirPlay 2 session.
/// </summary>
// pyatv/support/http.py (HttpResponse) — line 79-87 as of pyatv 0.18.0
public sealed class HttpResponse
	{
	/// <summary>Initializes a new instance of the <see cref="HttpResponse"/> class.</summary>
	public HttpResponse (string protocol, string version, int code, string message, IReadOnlyDictionary<string, string> headers, byte[] body)
		{
		Protocol = protocol;
		Version = version;
		Code = code;
		Message = message;
		Headers = headers;
		Body = body;
		}

	/// <summary>Gets the protocol name (e.g. "HTTP" or "RTSP").</summary>
	public string Protocol { get; }

	/// <summary>Gets the protocol version (e.g. "1.1" or "1.0").</summary>
	public string Version { get; }

	/// <summary>Gets the numeric status code.</summary>
	public int Code { get; }

	/// <summary>Gets the status message.</summary>
	public string Message { get; }

	/// <summary>Gets the response headers, keyed case-insensitively.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; }

	/// <summary>Gets the response body.</summary>
	public byte[] Body { get; }
	}

/// <summary>
/// A generic HTTP request message, as used by the RTSP-over-HTTP control connection of an
/// AirPlay 2 session.
/// </summary>
// pyatv/support/http.py (HttpRequest) — line 90-97 as of pyatv 0.18.0
public sealed class HttpRequest
	{
	/// <summary>Initializes a new instance of the <see cref="HttpRequest"/> class.</summary>
	public HttpRequest (string method, string path, string protocol, string version, IReadOnlyDictionary<string, string> headers, byte[] body)
		{
		Method = method;
		Path = path;
		Protocol = protocol;
		Version = version;
		Headers = headers;
		Body = body;
		}

	/// <summary>Gets the request method (e.g. "GET", "SETUP").</summary>
	public string Method { get; }

	/// <summary>Gets the request path/URI.</summary>
	public string Path { get; }

	/// <summary>Gets the protocol name (e.g. "HTTP" or "RTSP").</summary>
	public string Protocol { get; }

	/// <summary>Gets the protocol version (e.g. "1.1" or "1.0").</summary>
	public string Version { get; }

	/// <summary>Gets the request headers, keyed case-insensitively.</summary>
	public IReadOnlyDictionary<string, string> Headers { get; }

	/// <summary>Gets the request body.</summary>
	public byte[] Body { get; }
	}

/// <summary>
/// Formatting and parsing of the generic HTTP/RTSP request and response messages used by the
/// AirPlay control connection.
/// </summary>
// pyatv/support/http.py — line 1-236 as of pyatv 0.18.0
public static class HttpMessages
	{
	// pyatv/support/http.py (USER_AGENT) — line 29 as of pyatv 0.18.0; this port uses the
	// caller-supplied user agent explicitly rather than deriving it from a library version.
	private const string DefaultUserAgent = "AppleTvControlLibrary";

	private static readonly Regex ResponseFirstLine = new (@"^([^/]+)/([0-9.]+) ([0-9]+) (.*)$", RegexOptions.Compiled);
	private static readonly Regex RequestFirstLine = new (@"^([A-Z_]+) ([^ ]+) ([^/]+)/([0-9.]+)$", RegexOptions.Compiled);

	/// <summary>Encode an outgoing HTTP/RTSP request message.</summary>
	/// <param name="method">The request method.</param>
	/// <param name="uri">The request URI/path.</param>
	/// <param name="protocol">The protocol/version string, e.g. "HTTP/1.1" or "RTSP/1.0".</param>
	/// <param name="userAgent">The value of the User-Agent header.</param>
	/// <param name="contentType">An optional Content-Type header value.</param>
	/// <param name="headers">Additional headers to include.</param>
	/// <param name="body">An optional request body.</param>
	/// <returns>The encoded request bytes.</returns>
	// pyatv/support/http.py (_format_message) — line 47-70 as of pyatv 0.18.0
	public static byte[] FormatMessage (
		string method,
		string uri,
		string protocol = "HTTP/1.1",
		string userAgent = DefaultUserAgent,
		string? contentType = null,
		IReadOnlyDictionary<string, string>? headers = null,
		byte[]? body = null)
		{
		headers ??= new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);

		var msg = new StringBuilder ();
		_ = msg.Append (method).Append (' ').Append (uri).Append (' ').Append (protocol);

		if (!ContainsHeader (headers, "User-Agent"))
			{
			_ = msg.Append ("\r\nUser-Agent: ").Append (userAgent);
			}

		if (contentType is not null)
			{
			_ = msg.Append ("\r\nContent-Type: ").Append (contentType);
			}

		if (body is { Length: > 0 })
			{
			_ = msg.Append ("\r\nContent-Length: ").Append (body.Length);
			}

		foreach (KeyValuePair<string, string> header in headers)
			{
			_ = msg.Append ('\r').Append ('\n').Append (header.Key).Append (": ").Append (header.Value);
			}

		_ = msg.Append ("\r\n\r\n");

		byte[] output = Encoding.UTF8.GetBytes (msg.ToString ());
		if (body is { Length: > 0 })
			{
			var result = new byte[output.Length + body.Length];
			Buffer.BlockCopy (output, 0, result, 0, output.Length);
			Buffer.BlockCopy (body, 0, result, output.Length, body.Length);
			return result;
			}

		return output;
		}

	/// <summary>Encode a <see cref="HttpRequest"/> back into its wire representation.</summary>
	/// <param name="request">The request to encode.</param>
	/// <returns>The encoded request bytes.</returns>
	// pyatv/support/http.py (format_request) — line 176-183 as of pyatv 0.18.0
	public static byte[] FormatRequest (HttpRequest request)
		{
		return FormatMessage (
			request.Method,
			request.Path,
			protocol: $"{request.Protocol}/{request.Version}",
			headers: request.Headers,
			body: request.Body);
		}

	/// <summary>Encode a <see cref="HttpResponse"/> into its wire representation.</summary>
	/// <param name="response">The response to encode.</param>
	/// <param name="serverName">The value used for the Server header when not already present.</param>
	/// <returns>The encoded response bytes.</returns>
	// pyatv/support/http.py (format_response) — line 138-160 as of pyatv 0.18.0
	public static byte[] FormatResponse (HttpResponse response, string serverName = DefaultUserAgent)
		{
		var msg = new StringBuilder ();
		_ = msg.Append (response.Protocol).Append ('/').Append (response.Version).Append (' ')
			.Append (response.Code).Append (' ').Append (response.Message).Append ("\r\n");

		if (!ContainsHeader (response.Headers, "Server"))
			{
			_ = msg.Append ("Server: ").Append (serverName).Append ("\r\n");
			}

		foreach (KeyValuePair<string, string> header in response.Headers)
			{
			_ = msg.Append (header.Key).Append (": ").Append (header.Value).Append ("\r\n");
			}

		byte[] body = response.Body;
		if (body.Length > 0)
			{
			_ = msg.Append ("Content-Length: ").Append (body.Length).Append ("\r\n");
			}

		byte[] output = Encoding.UTF8.GetBytes (msg.ToString ());
		var result = new byte[output.Length + 2 + body.Length];
		Buffer.BlockCopy (output, 0, result, 0, output.Length);
		result[output.Length] = (byte)'\r';
		result[output.Length + 1] = (byte)'\n';
		Buffer.BlockCopy (body, 0, result, output.Length + 2, body.Length);
		return result;
		}

	/// <summary>Parse a buffer that may contain a complete HTTP/RTSP response.</summary>
	/// <param name="data">The buffer to parse.</param>
	/// <param name="response">The parsed response, if a complete one was found.</param>
	/// <param name="rest">The unconsumed remainder of <paramref name="data"/>.</param>
	/// <returns><see langword="true"/> if a complete response was parsed.</returns>
	// pyatv/support/http.py (parse_response) — line 163-174 as of pyatv 0.18.0
	public static bool TryParseResponse (byte[] data, out HttpResponse? response, out byte[] rest)
		{
		if (!TryParseHttpMessage (data, out string? firstLine, out Dictionary<string, string>? headers, out byte[]? body, out rest))
			{
			response = null;
			return false;
			}

		Match match = ResponseFirstLine.Match (firstLine!);
		if (!match.Success)
			{
			throw new ArgumentException ($"bad first line: {firstLine}");
			}

		response = new HttpResponse (
			match.Groups[1].Value,
			match.Groups[2].Value,
			int.Parse (match.Groups[3].Value, CultureInfo.InvariantCulture),
			match.Groups[4].Value,
			headers!,
			body!);
		return true;
		}

	/// <summary>Parse a buffer that may contain a complete HTTP/RTSP request.</summary>
	/// <param name="data">The buffer to parse.</param>
	/// <param name="request">The parsed request, if a complete one was found.</param>
	/// <param name="rest">The unconsumed remainder of <paramref name="data"/>.</param>
	/// <returns><see langword="true"/> if a complete request was parsed.</returns>
	// pyatv/support/http.py (parse_request) — line 186-204 as of pyatv 0.18.0
	public static bool TryParseRequest (byte[] data, out HttpRequest? request, out byte[] rest)
		{
		if (!TryParseHttpMessage (data, out string? firstLine, out Dictionary<string, string>? headers, out byte[]? body, out rest))
			{
			request = null;
			return false;
			}

		Match match = RequestFirstLine.Match (firstLine!);
		if (!match.Success)
			{
			throw new ArgumentException ($"bad first line: {firstLine}");
			}

		request = new HttpRequest (
			match.Groups[1].Value,
			match.Groups[2].Value,
			match.Groups[3].Value,
			match.Groups[4].Value,
			headers!,
			body!);
		return true;
		}

	// pyatv/support/http.py (_parse_http_message) — line 104-128 as of pyatv 0.18.0
	private static bool TryParseHttpMessage (byte[] message, out string? firstLine, out Dictionary<string, string>? headers, out byte[]? body, out byte[] rest)
		{
		firstLine = null;
		headers = null;
		body = null;
		rest = message;

		int separator = IndexOfCrLfCrLf (message);
		if (separator < 0)
			{
			return false;
			}

		string headerStr = Encoding.UTF8.GetString (message, 0, separator);
		byte[] afterHeaders = Slice (message, separator + 4, message.Length - (separator + 4));

		var msgHeaders = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		ReadOnlySpan<char> headerSpan = headerStr.AsSpan ();
		ReadOnlySpan<char> firstLineSpan = default;
		bool isFirstLine = true;
		while (headerSpan.Length > 0)
			{
			int lineBreak = headerSpan.IndexOf ("\r\n", StringComparison.Ordinal);
			ReadOnlySpan<char> line = lineBreak < 0 ? headerSpan : headerSpan[..lineBreak];
			headerSpan = lineBreak < 0 ? default : headerSpan[(lineBreak + 2)..];

			if (isFirstLine)
				{
				firstLineSpan = line;
				isFirstLine = false;
				}
			else if (line.Length > 0)
				{
				int colon = line.IndexOf (": ", StringComparison.Ordinal);
				if (colon >= 0)
					{
					msgHeaders[line[..colon].ToString ()] = line[(colon + 2)..].ToString ();
					}
				}

			if (lineBreak < 0)
				{
				break;
				}
			}

		int contentLength = 0;
		if (msgHeaders.TryGetValue ("Content-Length", out string? lengthText)
			&& !int.TryParse (lengthText, NumberStyles.Integer, CultureInfo.InvariantCulture, out contentLength))
			{
			contentLength = 0;
			}

		if (afterHeaders.Length < contentLength)
			{
			return false;
			}

		firstLine = firstLineSpan.ToString ();
		headers = msgHeaders;
		body = Slice (afterHeaders, 0, contentLength);
		rest = Slice (afterHeaders, contentLength, afterHeaders.Length - contentLength);
		return true;
		}

	private static bool ContainsHeader (IReadOnlyDictionary<string, string> headers, string name)
		{
		foreach (string key in headers.Keys)
			{
			if (string.Equals (key, name, StringComparison.OrdinalIgnoreCase))
				{
				return true;
				}
			}

		return false;
		}

	private static int IndexOfCrLfCrLf (byte[] data)
		{
		return data.AsSpan ().IndexOf ("\r\n\r\n"u8);
		}

	private static byte[] Slice (byte[] data, int start, int length)
		{
		if (length <= 0)
			{
			return [];
			}

		return data.AsSpan (start, length).ToArray ();
		}
	}
