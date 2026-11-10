// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System;
using System.Threading;

namespace System.Threading.Tasks;

// Focused test-only shim: retain modern bounded-wait call sites without adding
// runtime dependencies to the Apple TV library or changing its public API.
internal static class AppleFrameworkTaskWaitExtensions
	{
	internal static async Task WaitAsync (this Task task, TimeSpan timeout)
		{
		using var delay = new CancellationTokenSource ();
		if (await Task.WhenAny (task, Task.Delay (timeout, delay.Token)).ConfigureAwait (false) != task)
			throw new TimeoutException ("The fake protocol operation did not complete.");
		delay.Cancel ();
		await task.ConfigureAwait (false);
		}
	internal static async Task<T> WaitAsync<T> (this Task<T> task, TimeSpan timeout)
		{
		await ((Task)task).WaitAsync (timeout).ConfigureAwait (false);
		return await task.ConfigureAwait (false);
		}
	}
#endif