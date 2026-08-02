// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AppleTvControlLibrary.Remote.Wpf;

/// <summary>
/// Application entry point.
/// </summary>
public partial class App : Application
	{
	/// <inheritdoc/>
	protected override void OnStartup (StartupEventArgs e)
		{
		// Catch-all safety net: every exception that would otherwise crash the process
		// (UI thread, background threads, and unobserved Task faults) is logged here.
		// This does not replace per-operation logging elsewhere (CompanionProtocol,
		// CompanionApi, TcpCompanionTransport) - it exists so nothing ever falls through
		// to a silent/unlogged process crash.
		this.DispatcherUnhandledException += this.OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += this.OnAppDomainUnhandledException;
		TaskScheduler.UnobservedTaskException += this.OnUnobservedTaskException;

		base.OnStartup (e);
		}

	private void OnDispatcherUnhandledException (object sender, DispatcherUnhandledExceptionEventArgs e)
		{
		System.Diagnostics.Debug.WriteLine ($"[App] Unhandled UI-thread exception: {e.Exception}");
		MessageBox.Show (
			$"An unexpected error occurred:\n\n{e.Exception.Message}\n\nSee the debug output for details.",
			"AppleTv Remote - Unexpected Error",
			MessageBoxButton.OK,
			MessageBoxImage.Error);

		// Prevent the process from crashing; the error has been logged and reported above.
		e.Handled = true;
		}

	private void OnAppDomainUnhandledException (object sender, UnhandledExceptionEventArgs e)
		{
		// Exceptions on non-UI threads that are not caught anywhere are, unfortunately,
		// always fatal to the process (the CLR terminates it regardless of e.IsTerminating).
		// Logging here is the only chance to capture what happened before the crash.
		System.Diagnostics.Debug.WriteLine ($"[App] Unhandled exception (IsTerminating={e.IsTerminating}): {e.ExceptionObject}");
		}

	private void OnUnobservedTaskException (object? sender, UnobservedTaskExceptionEventArgs e)
		{
		System.Diagnostics.Debug.WriteLine ($"[App] Unobserved Task exception: {e.Exception}");

		// Task exceptions are not fatal by default on modern .NET, but mark it observed
		// so it definitely does not escalate.
		e.SetObserved ();
		}
	}
