using System;
using System.Windows.Input;

namespace AppleTvControlLibrary.Remote.Wpf.ViewModels;

/// <summary>A simple <see cref="ICommand"/> implementation delegating to plain delegates.</summary>
public sealed class RelayCommand : ICommand
	{
	private readonly Action<object?> _execute;
	private readonly Func<object?, bool>? _canExecute;

	/// <summary>Initializes a new instance of the <see cref="RelayCommand"/> class.</summary>
	/// <param name="execute">The action to run when the command is invoked.</param>
	/// <param name="canExecute">An optional predicate controlling whether the command can currently run.</param>
	public RelayCommand (Action<object?> execute, Func<object?, bool>? canExecute = null)
		{
		this._execute = execute ?? throw new ArgumentNullException (nameof (execute));
		this._canExecute = canExecute;
		}

	/// <summary>Initializes a new instance of the <see cref="RelayCommand"/> class with a parameterless action.</summary>
	/// <param name="execute">The action to run when the command is invoked.</param>
	/// <param name="canExecute">An optional predicate controlling whether the command can currently run.</param>
	public RelayCommand (Action execute, Func<bool>? canExecute = null)
		: this (_ => execute (), canExecute is null ? null : _ => canExecute ())
		{
		}

	/// <inheritdoc/>
	public event EventHandler? CanExecuteChanged;

	/// <inheritdoc/>
	public bool CanExecute (object? parameter) => this._canExecute?.Invoke (parameter) ?? true;

	/// <inheritdoc/>
	public void Execute (object? parameter) => this._execute (parameter);

	/// <summary>Raises <see cref="CanExecuteChanged"/> so bound controls re-evaluate <see cref="CanExecute"/>.</summary>
	public void RaiseCanExecuteChanged () => this.CanExecuteChanged?.Invoke (this, EventArgs.Empty);
	}
