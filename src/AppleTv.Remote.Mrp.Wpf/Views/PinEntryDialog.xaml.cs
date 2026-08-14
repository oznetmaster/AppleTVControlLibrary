// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Windows;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.Views;

/// <summary>Simple modal dialog collecting the pair-setup PIN code shown on the TV.</summary>
public partial class PinEntryDialog : Window
	{
	/// <summary>Initializes a new instance of the <see cref="PinEntryDialog"/> class.</summary>
	/// <param name="deviceName">The name of the device being paired, shown in the prompt.</param>
	public PinEntryDialog (string deviceName)
		{
		this.InitializeComponent ();
		this.PromptText.Text = $"Enter the PIN shown on \"{deviceName}\":";
		}

	/// <summary>Gets the PIN entered by the user, if <see cref="Window.DialogResult"/> is <see langword="true"/>.</summary>
	public int? EnteredPin
		{
		get;
		private set;
		}

	private void OnOkClick (object sender, RoutedEventArgs e)
		{
		if (!int.TryParse (this.PinTextBox.Text, out int pin))
			{
			MessageBox.Show (this, "Please enter a valid numeric PIN.", "Invalid PIN", MessageBoxButton.OK, MessageBoxImage.Warning);
			return;
			}

		this.EnteredPin = pin;
		this.DialogResult = true;
		}
	}
