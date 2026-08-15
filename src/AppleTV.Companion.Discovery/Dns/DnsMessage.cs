// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a DNS message (query or response).</summary>
/// <remarks>Initializes a new instance of the <see cref="DnsMessage"/> class.</remarks>
/// <param name="msgId">The message id.</param>
/// <param name="flags">The message flags.</param>
// pyatv/support/dns.py (DnsMessage) — line 361-448 as of pyatv 0.18.0
public sealed class DnsMessage (ushort msgId = 0, ushort flags = DnsMessage.DEFAULT_FLAGS)
	{
	// pyatv/support/dns.py (default flags=0x0120) — line 364 as of pyatv 0.18.0
	private const ushort DEFAULT_FLAGS = 0x0120;

	/// <summary>Gets or sets the message id.</summary>
	public ushort MsgId
		{
		get; set;
		} = msgId;

	/// <summary>Gets or sets the message flags.</summary>
	public ushort Flags
		{
		get; set;
		} = flags;

	/// <summary>Gets the questions in this message.</summary>
	public List<DnsQuestion> Questions
		{
		get;
		} = [];

	/// <summary>Gets the answer resource records in this message.</summary>
	public List<DnsResource> Answers
		{
		get;
		} = [];

	/// <summary>Gets the authority resource records in this message.</summary>
	public List<DnsResource> Authorities
		{
		get;
		} = [];

	/// <summary>Gets the additional resource records in this message.</summary>
	public List<DnsResource> Resources
		{
		get;
		} = [];

	/// <summary>Unpacks bytes into this <see cref="DnsMessage"/>.</summary>
	/// <param name="msg">The raw message bytes.</param>
	/// <returns>This instance, for chaining.</returns>
	// pyatv/support/dns.py (unpack) — line 373-401 as of pyatv 0.18.0
	public DnsMessage Unpack (byte[] msg)
		{
		DnsBufferReader buffer = new DnsBufferReader (msg);

		DnsHeader header = DnsHeader.UnpackRead (buffer);
		MsgId = header.Id;
		Flags = header.Flags;

		for (int i = 0; i < header.Qdcount; i++)
			{
			Questions.Add (DnsQuestion.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Ancount; i++)
			{
			Answers.Add (DnsResource.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Nscount; i++)
			{
			Authorities.Add (DnsResource.UnpackRead (buffer));
			}

		for (int i = 0; i < header.Arcount; i++)
			{
			Resources.Add (DnsResource.UnpackRead (buffer));
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
			MsgId,
			Flags,
			(ushort)Questions.Count,
			(ushort)Answers.Count,
			(ushort)Authorities.Count,
			(ushort)Resources.Count);

		List<byte> buf = [.. header.Pack ()];

		foreach (DnsQuestion question in Questions)
			{
			buf.AddRange (question.Pack ());
			}

		return [.. buf];
		}
	}
