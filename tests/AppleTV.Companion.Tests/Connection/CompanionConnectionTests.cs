// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Text;

using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Crypto;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Connection;

/// <summary>
/// Tests for <see cref="CompanionConnection"/> framing and encryption.
/// </summary>
/// <remarks>
/// pyatv has no isolated unit test for connection.py (only functional tests that require
/// the fake device from WP5); these tests instead validate directly against the cited
/// behavior in pyatv/protocols/companion/connection.py, byte for byte.
/// </remarks>
[TestClass]
public class CompanionConnectionTests
	{
	private static readonly byte[] FakeOutKey = Encoding.ASCII.GetBytes (new string ('o', 32));
	private static readonly byte[] FakeInKey = Encoding.ASCII.GetBytes (new string ('i', 32));

	// pyatv/protocols/companion/connection.py (1 byte type + 3 byte big-endian length) — line 106 as of pyatv 0.18.0
	[TestMethod]
	public void BuildFrameUnencryptedHeader ()
		{
		var connection = new CompanionConnection ();
		var data = Encoding.ASCII.GetBytes ("hello");

		var frame = connection.BuildFrame (FrameType.U_OPACK, data);

		Assert.HasCount (4 + data.Length, frame);
		Assert.AreEqual ((byte)FrameType.U_OPACK, frame[0]);
		Assert.AreEqual (0x00, frame[1]);
		Assert.AreEqual (0x00, frame[2]);
		Assert.AreEqual ((byte)data.Length, frame[3]);
		}

	// pyatv/protocols/companion/connection.py (payload_length += AUTH_TAG_LENGTH
	// when encryption is active) — line 103-105 as of pyatv 0.18.0
	[TestMethod]
	public void BuildFrameEncryptedHeaderIncludesAuthTag ()
		{
		var connection = new CompanionConnection ();
		connection.EnableEncryption (FakeOutKey, FakeInKey);
		var data = Encoding.ASCII.GetBytes ("hello");

		var frame = connection.BuildFrame (FrameType.E_OPACK, data);

		var expectedPayloadLength = data.Length + 16;
		Assert.HasCount (4 + expectedPayloadLength, frame);
		Assert.AreEqual (0x00, frame[1]);
		Assert.AreEqual (0x00, frame[2]);
		Assert.AreEqual ((byte)expectedPayloadLength, frame[3]);
		}

	// pyatv/protocols/companion/connection.py — line 104 as of pyatv 0.18.0, 115 (zero-length payloads are never
	// encrypted, even after encryption is enabled)
	[TestMethod]
	public void BuildFrameZeroLengthPayloadNeverEncrypted ()
		{
		var connection = new CompanionConnection ();
		connection.EnableEncryption (FakeOutKey, FakeInKey);

		var frame = connection.BuildFrame (FrameType.NoOp, System.Array.Empty<byte> ());

		Assert.HasCount (4, frame);
		Assert.AreEqual ((byte)FrameType.NoOp, frame[0]);
		Assert.AreEqual (0x00, frame[1]);
		Assert.AreEqual (0x00, frame[2]);
		Assert.AreEqual (0x00, frame[3]);
		}

	// pyatv/protocols/companion/connection.py — line 98-153 as of pyatv 0.18.0 round trip: what one side builds,
	// the other side (with output/input keys swapped) must be able to receive.
	[TestMethod]
	public void FramingRoundTripsUnencrypted ()
		{
		var sender = new CompanionConnection ();
		var receiver = new CompanionConnection ();

		var data = Encoding.UTF8.GetBytes ("_systemInfo payload");
		var frame = sender.BuildFrame (FrameType.U_OPACK, data);

		FrameType? receivedType = null;
		byte[]? receivedData = null;
		receiver.FrameReceived += (_, frameType, payload) =>
			{
				receivedType = frameType;
				receivedData = payload;
			};

		receiver.ReceiveData (frame);

		Assert.AreEqual (FrameType.U_OPACK, receivedType);
		CollectionAssert.AreEqual (data, receivedData);
		}

	// Encrypted round trip: the sender's output key must be the receiver's input key
	// and vice versa, matching how pyatv/auth/hap_srp.py + protocol.py:40-42 derive
	// independent client/server key pairs.
	[TestMethod]
	public void FramingRoundTripsEncrypted ()
		{
		var sender = new CompanionConnection ();
		sender.EnableEncryption (outputKey: FakeOutKey, inputKey: FakeInKey);

		var receiver = new CompanionConnection ();
		receiver.EnableEncryption (outputKey: FakeInKey, inputKey: FakeOutKey);

		var data = Encoding.UTF8.GetBytes ("encrypted payload");
		var frame = sender.BuildFrame (FrameType.E_OPACK, data);

		FrameType? receivedType = null;
		byte[]? receivedData = null;
		receiver.FrameReceived += (_, frameType, payload) =>
			{
				receivedType = frameType;
				receivedData = payload;
			};

		receiver.ReceiveData (frame);

		Assert.AreEqual (FrameType.E_OPACK, receivedType);
		CollectionAssert.AreEqual (data, receivedData);
		}

	// pyatv/protocols/companion/connection.py (require 4 + big-endian length
	// bytes before a frame is considered complete; partial frames must be buffered) — line 135-141 as of pyatv 0.18.0
	[TestMethod]
	public void ReceiveDataBuffersPartialFrames ()
		{
		var receiver = new CompanionConnection ();
		var sender = new CompanionConnection ();

		var data = Encoding.UTF8.GetBytes ("partial delivery test");
		var frame = sender.BuildFrame (FrameType.U_OPACK, data);

		var split = frame.Length / 2;
		var firstHalf = new byte[split];
		var secondHalf = new byte[frame.Length - split];
		System.Array.Copy (frame, 0, firstHalf, 0, split);
		System.Array.Copy (frame, split, secondHalf, 0, secondHalf.Length);

		var received = false;
		receiver.FrameReceived += (_, _, _) => received = true;

		receiver.ReceiveData (firstHalf);
		Assert.IsFalse (received);

		receiver.ReceiveData (secondHalf);
		Assert.IsTrue (received);
		}

	// Nonce counter must increment per direction so that decrypting a second frame
	// with a stale nonce fails; this is the "decrypt fails on second frame" failure
	// mode from the brief's known-silent-failure table (section 3).
	[TestMethod]
	public void EncryptedFramingIncrementsNonceAcrossMultipleFrames ()
		{
		var sender = new CompanionConnection ();
		sender.EnableEncryption (outputKey: FakeOutKey, inputKey: FakeInKey);

		var receiver = new CompanionConnection ();
		receiver.EnableEncryption (outputKey: FakeInKey, inputKey: FakeOutKey);

		var receivedPayloads = new System.Collections.Generic.List<byte[]> ();
		receiver.FrameReceived += (_, _, payload) => receivedPayloads.Add (payload);

		var first = sender.BuildFrame (FrameType.E_OPACK, Encoding.UTF8.GetBytes ("first"));
		var second = sender.BuildFrame (FrameType.E_OPACK, Encoding.UTF8.GetBytes ("second"));

		receiver.ReceiveData (first);
		receiver.ReceiveData (second);

		Assert.HasCount (2, receivedPayloads);
		CollectionAssert.AreEqual (Encoding.UTF8.GetBytes ("first"), receivedPayloads[0]);
		CollectionAssert.AreEqual (Encoding.UTF8.GetBytes ("second"), receivedPayloads[1]);
		}
	}