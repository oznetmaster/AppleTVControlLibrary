// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AppleTvControlLibrary.Protocol;

// Counts holders and waiters separately from the protocol's serialization gates.
// Cancellation, transport callbacks and cleanup never run under the Monitor.
internal sealed class ProtocolLifetime (Action cleanup, Action signal) : IDisposable
	{
#pragma warning disable IDE0330 // Use the same Monitor implementation on processor Mono.
	private readonly object _sync = new ();
#pragma warning restore IDE0330
	private readonly CancellationTokenSource _stop = new ();
	private readonly TaskCompletionSource<bool> _drained = new (TaskCreationOptions.RunContinuationsAsynchronously);
	private int _active;
	private bool _stopped, _cancelled, _cleaned;
	private Exception? _failure;
	internal bool IsStopped
		{
		get
			{
			lock (_sync)
				return _stopped;
			}
		}
	internal Operation Enter (CancellationToken cancellationToken)
		{
		cancellationToken.ThrowIfCancellationRequested ();
		lock (_sync)
			{
#if NETFRAMEWORK
			if (_stopped) throw new ObjectDisposedException (nameof (CompanionProtocol));
#else
			ObjectDisposedException.ThrowIf (_stopped, typeof (CompanionProtocol));
#endif
			_active++;
			}
		CancellationTokenSource? linked = null;
		try
			{
			linked = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, _stop.Token);
			linked.Token.ThrowIfCancellationRequested ();
			return new (this, linked);
			}
		catch { linked?.Dispose (); Exit (); throw; }
		}
	public void Dispose () => _ = Stop ();
	internal Task Stop ()
		{
		lock (_sync)
			{
			if (_stopped)
				return _drained.Task;
			_stopped = true;
			}
		try
			{
			try
				{
				_stop.Cancel ();
				}
			catch (AggregateException error) { _failure = error; }
			try
				{
				signal ();
				}
			catch (Exception error) { _failure = _failure is null ? error : new AggregateException (_failure, error); }
			}
		finally
			{
			bool finish;
			lock (_sync)
				{
				_cancelled = true;
				finish = ClaimCleanup ();
				}
			if (finish)
				Cleanup ();
			}
		return _drained.Task;
		}
	private void Exit ()
		{
		bool finish;
		lock (_sync)
			{
			_active--;
			finish = ClaimCleanup ();
			}
		if (finish)
			Cleanup ();
		}
	private bool ClaimCleanup ()
		{
		if (!_stopped || !_cancelled || _active != 0 || _cleaned)
			return false;
		_cleaned = true;
		return true;
		}
	private void Cleanup ()
		{
		try
			{
			cleanup ();
			}
		catch (Exception error) { _failure = _failure is null ? error : new AggregateException (_failure, error); }
		finally { _stop.Dispose (); }
		if (_failure is null)
			_ = _drained.TrySetResult (true);
		else
			_ = _drained.TrySetException (_failure);
		}
	internal sealed class Operation (ProtocolLifetime owner, CancellationTokenSource linked) : IDisposable
		{
		private ProtocolLifetime? _owner = owner;
		internal CancellationToken Token { get; } = linked.Token;
		public void Dispose ()
			{
			ProtocolLifetime? active = Interlocked.Exchange (ref _owner, null);
			if (active is null)
				return;
			try
				{
				linked.Dispose ();
				}
			finally { active.Exit (); }
			}
		}
	}