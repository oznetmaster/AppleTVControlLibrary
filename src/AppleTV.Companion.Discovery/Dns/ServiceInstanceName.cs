namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>
/// Represents either a service or service instance name in the DNS, handling periods
/// embedded in the instance name correctly.
/// </summary>
// pyatv/support/dns.py (ServiceInstanceName) — line 28-68 as of pyatv 0.18.0
public sealed class ServiceInstanceName
	{
	/// <summary>Initializes a new instance of the <see cref="ServiceInstanceName"/> class.</summary>
	/// <param name="instance">The optional instance label (e.g. "Living Room").</param>
	/// <param name="service">The service label pair (e.g. "_companion-link._tcp").</param>
	/// <param name="domain">The domain, defaulting to "local".</param>
	public ServiceInstanceName (string? instance, string service, string domain = "local")
		{
		this.Instance = instance;
		this.Service = service;
		this.Domain = domain;
		}

	/// <summary>Gets the optional instance name.</summary>
	public string? Instance
		{
		get;
		}

	/// <summary>Gets the service label pair (e.g. "_companion-link._tcp").</summary>
	public string Service
		{
		get;
		}

	/// <summary>Gets the domain (typically "local").</summary>
	public string Domain
		{
		get;
		}

	/// <summary>Gets just the service name, like the name for a PTR record.</summary>
	// pyatv/support/dns.py (ptr_name) — line 65-68 as of pyatv 0.18.0
	public string PtrName => string.Join (".", new[] { this.Service, this.Domain });

	/// <summary>
	/// Splits a name into instance (optional), service, and domain parts.
	/// </summary>
	/// <param name="name">The full name to split.</param>
	/// <returns>The parsed <see cref="ServiceInstanceName"/>.</returns>
	/// <exception cref="System.ArgumentException">
	/// Thrown when <paramref name="name"/> isn't a service name or service instance name.
	/// </exception>
	// pyatv/support/dns.py (split_name) — line 43-63 as of pyatv 0.18.0
	public static ServiceInstanceName SplitName (string name)
		{
		string[] labels = name.Split ('.');
		if (labels.Length < 2)
			{
			throw new System.ArgumentException ("There must be at least three labels in a service name", nameof (name));
			}

		for (int index = 0; index < labels.Length - 1; index++)
			{
			string label = labels[index];
			string nextLabel = labels[index + 1];
			// CA1865 (StartsWith(char)) is not available on net472; this must build on both TFMs.
#pragma warning disable CA1865
			if (label.StartsWith ("_", System.StringComparison.Ordinal) &&
#pragma warning restore CA1865
				(nextLabel.Equals ("_tcp", System.StringComparison.OrdinalIgnoreCase) ||
				 nextLabel.Equals ("_udp", System.StringComparison.OrdinalIgnoreCase)))
				{
				string? instance = index > 0 ? string.Join (".", labels, 0, index) : null;
				string service = label + "." + nextLabel;
				string domain = string.Join (".", labels, index + 2, labels.Length - (index + 2));
				return new ServiceInstanceName (instance, service, domain);
				}
			}

		throw new System.ArgumentException ($"'{name}' is not a service domain, nor a service instance name", nameof (name));
		}
	}
