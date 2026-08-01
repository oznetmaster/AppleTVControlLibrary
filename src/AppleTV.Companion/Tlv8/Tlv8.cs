using System;
using System.Collections.Generic;
using System.Text;

namespace AppleTvControlLibrary.Tlv8;

/// <summary>
/// Correspond to TLV values in HAP specification.
/// </summary>
// pyatv/auth/hap_tlv8.py:13-34
public enum TlvValue
	{
	/// <summary>Pairing method. pyatv/auth/hap_tlv8.py:17</summary>
	Method = 0x00,
	/// <summary>Peer identifier. pyatv/auth/hap_tlv8.py:18</summary>
	Identifier = 0x01,
	/// <summary>SRP salt. pyatv/auth/hap_tlv8.py:19</summary>
	Salt = 0x02,
	/// <summary>Public key. pyatv/auth/hap_tlv8.py:20</summary>
	PublicKey = 0x03,
	/// <summary>SRP proof. pyatv/auth/hap_tlv8.py:21</summary>
	Proof = 0x04,
	/// <summary>Encrypted data. pyatv/auth/hap_tlv8.py:22</summary>
	EncryptedData = 0x05,
	/// <summary>Sequence number. pyatv/auth/hap_tlv8.py:23</summary>
	SeqNo = 0x06,
	/// <summary>Error code. pyatv/auth/hap_tlv8.py:24</summary>
	Error = 0x07,
	/// <summary>Backoff time in seconds. pyatv/auth/hap_tlv8.py:25</summary>
	BackOff = 0x08,
	/// <summary>Certificate. pyatv/auth/hap_tlv8.py:26</summary>
	Certificate = 0x09,
	/// <summary>Signature. pyatv/auth/hap_tlv8.py:27</summary>
	Signature = 0x0A,
	/// <summary>Permissions. pyatv/auth/hap_tlv8.py:28</summary>
	Permissions = 0x0B,
	/// <summary>Fragment data. pyatv/auth/hap_tlv8.py:29</summary>
	FragmentData = 0x0C,
	/// <summary>Last fragment. pyatv/auth/hap_tlv8.py:30</summary>
	FragmentLast = 0x0D,

	/// <summary>Apple internal(?) name value. pyatv/auth/hap_tlv8.py:33</summary>
	Name = 0x11,
	/// <summary>Apple internal(?) flags value. pyatv/auth/hap_tlv8.py:34</summary>
	PairingFlags = 0x13,
	}

/// <summary>
/// Flag values used with TlvValue.PairingFlags.
/// </summary>
// pyatv/auth/hap_tlv8.py:37-40
public enum PairingFlagValue
	{
	/// <summary>Transient pairing flag. pyatv/auth/hap_tlv8.py:40</summary>
	TransientPairing = 0x10,
	}

/// <summary>
/// Correspond to error codes in HAP specification.
/// </summary>
// pyatv/auth/hap_tlv8.py:43-52
public enum ErrorCode
	{
	/// <summary>Unknown error. pyatv/auth/hap_tlv8.py:46</summary>
	Unknown = 0x01,
	/// <summary>Authentication error. pyatv/auth/hap_tlv8.py:47</summary>
	Authentication = 0x02,
	/// <summary>Backoff required. pyatv/auth/hap_tlv8.py:48</summary>
	BackOff = 0x03,
	/// <summary>Max peers reached. pyatv/auth/hap_tlv8.py:49</summary>
	MaxPeers = 0x04,
	/// <summary>Max tries reached. pyatv/auth/hap_tlv8.py:50</summary>
	MaxTries = 0x05,
	/// <summary>Unavailable. pyatv/auth/hap_tlv8.py:51</summary>
	Unavailable = 0x06,
	/// <summary>Busy. pyatv/auth/hap_tlv8.py:52</summary>
	Busy = 0x07,
	}

/// <summary>
/// Correspond to methods in HAP specification.
/// </summary>
// pyatv/auth/hap_tlv8.py:55-63
public enum Method
	{
	/// <summary>Pair setup. pyatv/auth/hap_tlv8.py:58</summary>
	PairSetup = 0x00,
	/// <summary>Pair setup with auth. pyatv/auth/hap_tlv8.py:59</summary>
	PairSetupWithAuth = 0x01,
	/// <summary>Pair verify. pyatv/auth/hap_tlv8.py:60</summary>
	PairVerify = 0x02,
	/// <summary>Add pairing. pyatv/auth/hap_tlv8.py:61</summary>
	AddPairing = 0x03,
	/// <summary>Remove pairing. pyatv/auth/hap_tlv8.py:62</summary>
	RemovePairing = 0x04,
	/// <summary>List pairing. pyatv/auth/hap_tlv8.py:63</summary>
	ListPairing = 0x05,
	}

/// <summary>
/// Correspond to states in HAP specification.
/// </summary>
// pyatv/auth/hap_tlv8.py:66-74
public enum State
	{
	/// <summary>State M1. pyatv/auth/hap_tlv8.py:69</summary>
	M1 = 0x01,
	/// <summary>State M2. pyatv/auth/hap_tlv8.py:70</summary>
	M2 = 0x02,
	/// <summary>State M3. pyatv/auth/hap_tlv8.py:71</summary>
	M3 = 0x03,
	/// <summary>State M4. pyatv/auth/hap_tlv8.py:72</summary>
	M4 = 0x04,
	/// <summary>State M5. pyatv/auth/hap_tlv8.py:73</summary>
	M5 = 0x05,
	/// <summary>State M6. pyatv/auth/hap_tlv8.py:74</summary>
	M6 = 0x06,
	}

