// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

using NUnit.Framework;

namespace AppleTvControlLibrary.Mrp.Tests.AirPlay.Http;

/// <summary>
/// Tests for <see cref="HttpMessages"/> request/response formatting and parsing used by the
/// AirPlay 2 control connection.
/// </summary>
// pyatv/support/http.py — line 1-236 as of pyatv 0.18.0
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class HttpMessagesTests
	{
	[Test]
	public void FormatMessageWithoutBodyOmitsContentLength ()
		{
		byte[] encoded = HttpMessages.FormatMessage ("GET", "/info", protocol: "HTTP/1.1", userAgent: "TestAgent");
		string text = Encoding.UTF8.GetString (encoded);

		Assert.That (text.StartsWith ("GET /info HTTP/1.1\r\n", StringComparison.Ordinal), Is.True);
		Assert.That (text.Contains ("User-Agent: TestAgent"), Is.True);
		Assert.That (text.Contains ("Content-Length"), Is.False);
		Assert.That (text.EndsWith ("\r\n\r\n", StringComparison.Ordinal), Is.True);
		}

	[Test]
	public void FormatMessageWithBodyIncludesContentLengthAndAppendsBody ()
		{
		byte[] body = Encoding.UTF8.GetBytes ("hello");
		byte[] encoded = HttpMessages.FormatMessage (
			"POST",
			"/data",
			protocol: "RTSP/1.0",
			userAgent: "TestAgent",
			contentType: "application/octet-stream",
			body: body);
		string text = Encoding.UTF8.GetString (encoded);

		Assert.That (text.Contains ("Content-Type: application/octet-stream"), Is.True);
		Assert.That (text.Contains ($"Content-Length: {body.Length}"), Is.True);
		Assert.That (text.EndsWith ("hello", StringComparison.Ordinal), Is.True);
		}

	[Test]
	public void FormatMessageDoesNotDuplicateExplicitUserAgentHeader ()
		{
		var headers = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase)
			{
			["User-Agent"] = "Explicit",
			};

		byte[] encoded = HttpMessages.FormatMessage ("GET", "/info", headers: headers);
		string text = Encoding.UTF8.GetString (encoded);

		int firstIndex = text.IndexOf ("User-Agent:", StringComparison.Ordinal);
		int lastIndex = text.LastIndexOf ("User-Agent:", StringComparison.Ordinal);
		Assert.That (lastIndex, Is.EqualTo (firstIndex));
		Assert.That (text.Contains ("User-Agent: Explicit"), Is.True);
		}

	[Test]
	public void TryParseResponseParsesStatusLineHeadersAndBody ()
		{
		byte[] body = Encoding.UTF8.GetBytes ("payload");
		string message =
			"RTSP/1.0 200 OK\r\n" +
			"Content-Length: 7\r\n" +
			"CSeq: 1\r\n" +
			"\r\n" +
			"payload";
		byte[] data = Encoding.UTF8.GetBytes (message);

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.That (parsed, Is.True);
		Assert.That (response, Is.Not.Null);
		Assert.That (response!.Protocol, Is.EqualTo ("RTSP"));
		Assert.That (response.Version, Is.EqualTo ("1.0"));
		Assert.That (response.Code, Is.EqualTo (200));
		Assert.That (response.Message, Is.EqualTo ("OK"));
		Assert.That (response.Headers["CSeq"], Is.EqualTo ("1"));
		Assert.That (response.Body, Is.EqualTo (body));
		Assert.That (rest.Length, Is.EqualTo (0));
		}

	[Test]
	public void TryParseResponseReturnsFalseWhenBodyIncomplete ()
		{
		string message =
			"HTTP/1.1 200 OK\r\n" +
			"Content-Length: 10\r\n" +
			"\r\n" +
			"short";
		byte[] data = Encoding.UTF8.GetBytes (message);

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.That (parsed, Is.False);
		Assert.That (response, Is.Null);
		Assert.That (rest, Is.EqualTo (data));
		}

	[Test]
	public void TryParseResponseReturnsFalseWhenHeadersIncomplete ()
		{
		byte[] data = Encoding.UTF8.GetBytes ("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n");

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.That (parsed, Is.False);
		Assert.That (response, Is.Null);
		Assert.That (rest, Is.EqualTo (data));
		}

	[Test]
	public void TryParseResponseLeavesTrailingBytesInRest ()
		{
		string message = "HTTP/1.1 200 OK\r\n\r\n";
		string trailing = "RTSP/1.0 200 OK\r\n\r\n";
		byte[] data = Encoding.UTF8.GetBytes (message + trailing);

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.That (parsed, Is.True);
		Assert.That (response, Is.Not.Null);
		Assert.That (rest, Is.EqualTo (Encoding.UTF8.GetBytes (trailing)));
		}

	[Test]
	public void TryParseRequestParsesMethodPathProtocolAndHeaders ()
		{
		string message =
			"SETUP /rc RTSP/1.0\r\n" +
			"CSeq: 3\r\n" +
			"\r\n";
		byte[] data = Encoding.UTF8.GetBytes (message);

		bool parsed = HttpMessages.TryParseRequest (data, out HttpRequest? request, out byte[] rest);

		Assert.That (parsed, Is.True);
		Assert.That (request, Is.Not.Null);
		Assert.That (request!.Method, Is.EqualTo ("SETUP"));
		Assert.That (request.Path, Is.EqualTo ("/rc"));
		Assert.That (request.Protocol, Is.EqualTo ("RTSP"));
		Assert.That (request.Version, Is.EqualTo ("1.0"));
		Assert.That (request.Headers["CSeq"], Is.EqualTo ("3"));
		Assert.That (request.Body.Length, Is.EqualTo (0));
		Assert.That (rest.Length, Is.EqualTo (0));
		}

	[Test]
	public void FormatRequestAndTryParseRequestRoundTrip ()
		{
		var headers = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase)
			{
			["CSeq"] = "9",
			};
		byte[] body = Encoding.UTF8.GetBytes ("body-data");
		var original = new HttpRequest ("POST", "/data", "RTSP", "1.0", headers, body);

		byte[] encoded = HttpMessages.FormatRequest (original);
		bool parsed = HttpMessages.TryParseRequest (encoded, out HttpRequest? request, out byte[] rest);

		Assert.That (parsed, Is.True);
		Assert.That (request, Is.Not.Null);
		Assert.That (request!.Method, Is.EqualTo (original.Method));
		Assert.That (request.Path, Is.EqualTo (original.Path));
		Assert.That (request.Protocol, Is.EqualTo (original.Protocol));
		Assert.That (request.Version, Is.EqualTo (original.Version));
		Assert.That (request.Headers["CSeq"], Is.EqualTo ("9"));
		Assert.That (request.Body, Is.EqualTo (body));
		Assert.That (rest.Length, Is.EqualTo (0));
		}

	[Test]
	public void FormatResponseAndTryParseResponseRoundTrip ()
		{
		var headers = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase)
			{
			["CSeq"] = "4",
			};
		byte[] body = Encoding.UTF8.GetBytes ("response-body");
		var original = new HttpResponse ("RTSP", "1.0", 200, "OK", headers, body);

		byte[] encoded = HttpMessages.FormatResponse (original, serverName: "TestServer");
		bool parsed = HttpMessages.TryParseResponse (encoded, out HttpResponse? response, out byte[] rest);

		Assert.That (parsed, Is.True);
		Assert.That (response, Is.Not.Null);
		Assert.That (response!.Code, Is.EqualTo (original.Code));
		Assert.That (response.Message, Is.EqualTo (original.Message));
		Assert.That (response.Headers["CSeq"], Is.EqualTo ("4"));
		Assert.That (response.Body, Is.EqualTo (body));
		Assert.That (rest.Length, Is.EqualTo (0));
		}
	}