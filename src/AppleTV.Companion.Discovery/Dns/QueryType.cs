// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>DNS resource record type identifiers used by DNS-SD.</summary>
// pyatv/support/dns.py (QueryType) — line 249-256 as of pyatv 0.18.0
#pragma warning disable CA1720 // Identifier contains type name -- "Ptr" is the DNS-SD record type name, not a reference to System.IntPtr/UIntPtr, and is used verbatim throughout the vendored pyatv source this enum ports.
public enum QueryType
	{
	/// <summary>Address record.</summary>
	A = 0x01,

	/// <summary>Domain name pointer.</summary>
	Ptr = 0x0C,

	/// <summary>Text record.</summary>
	Txt = 0x10,

	/// <summary>Service record.</summary>
	Srv = 0x21,

	/// <summary>Wildcard query for any record type.</summary>
	Any = 0xFF,
	}
#pragma warning restore CA1720
