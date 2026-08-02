using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using AppleTvControlLibrary.Discovery.Dns;

namespace AppleTvControlLibrary.LiveTests.Discovery;

/// <summary>
/// A real, socket-backed fake mDNS responder for Companion Link discovery: joins the mDNS
/// multicast group on the loopback host, answers PTR queries for a single configured service
/// with PTR/SRV/TXT/A records, and unicasts the reply back to the querier -- exactly what
/// <see cref="AppleTvControlLibrary.Discovery.Companion.MulticastCompanionDiscovery"/> expects
/// to see from a real Apple TV on the LAN.
/// </summary>
/// <remarks>
/// This exists to exercise the real <c>UdpClient</c> multicast join/send/receive path used by
/// <c>MulticastCompanionDiscovery.ScanAsync</c>, rather than relying only on unit tests against
/// already-decoded DNS records. Running it against the real discovery code on localhost is the
/// most direct way to catch socket-level scan bugs (the "scan never returns" / "operation
/// aborted" class of bug) without needing real hardware.
/// </remarks>
internal sealed class FakeMdnsResponder : IDisposable
	{
	// pyatv/core/mdns.py (multicast default address) — line 509 as of pyatv 0.18.0
	private const string MulticastAddress = "224.0.0.251";

	// pyatv/core/mdns.py (multicast default port) — line 510 as of pyatv 0.18.0
	private const int MulticastPort = 5353;

	private readonly UdpClient _client;
	private readonly string _serviceType;
	private readonly string _instanceName;
	private readonly string _hostName;
	private readonly IPAddress _address;
	private readonly int _port;
	private readonly IReadOnlyDictionary<string, string> _txtProperties;
	private Thread? _thread;
	private volatile bool _disposed;

	/// <summary>Initializes a new instance of the <see cref="FakeMdnsResponder"/> class and joins the multicast group.</summary>
	/// <param name="serviceType">The DNS-SD service type to answer for, e.g. <c>_companion-link._tcp.local</c>.</param>
	/// <param name="instanceName">The service instance name, e.g. <c>Living Room</c>.</param>
	/// <param name="hostName">The target host name for the SRV/A records, e.g. <c>appletv.local</c>.</param>
	/// <param name="address">The address to answer in the A record.</param>
	/// <param name="port">The service port to answer in the SRV record.</param>
	/// <param name="txtProperties">The TXT record key/value pairs to answer with.</param>
	public FakeMdnsResponder (
		string serviceType,
		string instanceName,
		string hostName,
		IPAddress address,
		int port,
		IReadOnlyDictionary<string, string> txtProperties)
		{
		_serviceType = serviceType;
		_instanceName = instanceName;
		_hostName = hostName;
		_address = address;
		_port = port;
		_txtProperties = txtProperties;

		_client = new UdpClient (AddressFamily.InterNetwork);
		_client.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_client.Client.Bind (new IPEndPoint (IPAddress.Any, MulticastPort));
		_client.JoinMulticastGroup (IPAddress.Parse (MulticastAddress));
		}

	/// <summary>Starts the background thread that listens for and answers queries.</summary>
	public void Start ()
		{
		_thread = new Thread (Loop)
			{
			IsBackground = true,
			Name = "FakeMdnsResponder",
			};
		_thread.Start ();
		}

	private void Loop ()
		{
		while (!_disposed)
			{
			IPEndPoint remote = new IPEndPoint (IPAddress.Any, 0);
			byte[] data;
			try
				{
				data = _client.Receive (ref remote);
				}
			catch (Exception)
				{
				// Socket was closed to stop the responder.
				return;
				}

			try
				{
				DnsMessage message = new DnsMessage ().Unpack (data);
				bool matches = false;
				foreach (DnsQuestion question in message.Questions)
					{
					if (question.QType == QueryType.Ptr && string.Equals (question.QName, _serviceType, StringComparison.OrdinalIgnoreCase))
						{
						matches = true;
						break;
						}
					}

				if (!matches)
					{
					continue;
					}

				byte[] response = BuildResponse (message.MsgId);
				_client.Send (response, response.Length, remote);
				}
			catch (Exception)
				{
				// Malformed query, or the socket was closed mid-iteration -- keep listening
				// (or let the next Receive() surface the closed-socket exception above).
				}
			}
		}

	private byte[] BuildResponse (ushort msgId)
		{
		string instanceQName = _instanceName + "." + _serviceType;

		List<byte> answers = new List<byte> ();
		int answerCount = 0;

		// PTR: _companion-link._tcp.local -> "Living Room._companion-link._tcp.local"
		AppendRecord (answers, _serviceType, QueryType.Ptr, DnsWireFormat.QNameEncode (instanceQName));
		answerCount++;

		// SRV: instance name -> priority/weight/port/target
		List<byte> srvRdata = new List<byte> ();
		WriteUInt16BE (srvRdata, 0);
		WriteUInt16BE (srvRdata, 0);
		WriteUInt16BE (srvRdata, (ushort)_port);
		srvRdata.AddRange (DnsWireFormat.QNameEncode (_hostName));
		AppendRecord (answers, instanceQName, QueryType.Srv, srvRdata.ToArray ());
		answerCount++;

		// TXT: instance name -> key=value character-strings
		List<byte> txtRdata = new List<byte> ();
		foreach (KeyValuePair<string, string> pair in _txtProperties)
			{
			byte[] entry = Encoding.UTF8.GetBytes ($"{pair.Key}={pair.Value}");
			txtRdata.Add ((byte)entry.Length);
			txtRdata.AddRange (entry);
			}

		if (txtRdata.Count == 0)
			{
			txtRdata.Add (0);
			}

		AppendRecord (answers, instanceQName, QueryType.Txt, txtRdata.ToArray ());
		answerCount++;

		// A: host name -> address
		AppendRecord (answers, _hostName, QueryType.A, _address.GetAddressBytes ());
		answerCount++;

		List<byte> message = new List<byte> ();
		WriteUInt16BE (message, msgId);
		WriteUInt16BE (message, 0x8400); // response, authoritative
		WriteUInt16BE (message, 0); // qdcount
		WriteUInt16BE (message, (ushort)answerCount); // ancount
		WriteUInt16BE (message, 0); // nscount
		WriteUInt16BE (message, 0); // arcount
		message.AddRange (answers);

		return message.ToArray ();
		}

	private static void AppendRecord (List<byte> buffer, string name, QueryType type, byte[] rdata)
		{
		buffer.AddRange (DnsWireFormat.QNameEncode (name));
		WriteUInt16BE (buffer, (ushort)type);
		WriteUInt16BE (buffer, 1); // class IN
		WriteUInt32BE (buffer, 4500); // ttl
		WriteUInt16BE (buffer, (ushort)rdata.Length);
		buffer.AddRange (rdata);
		}

	private static void WriteUInt16BE (List<byte> buffer, ushort value)
		{
		buffer.Add ((byte)(value >> 8));
		buffer.Add ((byte)(value & 0xFF));
		}

	private static void WriteUInt32BE (List<byte> buffer, uint value)
		{
		buffer.Add ((byte)(value >> 24));
		buffer.Add ((byte)(value >> 16));
		buffer.Add ((byte)(value >> 8));
		buffer.Add ((byte)(value & 0xFF));
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		_disposed = true;
		try
			{
			_client.Close ();
			}
		catch (Exception)
			{
			}
		}
	}
