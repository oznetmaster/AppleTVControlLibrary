// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using CompanionStorage = AppleTvControlLibrary.Remote.Wpf.Storage;
using Mrp = AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

namespace AppleTV.Companion.Tests.Storage;

[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public sealed class CredentialStoreCompatibilityTests
{
    private string _directory = null!;

    [SetUp]
    public void SetUp () => _directory = Path.Combine (Path.GetTempPath (), "AppleTvStorageTests", Guid.NewGuid ().ToString ("N"));

    [TearDown]
    public void TearDown ()
    {
        if (Directory.Exists (_directory)) Directory.Delete (_directory, true);
    }

    [TestCase (false)]
    [TestCase (true)]
    public void ExistingCredentialFileLoadsWithUnknownFields (bool mrp)
    {
        Directory.CreateDirectory (_directory);
        File.WriteAllText (Path.Combine (_directory, "device.json"),
            "{\"UniqueId\":\"device\",\"Name\":\"Living Room\",\"Address\":\"192.0.2.1\",\"Port\":1234,\"StableIdentifier\":\"stable\",\"Ltpk\":\"AQI=\",\"Ltsk\":\"AwQ=\",\"AtvId\":\"BQ==\",\"ClientId\":\"Bg==\",\"AutoConnect\":true,\"FutureField\":{\"nested\":true}}");
        if (mrp)
        {
            Mrp.StoredDevice device = new Mrp.CredentialStore (_directory).Load ("device")!;
            using (Assert.EnterMultipleScope ())
            {
                Assert.That (device.Ltpk, Is.EqualTo (new byte[] { 1, 2 }));
                Assert.That (device.Ltsk, Is.EqualTo (new byte[] { 3, 4 }));
                Assert.That (device.ClientId, Is.EqualTo (new byte[] { 6 }));
                Assert.That (device.AutoConnect, Is.True);
                Assert.That (device.Port, Is.EqualTo (1234));
            }
        }
        else
        {
            CompanionStorage.StoredDevice device = new CompanionStorage.CredentialStore (_directory).Load ("device")!;
            using (Assert.EnterMultipleScope ())
            {
                Assert.That (device.Ltpk, Is.EqualTo (new byte[] { 1, 2 }));
                Assert.That (device.Ltsk, Is.EqualTo (new byte[] { 3, 4 }));
                Assert.That (device.ClientId, Is.EqualTo (new byte[] { 6 }));
                Assert.That (device.StableIdentifier, Is.EqualTo ("stable"));
                Assert.That (device.AutoConnect, Is.True);
                Assert.That (device.Port, Is.EqualTo (1234));
            }
        }
    }

    [TestCase (false)]
    [TestCase (true)]
    public void SavedFilePreservesExistingNamesAndBase64Keys (bool mrp)
    {
        if (mrp) new Mrp.CredentialStore (_directory).Save (new Mrp.StoredDevice { UniqueId = "device", Name = "Télévision", Ltpk = new byte[] { 1, 2 } });
        else new CompanionStorage.CredentialStore (_directory).Save (new CompanionStorage.StoredDevice { UniqueId = "device", Name = "Télévision", Ltpk = new byte[] { 1, 2 }, StableIdentifier = "stable" });
        // An independent reader verifies the persisted format, not just a serializer round trip.
        using JsonDocument saved = JsonDocument.Parse (File.ReadAllText (Path.Combine (_directory, "device.json")));
        using (Assert.EnterMultipleScope ())
        {
            Assert.That (saved.RootElement.GetProperty ("Name").GetString (), Is.EqualTo ("Télévision"));
            Assert.That (saved.RootElement.GetProperty ("Ltpk").GetString (), Is.EqualTo ("AQI="));
            Assert.That (saved.RootElement.GetProperty ("UniqueId").GetString (), Is.EqualTo ("device"));
            if (!mrp) Assert.That (saved.RootElement.GetProperty ("StableIdentifier").GetString (), Is.EqualTo ("stable"));
        }
    }

    [TestCase (false)]
    [TestCase (true)]
    public void CorruptFileDoesNotHideValidDevice (bool mrp)
    {
        if (mrp)
        {
            var store = new Mrp.CredentialStore (_directory);
            store.Save (new Mrp.StoredDevice { UniqueId = "device", Name = "Room" });
            File.WriteAllText (Path.Combine (_directory, "corrupt.json"), "{");
            Assert.That (store.LoadAll ().Count, Is.EqualTo (1));
        }
        else
        {
            var store = new CompanionStorage.CredentialStore (_directory);
            store.Save (new CompanionStorage.StoredDevice { UniqueId = "device", Name = "Room" });
            File.WriteAllText (Path.Combine (_directory, "corrupt.json"), "{");
            Assert.That (store.LoadAll ().Count, Is.EqualTo (1));
        }
    }

    [TestCase (false)]
    [TestCase (true)]
    public void ChangingAutoConnectKeepsOnlySelectedDevice (bool mrp)
    {
        if (mrp)
        {
            var store = new Mrp.CredentialStore (_directory);
            store.Save (new Mrp.StoredDevice { UniqueId = "first", Name = "One", AutoConnect = true });
            store.Save (new Mrp.StoredDevice { UniqueId = "second", Name = "Two" });
            store.SetAutoConnect ("second");
            Assert.That (store.Load ("first")!.AutoConnect, Is.False);
            Assert.That (store.LoadAutoConnectDevice ()!.UniqueId, Is.EqualTo ("second"));
        }
        else
        {
            var store = new CompanionStorage.CredentialStore (_directory);
            store.Save (new CompanionStorage.StoredDevice { UniqueId = "first", Name = "One", AutoConnect = true });
            store.Save (new CompanionStorage.StoredDevice { UniqueId = "second", Name = "Two" });
            store.SetAutoConnect ("second");
            Assert.That (store.Load ("first")!.AutoConnect, Is.False);
            Assert.That (store.LoadAutoConnectDevice ()!.UniqueId, Is.EqualTo ("second"));
        }
    }
}
