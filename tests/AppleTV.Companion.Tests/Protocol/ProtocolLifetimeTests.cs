// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Protocol;

using NUnit.Framework;

namespace AppleTv.Companion.Tests.ProtocolTests;

[TestFixture]
public sealed class ProtocolLifetimeTests
	{
	[Test]
	public async Task AsyncDisposalWaitsForSenderAndQueuedSendsCancelWithoutReleasingDisposedGates ()
		{
		var connection = new CompanionConnection ();
		var protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
		var started = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource<bool> (TaskCreationOptions.RunContinuationsAsynchronously);
		int sends = 0;
		protocol.AsyncSender = async frame => { _ = Interlocked.Increment (ref sends); _ = started.TrySetResult (true); await release.Task; };
		Task active = protocol.SendOpackAsync (FrameType.U_OPACK, new Dictionary<string, object?> ());
		try
			{
			await started.Task.WaitAsync (TimeSpan.FromSeconds (10));
			Task queued = protocol.SendOpackAsync (FrameType.U_OPACK, new Dictionary<string, object?> ());
			Task stopped = protocol.DisposeAsync ().AsTask ();
			Assert.That (protocol.DisposeAsync ().AsTask (), Is.SameAs (stopped));
			Assert.That (stopped.IsCompleted, Is.False);
			Assert.That (connection.IsFaulted, Is.True);
			_ = release.TrySetResult (true);
			await Assert.CatchAsync<OperationCanceledException> (async () => await active);
			await Assert.CatchAsync<OperationCanceledException> (async () => await queued);
			await stopped.WaitAsync (TimeSpan.FromSeconds (10));
			Assert.That (sends, Is.EqualTo (1));
			await Assert.ThrowsAsync<ObjectDisposedException> (async () => await protocol.SendOpackAsync (FrameType.U_OPACK, new Dictionary<string, object?> ()));
			}
		finally { _ = release.TrySetResult (true); await protocol.DisposeAsync (); }
		}
	}