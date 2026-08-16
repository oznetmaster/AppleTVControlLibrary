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
	private const double CLICK_MOVEMENT_THRESHOLD = 8.0;

	private bool _isTouchpadDragging;
	private Point _touchpadDownPosition;
	private bool _touchpadMoved;
	private TextInputDialog? _textInputDialog;

	/// <summary>Initializes a new instance of the <see cref="MainWindow"/> class.</summary>
	public MainWindow ()
		{
		InitializeComponent ();

		MainViewModel viewModel = new MainViewModel
			{
			RequestPin = RequestPin,
			ShowTextInput = ShowTextInput,
			HideTextInput = HideTextInput,
			};
		DataContext = viewModel;
		Closed += (_, _) => viewModel.Dispose ();

		// Fire-and-forget: InitializeAsync reports its own status/errors via StatusMessage, and
		// Scan/Pair/Connect/Disconnect remain fully usable whether or not this succeeds.
		Loaded += async (_, _) => await viewModel.InitializeAsync ().ConfigureAwait (true);
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
		if (DataContext is not MainViewModel viewModel)
			{
			return;
			}

		if (_textInputDialog is not null)
			{
			_textInputDialog.SetTextWithoutNotifying (currentText ?? string.Empty);
			return;
			}

		TextInputDialog dialog = new TextInputDialog (currentText, viewModel.OnTextInputChanged)
			{
			Owner = this,
			};
		dialog.Closed += (_, _) => _textInputDialog = null;
		_textInputDialog = dialog;
		dialog.Show ();
		}

	private void HideTextInput ()
		{
		_textInputDialog?.Close ();
		_textInputDialog = null;
		}

	// A press+release with minimal movement in between is sent as a Companion touchpad "click"
	// (HidCommand.Select down/up plus a TouchAction.Click event - see CompanionApi.SendClick,
	// ported from pyatv/protocols/companion/api.py:373-393); a drag is reported as touch
	// Press (down) / Hold (move) / Release (up) so the device can distinguish a tap from a
	// swipe. A touch Press/Release pair alone does not cause tvOS to act on the tap.
	private void TouchpadSurface_MouseDown (object sender, MouseButtonEventArgs e)
		{
		if (DataContext is not MainViewModel viewModel || e.ChangedButton != MouseButton.Left)
			{
			return;
			}

		_isTouchpadDragging = true;
		_touchpadMoved = false;
		_touchpadDownPosition = e.GetPosition (TouchpadSurface);
		_ = TouchpadSurface.CaptureMouse ();

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (_touchpadDownPosition, TouchpadSurface.ActualWidth, TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Press);
		}

	private void TouchpadSurface_MouseMove (object sender, MouseEventArgs e)
		{
		if (!_isTouchpadDragging || DataContext is not MainViewModel viewModel)
			{
			return;
			}

		Point position = e.GetPosition (TouchpadSurface);
		if (!_touchpadMoved && (position - _touchpadDownPosition).Length > CLICK_MOVEMENT_THRESHOLD)
			{
			_touchpadMoved = true;
			}

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (position, TouchpadSurface.ActualWidth, TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Hold);
		}

	private void TouchpadSurface_MouseUp (object sender, MouseButtonEventArgs e)
		{
		if (!_isTouchpadDragging || DataContext is not MainViewModel viewModel || e.ChangedButton != MouseButton.Left)
			{
			return;
			}

		EndTouchpadDrag (viewModel, e.GetPosition (TouchpadSurface));
		}

	private void TouchpadSurface_MouseLeave (object sender, MouseEventArgs e)
		{
		if (!_isTouchpadDragging || DataContext is not MainViewModel viewModel)
			{
			return;
			}

		EndTouchpadDrag (viewModel, e.GetPosition (TouchpadSurface));
		}

	private void EndTouchpadDrag (MainViewModel viewModel, Point position)
		{
		_isTouchpadDragging = false;
		TouchpadSurface.ReleaseMouseCapture ();

		(int x, int y) = MainViewModel.TranslateTouchCoordinate (position, TouchpadSurface.ActualWidth, TouchpadSurface.ActualHeight);
		viewModel.SendTouchEvent (x, y, TouchAction.Release);

		if (!_touchpadMoved)
			{
			viewModel.SendTouchClick (InputAction.SingleTap);
			}
		}
	}
