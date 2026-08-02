// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Windows;

using AppleTvControlLibrary.Discovery.Companion;
using AppleTvControlLibrary.Remote.Wpf.ViewModels;

namespace AppleTvControlLibrary.Remote.Wpf.Views;

/// <summary>Code-behind for the main remote-control window.</summary>
public partial class MainWindow : Window
	{
	/// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
	public MainWindow ()
		{
		this.InitializeComponent ();

		MainViewModel viewModel = new MainViewModel
			{
			RequestPin = this.RequestPin,
			};
		this.DataContext = viewModel;
		this.Closed += (_, _) => viewModel.Dispose ();
		}

	private int? RequestPin (CompanionDiscoveryResult device)
		{
		PinEntryDialog dialog = new PinEntryDialog (device.Name)
			{
			Owner = this,
			};

		bool? result = dialog.ShowDialog ();
		return result == true ? dialog.EnteredPin : null;
		}
	}
