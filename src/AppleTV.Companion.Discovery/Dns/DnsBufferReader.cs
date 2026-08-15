// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>
/// A minimal big-endian binary cursor over a byte buffer, used for the DNS wire format.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="DnsBufferReader"/> class.</remarks>
/// <param name="buffer">The buffer to read from.</param>
// pyatv/support/dns.py (unpack_stream) — line 19-25 as of pyatv 0.18.0 - callers use this in place of Python's BinaryIO
public sealed class DnsBufferReader (byte[] buffer)
	{
	private readonly byte[] _buffer = buffer;

	/// <summary>Gets or sets the current read position, in bytes, into the buffer.</summary>
	public int Position
		{
		get;
		set;
		}

	/// <summary>Gets the total length of the buffer, in bytes.</summary>
	public int Length => _buffer.Length;

	/// <summary>Gets a value indicating whether there is unread data remaining in the buffer.</summary>
	public bool HasData => Position < _buffer.Length;

	/// <summary>Reads a single byte and advances the position by one.</summary>
	/// <returns>The byte read.</returns>
	public byte ReadByte ()
		{
		byte value = _buffer[Position];
		Position += 1;
		return value;
		}

	/// <summary>Reads a number of bytes and advances the position accordingly.</summary>
	/// <param name="count">The number of bytes to read.</param>
	/// <returns>The bytes read.</returns>
	public byte[] ReadBytes (int count)
		{
		byte[] result = new byte[count];
		Array.Copy (_buffer, Position, result, 0, count);
		Position += count;
		return result;
		}

	/// <summary>Reads a big-endian 16-bit unsigned integer and advances the position by two.</summary>
	/// <returns>The value read.</returns>
	public ushort ReadUInt16BE ()
		{
		ushort value = (ushort)((_buffer[Position] << 8) | _buffer[Position + 1]);
		Position += 2;
		return value;
		}

	/// <summary>Reads a big-endian 32-bit unsigned integer and advances the position by four.</summary>
	/// <returns>The value read.</returns>
	public uint ReadUInt32BE ()
		{
		uint value = ((uint)_buffer[Position] << 24) |
			((uint)_buffer[Position + 1] << 16) |
			((uint)_buffer[Position + 2] << 8) |
			_buffer[Position + 3];
		Position += 4;
		return value;
		}
	}
