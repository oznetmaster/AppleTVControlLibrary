// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Discovery.AirPlay;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

/// <summary>
/// The on-disk representation of a paired MRP-over-AirPlay device: its HAP credentials plus the
/// stable unique identifier and device metadata that must survive restarts.
/// </summary>
/// <remarks>
/// Unlike Companion Link, MRP has no <c>_i</c>-style stable client identifier requirement;
/// the identity that must be preserved here is simply the SRP <c>PairingId</c> used during
/// pair-setup, so that pair-verify on a later connection presents the same client identity
/// the device originally paired with (pyatv/protocols/mrp/protocol.py — line 137-140 as of
/// pyatv 0.18.0: <c>self.srp.pairing_id = self.service.credentials.client_id</c>).
/// </remarks>
public sealed class StoredDevice
	{
	/// <summary>Gets or sets the MRP stable unique id (the mDNS <c>UniqueIdentifier</c> value).</summary>
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

	/// <summary>Gets or sets the last known MRP port of the device.</summary>
	public int Port
		{
		get;
		set;
		}

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

	/// <summary>
	/// Gets or sets this client's identifier, reused as the SRP <c>PairingId</c> on subsequent
	/// pair-verify connections (pyatv/protocols/mrp/protocol.py — line 137-140 as of pyatv 0.18.0).
	/// </summary>
	public byte[] ClientId
		{
		get;
		set;
		} = Array.Empty<byte> ();

	/// <summary>
	/// Gets or sets a value indicating whether this device should be automatically connected to
	/// on the next application startup, for ease of testing. Only one stored device should have
	/// this set at a time; <see cref="CredentialStore.SetAutoConnect"/> enforces that.
	/// </summary>
	public bool AutoConnect
		{
		get;
		set;
		}

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
/// Persists paired MRP-device credentials, the stable unique id, and device metadata as JSON,
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
	/// <c>%AppData%\AppleTvRemoteMrpWpf\credentials</c>.
	/// </param>
	public CredentialStore (string? directory = null)
		{
		this._directory = directory ?? Path.Combine (
			Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData),
			"AppleTvRemoteMrpWpf",
			"credentials");

		Directory.CreateDirectory (this._directory);
		}

	/// <summary>Loads a previously paired device's stored credentials, if present.</summary>
	/// <param name="uniqueId">The device's stable unique id.</param>
	/// <returns>The stored device, or <see langword="null"/> if no credentials are saved for it.</returns>
	public StoredDevice? Load (string uniqueId)
		{
		string path = this.GetPath (uniqueId);
		System.Diagnostics.Debug.WriteLine ($"[CredentialStore] Load: uniqueId='{uniqueId}' path='{path}' exists={File.Exists (path)}");
		if (!File.Exists (path))
			{
			return null;
			}

		string json = File.ReadAllText (path);
		StoredDevice? device = JsonSerializer.Deserialize<StoredDevice> (json, SerializerOptions);
		// Older credential files may have been saved before mDNS collision suffixes
		// (e.g. "Office (2)") were stripped at pairing time; normalize on load so
		// stale files don't keep showing the suffix forever.
		if (device is not null)
			{
			device.Name = AirPlayServiceInfo.RemoveNameCollisionSuffix (device.Name);
			}

		return device;
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

	/// <summary>Loads every stored device currently persisted.</summary>
	public IReadOnlyList<StoredDevice> LoadAll ()
		{
		List<StoredDevice> devices = new ();
		foreach (string path in Directory.EnumerateFiles (this._directory, "*.json"))
			{
			try
				{
				string json = File.ReadAllText (path);
				StoredDevice? device = JsonSerializer.Deserialize<StoredDevice> (json, SerializerOptions);
				if (device is not null)
					{
					device.Name = AirPlayServiceInfo.RemoveNameCollisionSuffix (device.Name);
					System.Diagnostics.Debug.WriteLine ($"[CredentialStore] LoadAll: found file='{path}' UniqueId='{device.UniqueId}' Name='{device.Name}'");
					devices.Add (device);
					}
				}
			catch (JsonException)
				{
				// Ignore unreadable/corrupt files rather than failing the whole load.
				}
			}

		return devices;
		}

	/// <summary>
	/// Marks <paramref name="uniqueId"/> as the device to automatically connect to at startup,
	/// clearing the flag on every other stored device so at most one is ever marked.
	/// </summary>
	/// <param name="uniqueId">
	/// The device to enable auto-connect for, or <see langword="null"/> to clear auto-connect
	/// entirely.
	/// </param>
	public void SetAutoConnect (string? uniqueId)
		{
		foreach (StoredDevice device in this.LoadAll ())
			{
			bool shouldAutoConnect = uniqueId is not null && device.UniqueId == uniqueId;
			if (device.AutoConnect != shouldAutoConnect)
				{
				device.AutoConnect = shouldAutoConnect;
				this.Save (device);
				}
			}
		}

	/// <summary>Returns the stored device currently marked for auto-connect, if any.</summary>
	public StoredDevice? LoadAutoConnectDevice ()
		{
		return this.LoadAll ().FirstOrDefault (d => d.AutoConnect);
		}

	private string GetPath (string uniqueId)
		{
		string safeName = string.Join ("_", uniqueId.Split (Path.GetInvalidFileNameChars ()));
		return Path.Combine (this._directory, safeName + ".json");
		}
	}
