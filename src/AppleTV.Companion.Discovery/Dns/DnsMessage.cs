// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a DNS message (query or response).</summary>
// pyatv/support/dns.py (DnsMessage) — line 361-448 as of pyatv 0.18.0
public sealed class DnsMessage
	{
	// pyatv/support/dns.py (default flags=0x0120) — line 364 as of pyatv 0.18.0
	private const ushort DEFAULT_FLAGS = 0x0120;

	/// <summary>Initializes a new instance of the <see cref="DnsMessage"/> class.</summary>
	/// <param name="msgId">The message id.</param>
	/// <param name="flags">The message flags.</param>
	public DnsMessage (ushort msgId = 0, ushort flags = DEFAULT_FLAGS)
		{
		this.MsgId = msgId;
		this.Flags = flags;
		this.Questions = new List<DnsQuestion> ();
		this.Answers = new List<DnsResource> ();
		this.Authorities = new List<DnsResource> ();
		this.Resources = new List<DnsResource> ();
		}

	/// <summary>Gets or sets the message id.</summary>
	public ushort MsgId
		{
		get; set;
		}

	/// <summary>Gets or sets the message flags.</summary>
	public ushort Flags
		{
		get; set;
		}

	/// <summary>Gets the questions in this message.</summary>
	public List<DnsQuestion> Questions
		{
		get;
		}

	/// <summary>Gets the answer resource records in this message.</summary>
	public List<DnsResource> Answers
		{
		get;
		}

	/// <summary>Gets the authority resource records in this message.</summary>
	public List<DnsResource> Authorities
		{
		get;
		}

	/// <summary>Gets the additional resource records in this message.</summary>
	public List<DnsResource> Resources
		{
		get;
		}

	/// <summary>Unpacks bytes into this <see cref="DnsMessage"/>.</summary>
	/// <param name="msg">The raw message bytes.</param>
	/// <returns>This instance, for chaining.</returns>
	// pyatv/support/dns.py (unpack) — line 373-401 as of pyatv 0.18.0
	public DnsMessage Unpack (byte[] msg)
		{
		DnsBufferReader buffer = new DnsBufferReader (msg);

		DnsHeader header = DnsHeader.UnpackRead (buffer);
		this.MsgId = header.Id;
		this.Flags = header.Flags;

		for (int i = 0; i < header.Qdcount; i++)
			{
			this.Questions.Add (DnsQuestion.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Ancount; i++)
			{
			this.Answers.Add (DnsResource.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Nscount; i++)
			{
			this.Authorities.Add (DnsResource.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Arcount; i++)
			{
			this.Resources.Add (DnsResource.UnpackRead (buffer));
			}

		return this;
		}

	/// <summary>Packs this message into bytes.</summary>
	/// <returns>The packed message bytes.</returns>
	/// <remarks>
	/// Only the question section is packed for outgoing queries, matching the only usage
	/// pyatv makes of this method (<c>create_service_queries</c>); answer/authority/resource
	/// packing is not needed by a discovery client and is therefore not implemented.
	/// </remarks>
	// pyatv/support/dns.py (pack) — line 403-439 as of pyatv 0.18.0
	public byte[] Pack ()
		{
		DnsHeader header = new DnsHeader (
			this.MsgId,
			this.Flags,
			(ushort)this.Questions.Count,
			(ushort)this.Answers.Count,
			(ushort)this.Authorities.Count,
			(ushort)this.Resources.Count);

		List<byte> buf = new List<byte> ();
		buf.AddRange (header.Pack ());

		foreach (DnsQuestion question in this.Questions)
			{
			buf.AddRange (question.Pack ());
			}

		return buf.ToArray ();
		}
	}
