using System;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>
/// A minimal big-endian binary cursor over a byte buffer, used for the DNS wire format.
/// </summary>
// pyatv/support/dns.py (unpack_stream) — line 19-25 as of pyatv 0.18.0 - callers use this in place of Python's BinaryIO
public sealed class DnsBufferReader
	{
	private readonly byte[] _buffer;

	/// <summary>Initializes a new instance of the <see cref="DnsBufferReader"/> class.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	public DnsBufferReader (byte[] buffer)
		{
		this._buffer = buffer;
		this.Position = 0;
		}

	/// <summary>Gets or sets the current read position, in bytes, into the buffer.</summary>
	public int Position
		{
		get;
		set;
		}

	/// <summary>Gets the total length of the buffer, in bytes.</summary>
	public int Length => this._buffer.Length;

	/// <summary>Gets a value indicating whether there is unread data remaining in the buffer.</summary>
	public bool HasData => this.Position < this._buffer.Length;

	/// <summary>Reads a single byte and advances the position by one.</summary>
	/// <returns>The byte read.</returns>
	public byte ReadByte ()
		{
		byte value = this._buffer[this.Position];
		this.Position += 1;
		return value;
		}

	/// <summary>Reads a number of bytes and advances the position accordingly.</summary>
	/// <param name="count">The number of bytes to read.</param>
	/// <returns>The bytes read.</returns>
	public byte[] ReadBytes (int count)
		{
		byte[] result = new byte[count];
		Array.Copy (this._buffer, this.Position, result, 0, count);
		this.Position += count;
		return result;
		}

	/// <summary>Reads a big-endian 16-bit unsigned integer and advances the position by two.</summary>
	/// <returns>The value read.</returns>
	public ushort ReadUInt16BE ()
		{
		ushort value = (ushort)((this._buffer[this.Position] << 8) | this._buffer[this.Position + 1]);
		this.Position += 2;
		return value;
		}

	/// <summary>Reads a big-endian 32-bit unsigned integer and advances the position by four.</summary>
	/// <returns>The value read.</returns>
	public uint ReadUInt32BE ()
		{
		uint value = ((uint)this._buffer[this.Position] << 24) |
			((uint)this._buffer[this.Position + 1] << 16) |
			((uint)this._buffer[this.Position + 2] << 8) |
			this._buffer[this.Position + 3];
		this.Position += 4;
		return value;
		}
	}
