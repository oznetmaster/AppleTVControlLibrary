// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Windows;

using AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.Views;

/// <summary>Code-behind for the main MRP remote-control window.</summary>
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

		// Fire-and-forget: InitializeAsync reports its own status/errors via StatusMessage, and
		// Scan/Pair/Connect/Disconnect remain fully usable whether or not this succeeds.
		// Loaded can fire more than once for the same Window instance (e.g. when the visual
		// tree is re-hooked), so unsubscribe immediately to guarantee a single auto-connect
		// attempt; without this, a second InitializeAsync call races the first one and its
		// resulting "Already connected" failure clobbers the success status message.
		this.Loaded += this.OnLoaded;
		}

	private async void OnLoaded (object sender, RoutedEventArgs e)
		{
		this.Loaded -= this.OnLoaded;
		await ((MainViewModel)this.DataContext).InitializeAsync ().ConfigureAwait (true);
		}

	private int? RequestPin (string deviceName)
		{
		PinEntryDialog dialog = new PinEntryDialog (deviceName)
			{
			Owner = this,
			};

		bool? result = dialog.ShowDialog ();
		return result == true ? dialog.EnteredPin : null;
		}
	}

