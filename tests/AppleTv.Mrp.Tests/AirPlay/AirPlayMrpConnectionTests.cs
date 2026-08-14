// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.AirPlay;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTvControlLibrary.Mrp.Tests.AirPlay;

/// <summary>
/// Tests for the parts of <see cref="AirPlayMrpConnection"/> that do not require a live
/// AirPlay session (an already-connected <see cref="Ap2Session"/> with a real TCP data
/// channel is needed for the rest, and is exercised instead by
/// tools/AppleTV.AirPlay.ScanTool against real hardware).
/// </summary>
// pyatv/protocols/airplay/mrp_connection.py (AirPlayMrpConnection) — line 16-75 as of pyatv 0.18.0
[TestClass]
public class AirPlayMrpConnectionTests
	{
	private static AirPlayMrpConnection CreateConnection ()
		{
		// The session is never connected in these tests; BuildMessage and EnableEncryption do not
		// touch the underlying data channel, so a session that has not run ConnectAsync is safe here.
		var session = new Ap2Session ("127.0.0.1", 0, new HapCredentials ());
		return new AirPlayMrpConnection (session);
		}

	[TestMethod]
	public void BuildMessageIsAnIdentityPassthrough ()
		{
		// pyatv/protocols/airplay/mrp_connection.py — line 43-45 as of pyatv 0.18.0: build_message
		// just returns the data unchanged, since actual framing happens in DataStreamChannel.send_protobuf.
		using AirPlayMrpConnection connection = CreateConnection ();
		byte[] data = [0x01, 0x02, 0x03, 0x04];

		byte[] result = connection.BuildMessage (data);

		CollectionAssert.AreEqual (data, result);
		Assert.AreSame (data, result);
		}

	[TestMethod]
	public void EnableEncryptionIsANoOp ()
		{
		// pyatv/protocols/airplay/mrp_connection.py — line 40-41 as of pyatv 0.18.0: enable_encryption
		// is a no-op because the AirPlay data channel is already HAP-encrypted end-to-end. This test
		// only documents/verifies that calling it does not throw when no session is connected.
		using AirPlayMrpConnection connection = CreateConnection ();
		byte[] outputKey = new byte[32];
		byte[] inputKey = new byte[32];

		connection.EnableEncryption (outputKey, inputKey);
		}

	[TestMethod]
	public void ConnectThrowsWhenDataChannelNotYetSetUp ()
		{
		// pyatv/protocols/airplay/mrp_connection.py (connect) — line 33-38 as of pyatv 0.18.0: connect()
		// requires SetupRemoteControlAsync to have already populated the session's data channel.
		using AirPlayMrpConnection connection = CreateConnection ();

		Assert.Throws<System.InvalidOperationException> (connection.Connect);
		}
	}

