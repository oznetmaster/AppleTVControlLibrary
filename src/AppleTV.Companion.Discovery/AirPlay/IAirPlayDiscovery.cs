// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AppleTvControlLibrary.Discovery.AirPlay;

/// <summary>
/// Discovers AirPlay devices on the local network. Kept behind an interface so
/// multicast discovery - the least portable part of the library - can be swapped for a
/// static address on hosts where multicast/mDNS isn't available or reliable.
/// </summary>
public interface IAirPlayDiscovery
	{
	/// <summary>Scans the network for AirPlay devices.</summary>
	/// <param name="timeout">How long to wait for responses.</param>
	/// <param name="cancellationToken">A token to cancel the scan.</param>
	/// <returns>The discovered AirPlay devices.</returns>
	Task<IReadOnlyList<AirPlayDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default);
	}
