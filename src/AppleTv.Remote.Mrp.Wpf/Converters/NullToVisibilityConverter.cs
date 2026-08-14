// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.Converters;

/// <summary>Converts a value to <see cref="Visibility.Visible"/> when non-null, or <see cref="Visibility.Collapsed"/> when null.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
	{
	/// <inheritdoc/>
	public object Convert (object? value, Type targetType, object? parameter, CultureInfo culture)
		{
		return value is null ? Visibility.Collapsed : Visibility.Visible;
		}

	/// <inheritdoc/>
	public object ConvertBack (object? value, Type targetType, object? parameter, CultureInfo culture)
		{
		throw new NotSupportedException ();
		}
	}
