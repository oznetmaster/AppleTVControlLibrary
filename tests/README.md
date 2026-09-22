# Testing AppleTVControlLibrary

The same NUnit fixtures run on .NET Framework 4.7.2 and .NET 10. Install the .NET 10 SDK and the .NET Framework 4.7.2 developer pack on Windows.

| Dependency | Version | Purpose |
| --- | --- | --- |
| NUnit | 4.6.1 | Test framework |
| NUnit3TestAdapter | 6.3.0 | Test Explorer and VSTest discovery/execution |
| Microsoft.NET.Test.Sdk | 18.10.1 | Desktop test host |
| NUnit.Analyzers | 4.15.0 | Compile-time NUnit checks |
| System.Text.Json | 10.0.12 | Desktop sample credential-file compatibility tests |

From the repository root:

```powershell
dotnet test AppleTVControlLibrary.slnx -c Release --filter "TestCategory!=Live"
```

There are 237 offline cases per framework: 109 Companion/sample-storage, 56 HAP and 72 MRP. Pairing and protocol tests use simulated devices. Eight credential-store cases cover both sample applications, existing files, unknown fields, base64 key preservation, corrupt-file isolation and auto-connect selection. They use temporary synthetic data, never saved pairing credentials.

Run the five socket discovery cases separately and sequentially:

```powershell
dotnet test tests/AppleTV.Companion.LiveTests/AppleTV.Companion.LiveTests.csproj -c Release -f net472 --settings .runsettings.network
dotnet test tests/AppleTV.Companion.LiveTests/AppleTV.Companion.LiveTests.csproj -c Release -f net10.0 --settings .runsettings.network
```

These cases use real multicast/unicast sockets and a simulated mDNS responder; they do not pair with or control a physical Apple TV. Discovery alone never enables them. Multicast support and permission to share UDP port 5353 are required.

## Dependency decisions

The 2.2.6 runtime update uses stable Google.Protobuf 3.36.2, Grpc.Tools 2.84.0 and Microsoft compatibility packages 10.0.12. Desktop samples use System.Text.Json 10.0.12. No preview packages are selected.

Keep plist-cil 2.2.0: 2.3.1 only provides .NET 5–9 assets and restore rejects it for net472 (NU1202). Both supported targets retain the same compatible plist implementation. BouncyCastle.Cryptography 2.7.0, System.Memory 4.6.3 and Hafner.Compatibility.MetaPackage 1.9.0 remain current direct dependencies. Transitive test-host and compatibility assemblies follow their parent package versions; they are not independently overridden merely because NuGet lists a newer major version.

The protocol libraries have no Newtonsoft.Json, log4net or System.Text.Json runtime dependency. Companion Link uses OPACK/TLV8 and MRP uses generated Protobuf messages plus binary property lists. Their binary codec APIs are required protocol functionality, not raw JSON APIs. The sample credential stores use typed System.Text.Json models with explicit JsonPropertyName attributes preserving existing file names. Production JSON DOM parsing is absent; one test uses an independent JSON reader to verify saved files.
