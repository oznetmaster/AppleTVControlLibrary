using System;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>
/// A minimal big-endian binary cursor over a byte buffer, used for the DNS wire format.
/// </summary>
// pyatv/support/dns.py:19-25 (unpack_stream) - callers use this in place of Python's BinaryIO
public sealed class DnsBufferReader
	{
	private readonly byte[] _buffer;

	public DnsBufferReader (byte[] buffer)
		{
		this._buffer = buffer;
		this.Position = 0;
		}

	public int Position
		{
		get;
		set;
		}

	public int Length => this._buffer.Length;

	public bool HasData => this.Position < this._buffer.Length;

	public byte ReadByte ()
		{
		byte value = this._buffer[this.Position];
		this.Position += 1;
		return value;
		}

	public byte[] ReadBytes (int count)
		{
		byte[] result = new byte[count];
		Array.Copy (this._buffer, this.Position, result, 0, count);
		this.Position += count;
		return result;
		}

	public ushort ReadUInt16BE ()
		{
		ushort value = (ushort)((this._buffer[this.Position] << 8) | this._buffer[this.Position + 1]);
		this.Position += 2;
		return value;
		}

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
