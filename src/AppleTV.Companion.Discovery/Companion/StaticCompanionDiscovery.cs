// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>
/// A discovery implementation that always returns a single, pre-configured device instead
/// of performing an mDNS scan. Useful on hosts where multicast is unavailable or unreliable
/// (see WP7 notes on Mono/embedded hosts), or when the address is already known.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="StaticCompanionDiscovery"/> class.</remarks>
/// <param name="address">The known address of the Companion Link device.</param>
/// <param name="port">The Companion Link port.</param>
/// <param name="name">An optional display name for the device.</param>
/// <param name="uniqueId">An optional stable unique identifier for the device.</param>
public sealed class StaticCompanionDiscovery (IPAddress address, int port, string name = "", string? uniqueId = null) : ICompanionDiscovery
	{
	private readonly CompanionDiscoveryResult _result = new CompanionDiscoveryResult (
			name,
			address,
			port,
			uniqueId,
			CompanionPairingRequirement.Mandatory,
			new Dictionary<string, string> ());

	/// <inheritdoc/>
	public Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		IReadOnlyList<CompanionDiscoveryResult> results = new[] { _result };
		return Task.FromResult (results);
		}
	}
