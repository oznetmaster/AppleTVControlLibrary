// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Net;

using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

/// <summary>
/// Wraps an <see cref="AirPlayDiscoveryResult"/> (MRP tunneled over AirPlay) with whether
/// credentials for it are already persisted, so the device list can indicate which devices do
/// not need to be paired again.
/// </summary>
public sealed class DeviceListItem : ViewModelBase
	{
	private bool _isPaired;

	private DeviceListItem (
		string name,
		IPAddress? address,
		int port,
		string? uniqueId,
		AirPlayDiscoveryResult airPlayDevice,
		bool isPaired)
		{
		this.Name = name;
		this.Address = address;
		this.Port = port;
		this.UniqueId = uniqueId;
		this.AirPlayDevice = airPlayDevice;
		this._isPaired = isPaired;
		}

	/// <summary>Creates a <see cref="DeviceListItem"/> for a device discovered via AirPlay.</summary>
	/// <param name="device">The discovered device.</param>
	/// <param name="isPaired">Whether credentials for this device are already stored.</param>
	public static DeviceListItem FromAirPlay (AirPlayDiscoveryResult device, bool isPaired)
		{
		return new DeviceListItem (
			device.Name, device.Address, device.Port, device.UniqueId, device, isPaired);
		}

	/// <summary>Gets the underlying AirPlay discovery result.</summary>
	public AirPlayDiscoveryResult AirPlayDevice
		{
		get;
		}

	/// <summary>Gets the device's display name.</summary>
	public string Name
		{
		get;
		}

	/// <summary>Gets the resolved address of the device, if known.</summary>
	public IPAddress? Address
		{
		get;
		}

	/// <summary>Gets the device's port for the relevant transport.</summary>
	public int Port
		{
		get;
		}

	/// <summary>Gets a stable unique identifier for the device, if known.</summary>
	public string? UniqueId
		{
		get;
		}

	/// <summary>Gets or sets a value indicating whether credentials for this device are already stored.</summary>
	public bool IsPaired
		{
		get => this._isPaired;
		set
			{
			if (this.SetProperty (ref this._isPaired, value))
				{
				this.OnPropertyChanged (nameof (this.DisplayName));
				}
			}
		}

	/// <summary>Gets the text shown in the device list.</summary>
	public string DisplayName => AirPlayServiceInfo.RemoveNameCollisionSuffix (this.Name);
	}

