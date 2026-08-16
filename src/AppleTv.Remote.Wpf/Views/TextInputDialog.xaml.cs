// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.ComponentModel;
using System.Windows;

namespace AppleTvControlLibrary.Remote.Wpf.Views;

/// <summary>
/// Non-modal dialog shown while the Apple TV has an on-screen keyboard focused (RTI text
/// input). Unlike <see cref="PinEntryDialog"/>, this mirrors a real text field: every
/// keystroke is forwarded to the device immediately via the constructor's
/// <c>textChangedCallback</c> parameter rather than being collected and submitted once.
/// The dialog is expected to be closed
/// either by the user or by the owner when the device reports the field lost focus
/// (<c>_tiStopped</c>).
/// </summary>
public partial class TextInputDialog : Window
	{
	private readonly Action<string> _textChangedCallback;
	private bool _suppressTextChanged;

	/// <summary>Initializes a new instance of the <see cref="TextInputDialog"/> class.</summary>
	/// <param name="initialText">The device's current keyboard text, if any.</param>
	/// <param name="textChangedCallback">
	/// Invoked with the dialog's current text every time the user edits it, so it can be
	/// forwarded to the device as it is typed (just like a real keyboard).
	/// </param>
	public TextInputDialog (string? initialText, Action<string> textChangedCallback)
		{
		InitializeComponent ();
		_textChangedCallback = textChangedCallback;

		_suppressTextChanged = true;
		InputTextBox.Text = initialText ?? string.Empty;
		InputTextBox.SelectionStart = InputTextBox.Text.Length;
		_suppressTextChanged = false;
		}

	/// <summary>
	/// Updates the displayed text without forwarding it back to the device, e.g. when the
	/// device itself reports a text change out-of-band.
	/// </summary>
	/// <param name="text">The text to display.</param>
	public void SetTextWithoutNotifying (string text)
		{
		_suppressTextChanged = true;
		try
			{
			int caret = InputTextBox.SelectionStart;
			InputTextBox.Text = text;
			InputTextBox.SelectionStart = Math.Min (caret, text.Length);
			}
		finally
			{
			_suppressTextChanged = false;
			}
		}

	private void OnInputTextChanged (object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
		if (_suppressTextChanged)
			{
			return;
			}

		_textChangedCallback (InputTextBox.Text);
		}

	private void OnDoneClick (object sender, RoutedEventArgs e) => Close ();

	private void OnCancelClick (object sender, RoutedEventArgs e) => Close ();

	private void OnClosing (object? sender, CancelEventArgs e)
		{
		// Nothing to veto; present for symmetry with future confirmation prompts.
		}
	}
