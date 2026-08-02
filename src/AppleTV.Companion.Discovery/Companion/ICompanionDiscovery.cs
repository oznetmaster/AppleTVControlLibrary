// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>
/// Discovers Companion Link devices on the local network. Kept behind an interface so
/// multicast discovery - the least portable part of the library - can be swapped for a
/// static address on hosts where multicast/mDNS isn't available or reliable.
/// </summary>
public interface ICompanionDiscovery
	{
	/// <summary>Scans the network for Companion Link devices.</summary>
	/// <param name="timeout">How long to wait for responses.</param>
	/// <param name="cancellationToken">A token to cancel the scan.</param>
	/// <returns>The discovered Companion Link devices.</returns>
	Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default);
	}
