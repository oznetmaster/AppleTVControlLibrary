// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Connection;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Protocol;

using Google.Protobuf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTv.Mrp.Tests.ProtocolTests;

/// <summary>
/// Unit tests for <see cref="MrpProtocol"/>'s power-state derivation, ported from pyatv's
/// <c>MrpPower._get_power_state</c>/<c>_update_power_state</c> behavior.
/// </summary>
// pyatv/protocols/mrp/__init__.py (MrpPower) — line 651-695 as of pyatv 0.18.0
[TestClass]
public class MrpProtocolPowerStateTests
	{
	/// <summary>A no-op <see cref="IMrpFrameConnection"/> that passes payloads through unmodified,
	/// sufficient to construct an <see cref="MrpProtocol"/> without any real transport.</summary>
	private sealed class PassthroughConnection : IMrpFrameConnection
		{
		public IMrpConnectionListener? Listener
			{
			get;
			set;
			}

		public void EnableEncryption (byte[] outputKey, byte[] inputKey)
			{
			}

		public byte[] BuildMessage (byte[] data) => data;
		}

	private static MrpProtocol CreateProtocol () =>
		new (new PassthroughConnection (), new SrpAuthHandler (), new MrpInfoSettings ());

	private static ProtocolMessage DeviceInfoMessage (
		ProtocolMessage.Types.Type type,
		uint? logicalDeviceCount)
		{
		var inner = new DeviceInfoMessage ();
		if (logicalDeviceCount is not null)
			{
			inner.LogicalDeviceCount = logicalDeviceCount.Value;
			}

		var envelope = new ProtocolMessage { Type = type };
		envelope.SetExtension (DeviceInfoMessageExtensions.DeviceInfoMessage, inner);
		return envelope;
		}

	[TestMethod]
	public void PowerStateDefaultsToUnknown ()
		{
		MrpProtocol protocol = CreateProtocol ();
		Assert.AreEqual (MrpPowerState.Unknown, protocol.PowerState);
		}

	[TestMethod]
	public void DeviceInfoMessageWithLogicalDeviceCountReportsOn ()
		{
		MrpProtocol protocol = CreateProtocol ();
		ProtocolMessage message = DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 1);

		protocol.MessageReceived (message.ToByteArray ());

		Assert.AreEqual (MrpPowerState.On, protocol.PowerState);
		}

	[TestMethod]
	public void DeviceInfoMessageWithZeroLogicalDeviceCountReportsOff ()
		{
		MrpProtocol protocol = CreateProtocol ();
		ProtocolMessage message = DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 0);

		protocol.MessageReceived (message.ToByteArray ());

		Assert.AreEqual (MrpPowerState.Off, protocol.PowerState);
		}

	[TestMethod]
	public void DeviceInfoMessageWithoutLogicalDeviceCountReportsUnknown ()
		{
		MrpProtocol protocol = CreateProtocol ();
		ProtocolMessage message = DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: null);

		protocol.MessageReceived (message.ToByteArray ());

		Assert.AreEqual (MrpPowerState.Unknown, protocol.PowerState);
		}

	[TestMethod]
	public void DeviceInfoUpdateMessageAlsoUpdatesPowerState ()
		{
		MrpProtocol protocol = CreateProtocol ();
		ProtocolMessage message = DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoUpdateMessage, logicalDeviceCount: 1);

		protocol.MessageReceived (message.ToByteArray ());

		Assert.AreEqual (MrpPowerState.On, protocol.PowerState);
		}

	[TestMethod]
	public void UnrelatedMessageDoesNotChangePowerState ()
		{
		MrpProtocol protocol = CreateProtocol ();
		protocol.MessageReceived (DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 1).ToByteArray ());
		Assert.AreEqual (MrpPowerState.On, protocol.PowerState);

		var unrelated = new ProtocolMessage { Type = ProtocolMessage.Types.Type.SetStateMessage };
		unrelated.SetExtension (SetStateMessageExtensions.SetStateMessage, new SetStateMessage ());
		protocol.MessageReceived (unrelated.ToByteArray ());

		Assert.AreEqual (MrpPowerState.On, protocol.PowerState);
		}

	[TestMethod]
	public void PowerStateChangedFiresOnlyWhenStateActuallyChanges ()
		{
		MrpProtocol protocol = CreateProtocol ();
		var transitions = new List<(MrpPowerState Old, MrpPowerState New)> ();
		protocol.PowerStateChanged += (oldState, newState) => transitions.Add ((oldState, newState));

		protocol.MessageReceived (DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 1).ToByteArray ());
		protocol.MessageReceived (DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 1).ToByteArray ());
		protocol.MessageReceived (DeviceInfoMessage (ProtocolMessage.Types.Type.DeviceInfoMessage, logicalDeviceCount: 0).ToByteArray ());

		Assert.AreEqual (2, transitions.Count);
		Assert.AreEqual ((MrpPowerState.Unknown, MrpPowerState.On), transitions[0]);
		Assert.AreEqual ((MrpPowerState.On, MrpPowerState.Off), transitions[1]);
		}
	}
