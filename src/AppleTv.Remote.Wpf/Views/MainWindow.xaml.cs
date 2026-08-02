// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Windows;
using System.Windows.Input;

using AppleTvControlLibrary.Discovery.Companion;
using AppleTvControlLibrary.Remote.Wpf.ViewModels;

using InputAction = AppleTvControlLibrary.Protocol.InputAction;
using TouchAction = AppleTvControlLibrary.Protocol.TouchAction;

namespace AppleTvControlLibrary.Remote.Wpf.Views;

/// <summary>Code-behind for the main remote-control window.</summary>
public partial class MainWindow : Window
	{
	// A tap-vs-swipe threshold: movement below this distance (device-independent pixels)
	// between mouse-down and mouse-up is treated as a click rather than a swipe.
	private const double ClickMovementThreshold = 8.0;

	private bool _isTouchpadDragging;
	private Point _touchpadDownPosition;
	private bool _touchpadMoved;
	private TextInputDialog? _textInputDialog;

	/// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
	public MainWindow ()
		{
		this.InitializeComponent ();

		MainViewModel viewModel = new MainViewModel
			{
			RequestPin = this.RequestPin,
			ShowTextInput = this.ShowTextInput,
			HideTextInput = this.HideTextInput,
			};
		this.DataContext = viewModel;
		this.Closed += (_, _) => viewModel.Dispose ();

		// Fire-and-forget: InitializeAsync reports its own status/errors via StatusMessage, and
		// Scan/Pair/Connect/Disconnect remain fully usable whether or not this succeeds.
		this.Loaded += async (_, _) => await viewModel.InitializeAsync ().ConfigureAwait (true);
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

	// Non-modal (Show, not ShowDialog): the on-screen keyboard is a live session driven by
	// pushed _tiStarted/_tiStopped events, not a one-shot prompt, so the rest of the remote
	// must remain usable while the dialog is open.
	private void ShowTextInput (string? currentText)
		{
		if (this.DataContext is not MainViewModel viewModel)
			{
			return;
			}

		if (this._textInputDialog is not null)
			{
			this._textInputDialog.SetTextWithoutNotifying (currentText ?? string.Empty);
			return;
			}

		TextInputDialog dialog = new TextInputDialog (currentText, viewModel.OnTextInputChanged)
			{
			Owner = this,
			};
		dialog.Closed += (_, _) => this._textInputDialog = null;
		this._textInputDialog = dialog;
		dialog.Show ();
		}

	private void HideTextInput ()
		{
		this._textInputDialog?.Close ();
		this._textInputDialog = null;
		}

	// A press+release with minimal movement in between is sent as a Companion touchpad "click"
	// (HidCommand.Select down/up plus a TouchAction.Click event - see CompanionApi.SendClick,
	// ported from pyatv/protocols/companion/api.py:373-393); a drag is reported as touch
	// Press (down) / Hold (move) / Release (up) so the device can distinguish a tap from a
	// swipe. A touch Press/Release pair alone does not cause tvOS to act on the tap.
	private void TouchpadSurface_MouseDown (object sender, MouseButtonEventArgs e)
		{
		if (this.DataContext is not MainViewModel viewModel || e.ChangedButton != MouseButton.Left)
			{
			return;
			}

		this._isTouchpadDragging = true;
		this._touchpadMoved = false;
		this._touchpadDownPosition = e.GetPosition (this.TouchpadSurface);
		this.TouchpadSurface.CaptureMouse ();

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (this._touchpadDownPosition, this.TouchpadSurface.ActualWidth, this.TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Press);
		}

	private void TouchpadSurface_MouseMove (object sender, MouseEventArgs e)
		{
		if (!this._isTouchpadDragging || this.DataContext is not MainViewModel viewModel)
			{
			return;
			}

		Point position = e.GetPosition (this.TouchpadSurface);
		if (!this._touchpadMoved && (position - this._touchpadDownPosition).Length > ClickMovementThreshold)
			{
			this._touchpadMoved = true;
			}

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (position, this.TouchpadSurface.ActualWidth, this.TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Hold);
		}

	private void TouchpadSurface_MouseUp (object sender, MouseButtonEventArgs e)
		{
		if (!this._isTouchpadDragging || this.DataContext is not MainViewModel viewModel || e.ChangedButton != MouseButton.Left)
			{
			return;
			}

		this.EndTouchpadDrag (viewModel, e.GetPosition (this.TouchpadSurface));
		}

	private void TouchpadSurface_MouseLeave (object sender, MouseEventArgs e)
		{
		if (!this._isTouchpadDragging || this.DataContext is not MainViewModel viewModel)
			{
			return;
			}

		this.EndTouchpadDrag (viewModel, e.GetPosition (this.TouchpadSurface));
		}

	private void EndTouchpadDrag (MainViewModel viewModel, Point position)
		{
		this._isTouchpadDragging = false;
		this.TouchpadSurface.ReleaseMouseCapture ();

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (position, this.TouchpadSurface.ActualWidth, this.TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Release);

		if (!this._touchpadMoved)
			{
			viewModel.SendTouchClick (InputAction.SingleTap);
			}
		}
	}
