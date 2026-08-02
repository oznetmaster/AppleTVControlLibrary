namespace AppleTvControlLibrary.Opack;

/// <summary>
/// Wraps an integer value together with the encoded byte-width it was read with (or
/// should be re-encoded with), so that round-tripping a value through <see cref="Opack"/>
/// produces byte-identical output.
/// </summary>
// pyatv/support/opack.py (_sized_int) — line 16-29 as of pyatv 0.18.0
public readonly struct SizedInteger
	{
	/// <summary>Initializes a new instance of the <see cref="SizedInteger"/> struct.</summary>
	/// <param name="value">The integer value.</param>
	/// <param name="size">The encoded width in bytes (1, 2, 4 or 8).</param>
	public SizedInteger (long value, int size)
		{
		this.Value = value;
		this.Size = size;
		}

	/// <summary>Gets the integer value.</summary>
	public long Value
		{
		get;
		}

	/// <summary>Gets the encoded width, in bytes, of this integer (1, 2, 4 or 8).</summary>
	public int Size
		{
		get;
		}
	}