/// <summary>
/// Implementation of TLV8 used by HomeKit pairing process.
/// </summary>
/// <remarks>
/// Note that this implementation only supports one level of value, i.e. no dicts
/// in dicts.
/// </remarks>
// pyatv/auth/hap_tlv8.py (ported in full)
public static class Tlv8
	{
	/// <summary>Parse TLV8 bytes into a dict.</summary>
	/// <remarks>
	/// If value is larger than 255 bytes, it is split up in multiple chunks. So
	/// the same tag might occur several times.
	/// </remarks>
	// pyatv/auth/hap_tlv8.py:77-100 (read_tlv)
	public static Dictionary<int, byte[]> ReadTlv (byte[] data)
		{
		var result = new Dictionary<int, byte[]> ();
		int pos = 0;
		int size = data.Length;

		// pyatv/auth/hap_tlv8.py:84-98 (_parse, iterative rewrite of the recursion)
		while (pos < size)
			{
			int tag = data[pos];
			int length = data[pos + 1];
			var value = new byte[length];
			Array.Copy (data, pos + 2, value, 0, length);

			if (result.TryGetValue (tag, out var existing))
				{
				// pyatv/auth/hap_tlv8.py:94-95 (value > 255 is split up)
				var combined = new byte[existing.Length + value.Length];
				Array.Copy (existing, combined, existing.Length);
				Array.Copy (value, 0, combined, existing.Length, value.Length);
				result[tag] = combined;
				}
			else
				{
				result[tag] = value;
				}

			pos += 2 + length;
			}

		return result;
		}

	/// <summary>Convert a dict to TLV8 bytes.</summary>
	/// <remarks>
	/// NB: This simple implementation assumes all values are bytes!
	/// </remarks>
	// pyatv/auth/hap_tlv8.py:103-123 (write_tlv)
	public static byte[] WriteTlv (IEnumerable<KeyValuePair<int, byte[]>> data)
		{
		var tlv = new List<byte> ();

		foreach (var kvp in data)
			{
			byte tag = (byte)kvp.Key;
			var value = kvp.Value;
			int length = value.Length;
			int pos = 0;

			// pyatv/auth/hap_tlv8.py:114-122
			// A tag with length > 255 is added multiple times and concatenated into
			// one buffer when reading the TLV again.
			while (pos < value.Length)
				{
				int size = Math.Min (length, 255);
				tlv.Add (tag);
				tlv.Add ((byte)size);
				for (int i = 0; i < size; i++)
					{
					tlv.Add (value[pos + i]);
					}

				pos += size;
				length -= size;
				}
			}

		return tlv.ToArray ();
		}

	/// <summary>Create simplified string of TLV8 data.</summary>
	/// <remarks>
	/// Method, sequence number, error and backoff time are parsed while the rest
	/// are just summarized with value byte length.
	/// </remarks>
	// pyatv/auth/hap_tlv8.py:126-158 (stringify)
	public static string Stringify (IEnumerable<KeyValuePair<int, byte[]>> data)
		{
		var output = new List<string> ();

		foreach (var kvp in data)
			{
			int key = kvp.Key;
			var value = kvp.Value;
			bool isKnownKey = Enum.IsDefined (typeof (TlvValue), key);

			if (!isKnownKey)
				{
				// pyatv/auth/hap_tlv8.py:142-143
				output.Add ($"{ToHex (key)}={value.Length}bytes");
				continue;
				}

			var keyType = (TlvValue)key;

			if (keyType == TlvValue.Method)
				{
				// pyatv/auth/hap_tlv8.py:144-146
				long method = FromBytesLittleEndian (value);
				output.Add (keyType + "=" + EnumValueName<Method> (method));
				}
			else if (keyType == TlvValue.SeqNo)
				{
				// pyatv/auth/hap_tlv8.py:147-149
				long seqno = FromBytesLittleEndian (value);
				output.Add (keyType + "=" + EnumValueName<State> (seqno));
				}
			else if (keyType == TlvValue.Error)
				{
				// pyatv/auth/hap_tlv8.py:150-152
				long code = FromBytesLittleEndian (value);
				output.Add (keyType + "=" + EnumValueName<ErrorCode> (code));
				}
			else if (keyType == TlvValue.BackOff)
				{
				// pyatv/auth/hap_tlv8.py:153-155
				long seconds = FromBytesLittleEndian (value);
				output.Add ($"{keyType}={seconds}s");
				}
			else
				{
				// pyatv/auth/hap_tlv8.py:156-157
				output.Add ($"{keyType}={value.Length}bytes");
				}
			}

		return string.Join (", ", output);
		}

	// pyatv/auth/hap_tlv8.py:133-137 (_enum_value_name)
	private static string EnumValueName<TEnum> (long value) where TEnum : struct, Enum
		{
		if (Enum.IsDefined (typeof (TEnum), (int)value))
			{
			return Enum.GetName (typeof (TEnum), (int)value) ?? ToHex (value);
			}

		return ToHex (value);
		}

	private static string ToHex (long value)
		{
		return "0x" + value.ToString ("x", System.Globalization.CultureInfo.InvariantCulture);
		}

	private static long FromBytesLittleEndian (byte[] value)
		{
		long result = 0;
		for (int i = 0; i < value.Length; i++)
			{
			result |= (long)value[i] << (8 * i);
			}

		return result;
		}
	}