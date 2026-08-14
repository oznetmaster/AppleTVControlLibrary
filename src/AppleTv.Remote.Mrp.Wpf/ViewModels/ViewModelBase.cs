// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

/// <summary>Base class implementing <see cref="INotifyPropertyChanged"/> boilerplate.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
	{
	/// <inheritdoc/>
	public event PropertyChangedEventHandler? PropertyChanged;

	/// <summary>Raises <see cref="PropertyChanged"/> for the given property.</summary>
	/// <param name="propertyName">The name of the property that changed.</param>
	protected void OnPropertyChanged ([CallerMemberName] string? propertyName = null)
		{
		this.PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (propertyName));
		}

	/// <summary>Sets a backing field and raises <see cref="PropertyChanged"/> if the value changed.</summary>
	/// <typeparam name="T">The property type.</typeparam>
	/// <param name="field">The backing field.</param>
	/// <param name="value">The new value.</param>
	/// <param name="propertyName">The name of the property that changed.</param>
	/// <returns><see langword="true"/> if the value changed.</returns>
	protected bool SetProperty<T> (ref T field, T value, [CallerMemberName] string? propertyName = null)
		{
		if (Equals (field, value))
			{
			return false;
			}

		field = value;
		this.OnPropertyChanged (propertyName);
		return true;
		}
	}
