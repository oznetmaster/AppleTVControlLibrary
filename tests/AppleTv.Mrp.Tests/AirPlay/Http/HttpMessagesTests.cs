// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTvControlLibrary.Mrp.Tests.AirPlay.Http;

/// <summary>
/// Tests for <see cref="HttpMessages"/> request/response formatting and parsing used by the
/// AirPlay 2 control connection.
/// </summary>
// pyatv/support/http.py — line 1-236 as of pyatv 0.18.0
[TestClass]
public class HttpMessagesTests
	{
	[TestMethod]
	public void FormatMessageWithoutBodyOmitsContentLength ()
		{
		byte[] encoded = HttpMessages.FormatMessage ("GET", "/info", protocol: "HTTP/1.1", userAgent: "TestAgent");
		string text = Encoding.UTF8.GetString (encoded);

		Assert.IsTrue (text.StartsWith ("GET /info HTTP/1.1\r\n", StringComparison.Ordinal));
		Assert.IsTrue (text.Contains ("User-Agent: TestAgent"));
		Assert.IsFalse (text.Contains ("Content-Length"));
		Assert.IsTrue (text.EndsWith ("\r\n\r\n", StringComparison.Ordinal));
		}

	[TestMethod]
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

		Assert.IsTrue (text.Contains ("Content-Type: application/octet-stream"));
		Assert.IsTrue (text.Contains ($"Content-Length: {body.Length}"));
		Assert.IsTrue (text.EndsWith ("hello", StringComparison.Ordinal));
		}

	[TestMethod]
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
		Assert.AreEqual (firstIndex, lastIndex);
		Assert.IsTrue (text.Contains ("User-Agent: Explicit"));
		}

	[TestMethod]
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

		Assert.IsTrue (parsed);
		Assert.IsNotNull (response);
		Assert.AreEqual ("RTSP", response!.Protocol);
		Assert.AreEqual ("1.0", response.Version);
		Assert.AreEqual (200, response.Code);
		Assert.AreEqual ("OK", response.Message);
		Assert.AreEqual ("1", response.Headers["CSeq"]);
		CollectionAssert.AreEqual (body, response.Body);
		Assert.AreEqual (0, rest.Length);
		}

	[TestMethod]
	public void TryParseResponseReturnsFalseWhenBodyIncomplete ()
		{
		string message =
			"HTTP/1.1 200 OK\r\n" +
			"Content-Length: 10\r\n" +
			"\r\n" +
			"short";
		byte[] data = Encoding.UTF8.GetBytes (message);

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.IsFalse (parsed);
		Assert.IsNull (response);
		CollectionAssert.AreEqual (data, rest);
		}

	[TestMethod]
	public void TryParseResponseReturnsFalseWhenHeadersIncomplete ()
		{
		byte[] data = Encoding.UTF8.GetBytes ("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n");

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.IsFalse (parsed);
		Assert.IsNull (response);
		CollectionAssert.AreEqual (data, rest);
		}

	[TestMethod]
	public void TryParseResponseLeavesTrailingBytesInRest ()
		{
		string message = "HTTP/1.1 200 OK\r\n\r\n";
		string trailing = "RTSP/1.0 200 OK\r\n\r\n";
		byte[] data = Encoding.UTF8.GetBytes (message + trailing);

		bool parsed = HttpMessages.TryParseResponse (data, out HttpResponse? response, out byte[] rest);

		Assert.IsTrue (parsed);
		Assert.IsNotNull (response);
		CollectionAssert.AreEqual (Encoding.UTF8.GetBytes (trailing), rest);
		}

	[TestMethod]
	public void TryParseRequestParsesMethodPathProtocolAndHeaders ()
		{
		string message =
			"SETUP /rc RTSP/1.0\r\n" +
			"CSeq: 3\r\n" +
			"\r\n";
		byte[] data = Encoding.UTF8.GetBytes (message);

		bool parsed = HttpMessages.TryParseRequest (data, out HttpRequest? request, out byte[] rest);

		Assert.IsTrue (parsed);
		Assert.IsNotNull (request);
		Assert.AreEqual ("SETUP", request!.Method);
		Assert.AreEqual ("/rc", request.Path);
		Assert.AreEqual ("RTSP", request.Protocol);
		Assert.AreEqual ("1.0", request.Version);
		Assert.AreEqual ("3", request.Headers["CSeq"]);
		Assert.AreEqual (0, request.Body.Length);
		Assert.AreEqual (0, rest.Length);
		}

	[TestMethod]
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

		Assert.IsTrue (parsed);
		Assert.IsNotNull (request);
		Assert.AreEqual (original.Method, request!.Method);
		Assert.AreEqual (original.Path, request.Path);
		Assert.AreEqual (original.Protocol, request.Protocol);
		Assert.AreEqual (original.Version, request.Version);
		Assert.AreEqual ("9", request.Headers["CSeq"]);
		CollectionAssert.AreEqual (body, request.Body);
		Assert.AreEqual (0, rest.Length);
		}

	[TestMethod]
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

		Assert.IsTrue (parsed);
		Assert.IsNotNull (response);
		Assert.AreEqual (original.Code, response!.Code);
		Assert.AreEqual (original.Message, response.Message);
		Assert.AreEqual ("4", response.Headers["CSeq"]);
		CollectionAssert.AreEqual (body, response.Body);
		Assert.AreEqual (0, rest.Length);
		}
	}
