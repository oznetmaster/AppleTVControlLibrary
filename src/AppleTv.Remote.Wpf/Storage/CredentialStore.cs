using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using AppleTvControlLibrary.Auth;

namespace AppleTvControlLibrary.Remote.Wpf.Storage;

/// <summary>
/// The on-disk representation of a paired device: its HAP credentials plus the stable
/// identifier and device metadata that must survive restarts and never be regenerated.
/// </summary>
/// <remarks>
/// The <c>_i</c> stable identifier is the field the porting brief calls out as load-bearing:
/// a null/regenerated value stops power-state events and, from tvOS 18.4, drops the connection
/// entirely a few seconds after handshake. It must be generated once at pair time and persisted
/// alongside the credentials, never regenerated.
/// </remarks>
public sealed class StoredDevice
	{
	/// <summary>Gets or sets the Companion Link stable unique id (the mDNS <c>rpmrtid</c> value).</summary>
	public string UniqueId
		{
		get;
		set;
		} = string.Empty;

	/// <summary>Gets or sets the last known display name of the device.</summary>
	public string Name
		{
		get;
		set;
		} = string.Empty;

	/// <summary>Gets or sets the last known network address of the device.</summary>
	public string Address
		{
		get;
		set;
		} = string.Empty;

	/// <summary>Gets or sets the last known Companion Link port of the device.</summary>
	public int Port
		{
		get;
		set;
		}

	/// <summary>
	/// Gets or sets the stable <c>_i</c> identifier generated once at pair time and never
	/// regenerated (six random bytes, hex-encoded).
	/// </summary>
	public string StableIdentifier
		{
		get;
		set;
		} = string.Empty;

	/// <summary>Gets or sets the peer's long-term public key.</summary>
	public byte[] Ltpk
		{
		get;
		set;
		} = Array.Empty<byte> ();

	/// <summary>Gets or sets this client's long-term secret key.</summary>
	public byte[] Ltsk
		{
		get;
		set;
		} = Array.Empty<byte> ();

	/// <summary>Gets or sets the Apple TV's identifier.</summary>
	public byte[] AtvId
		{
		get;
		set;
		} = Array.Empty<byte> ();

	/// <summary>Gets or sets this client's identifier.</summary>
	public byte[] ClientId
		{
		get;
		set;
		} = Array.Empty<byte> ();

	/// <summary>Converts the stored key material to a <see cref="HapCredentials"/> instance.</summary>
	public HapCredentials ToCredentials () => new (this.Ltpk, this.Ltsk, this.AtvId, this.ClientId);

	/// <summary>Populates the key material fields from a <see cref="HapCredentials"/> instance.</summary>
	public void SetCredentials (HapCredentials credentials)
		{
		this.Ltpk = credentials.Ltpk;
		this.Ltsk = credentials.Ltsk;
		this.AtvId = credentials.AtvId;
		this.ClientId = credentials.ClientId;
		}
	}

/// <summary>
/// Persists paired-device credentials, the stable identifier, and device metadata as JSON,
/// one file per device, keyed by the device's stable unique id.
/// </summary>
public sealed class CredentialStore
	{
	private static readonly JsonSerializerOptions SerializerOptions = new ()
		{
		WriteIndented = true,
		};

	private readonly string _directory;

	/// <summary>Initializes a new instance of the <see cref="CredentialStore"/> class.</summary>
	/// <param name="directory">
	/// The directory credentials are stored in. Defaults to
	/// <c>%AppData%\AppleTvRemoteWpf\credentials</c>.
	/// </param>
	public CredentialStore (string? directory = null)
		{
		this._directory = directory ?? Path.Combine (
			Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
			"AppleTvRemoteWpf",
			"credentials");

		Directory.CreateDirectory (this._directory);
		}

	/// <summary>Loads a previously paired device's stored credentials, if present.</summary>
	/// <param name="uniqueId">The device's stable unique id.</param>
	/// <returns>The stored device, or <see langword="null"/> if no credentials are saved for it.</returns>
	public StoredDevice? Load (string uniqueId)
		{
		string path = this.GetPath (uniqueId);
		if (!File.Exists (path))
			{
			return null;
			}

		string json = File.ReadAllText (path);
		return JsonSerializer.Deserialize<StoredDevice> (json, SerializerOptions);
		}

	/// <summary>Saves a paired device's credentials.</summary>
	/// <param name="device">The device to persist.</param>
	public void Save (StoredDevice device)
		{
		string path = this.GetPath (device.UniqueId);
		string json = JsonSerializer.Serialize (device, SerializerOptions);
		File.WriteAllText (path, json);
		}

	/// <summary>Deletes any stored credentials for the given device (i.e. "forget" / unpair).</summary>
	/// <param name="uniqueId">The device's stable unique id.</param>
	public void Delete (string uniqueId)
		{
		string path = this.GetPath (uniqueId);
		if (File.Exists (path))
			{
			File.Delete (path);
			}
		}

	private string GetPath (string uniqueId)
		{
		string safeName = string.Join ("_", uniqueId.Split (Path.GetInvalidFileNameChars ()));
		return Path.Combine (this._directory, safeName + ".json");
		}
	}
