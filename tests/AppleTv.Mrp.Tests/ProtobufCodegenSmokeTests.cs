using AppleTvControlLibrary.Mrp.Protobuf;
using Google.Protobuf;
using NUnit.Framework;

namespace AppleTvControlLibrary.Mrp.Tests;

/// <summary>
/// Smoke tests confirming the protobuf codegen pipeline (WP1) produces usable, round-trippable
/// message types from the vendored pyatv MRP .proto definitions on both target frameworks.
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public sealed class ProtobufCodegenSmokeTests
{
	[Test]
	public void DeviceInfoMessage_RoundTripsThroughByteArray()
	{
		var message = new DeviceInfoMessage
		{
			Name = "Test Remote",
			UniqueIdentifier = "B8D8678C-9DA9-4D29-9338-5D6B827B8063",
			ApplicationBundleIdentifier = "com.example.testremote",
			ProtocolVersion = 1,
			AllowsPairing = true,
		};

		byte[] bytes = message.ToByteArray();
		var roundTripped = DeviceInfoMessage.Parser.ParseFrom(bytes);

		Assert.That (roundTripped.Name, Is.EqualTo (message.Name));
		Assert.That (roundTripped.UniqueIdentifier, Is.EqualTo (message.UniqueIdentifier));
		Assert.That (roundTripped.ApplicationBundleIdentifier, Is.EqualTo (message.ApplicationBundleIdentifier));
		Assert.That (roundTripped.ProtocolVersion, Is.EqualTo (message.ProtocolVersion));
		Assert.That (roundTripped.AllowsPairing, Is.EqualTo (message.AllowsPairing));
	}

	[Test]
	public void ProtocolMessage_CarriesDeviceInfoMessageExtension()
	{
		// pyatv/protocols/mrp/protobuf/DeviceInfoMessage.proto: DeviceInfoMessage is registered as
		// extension field 20 on ProtocolMessage, which is how MRP frames carry typed payloads.
		var inner = new DeviceInfoMessage { Name = "Extension Test" };
		var envelope = new ProtocolMessage
		{
			Type = ProtocolMessage.Types.Type.DeviceInfoMessage,
		};
		envelope.SetExtension(DeviceInfoMessageExtensions.DeviceInfoMessage, inner);

		var registry = new ExtensionRegistry { DeviceInfoMessageExtensions.DeviceInfoMessage };
		byte[] bytes = envelope.ToByteArray();
		var roundTripped = ProtocolMessage.Parser.WithExtensionRegistry(registry).ParseFrom(bytes);

		Assert.That (roundTripped.Type, Is.EqualTo (ProtocolMessage.Types.Type.DeviceInfoMessage));
		Assert.That (roundTripped.GetExtension(DeviceInfoMessageExtensions.DeviceInfoMessage).Name, Is.EqualTo ("Extension Test"));
	}
}