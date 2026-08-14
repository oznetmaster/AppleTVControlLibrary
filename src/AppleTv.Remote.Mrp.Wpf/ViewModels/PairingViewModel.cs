// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

/// <summary>View model backing the PIN entry dialog shown during pairing.</summary>
public sealed class PairingViewModel : ViewModelBase
	{
	private string _pin = string.Empty;

	/// <summary>Gets or sets the display name of the device being paired, shown to the user.</summary>
	public string DeviceName
		{
		get;
		set;
		} = string.Empty;

	/// <summary>Gets or sets the PIN entered by the user.</summary>
	public string Pin
		{
		get => this._pin;
		set => this.SetProperty (ref this._pin, value);
		}

	/// <summary>Attempts to parse <see cref="Pin"/> as the numeric PIN code required by pair-setup.</summary>
	/// <param name="pin">The parsed PIN, if successful.</param>
	/// <returns><see langword="true"/> if <see cref="Pin"/> is a valid PIN.</returns>
	public bool TryGetPin (out int pin) => int.TryParse (this.Pin, out pin);
	}
