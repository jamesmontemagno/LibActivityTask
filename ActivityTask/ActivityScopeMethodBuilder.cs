﻿using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Neteril.Android
{
	public class ActivityScopeMethodBuilder
	{
		static ActivityScope currentScope;

		IAsyncStateMachine stateMachine;
		SynchronizationContext syncContext;
		ActivityScope scope;
		ActivityTask task;

		public static ActivityScopeMethodBuilder Create () => new ActivityScopeMethodBuilder (SynchronizationContext.Current, currentScope);

		ActivityScopeMethodBuilder (SynchronizationContext synchronizationContext, ActivityScope scope)
		{
			this.syncContext = synchronizationContext;
			this.scope = scope;
			this.task = new ActivityTask ();
			if (scope == null)
				throw new ArgumentNullException (nameof (scope), "A valid scope is required");
			if (syncContext != null)
				syncContext.OperationStarted ();
		}

		public static void SetCurrentScope (ActivityScope scope)
		{
			if (scope != null && currentScope != null)
				throw new InvalidOperationException ("Another scope is already registered");
			currentScope = scope;
		}

		public void Start<TStateMachine> (ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
		{
			stateMachine.MoveNext ();
		}

		public ActivityTask Task => task;

		public void SetResult ()
		{
			if (syncContext != null)
				syncContext.OperationCompleted ();
			scope = null;
			task.Completion.SetResult (default (VoidTaskResult));
		}

		public void SetException (Exception ex)
		{
			task.Completion.SetException (ex);
			if (syncContext != null)
				syncContext.OperationCompleted ();
		}

		public void SetStateMachine (IAsyncStateMachine stateMachine)
		{
			this.stateMachine = stateMachine;
		}

		public void AwaitOnCompleted<TAwaiter, TStateMachine> (ref TAwaiter awaiter, ref TStateMachine stateMachine)
		   where TAwaiter : INotifyCompletion
		   where TStateMachine : IAsyncStateMachine
		{
			var callback = GetCompletionAction<TStateMachine> (ref stateMachine);
			awaiter.OnCompleted (callback);
		}

		public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine> (ref TAwaiter awaiter, ref TStateMachine stateMachine)
		   where TAwaiter : ICriticalNotifyCompletion
		   where TStateMachine : IAsyncStateMachine
		{
			AwaitOnCompleted (ref awaiter, ref stateMachine);
		}

		Action GetCompletionAction<TStateMachine> (ref TStateMachine machine) where TStateMachine : IAsyncStateMachine
		{
			// If this is our first await, such that we've not yet boxed the state machine, do so now.
			if (stateMachine == null) {
				stateMachine = (IAsyncStateMachine)machine;
				stateMachine.SetStateMachine (stateMachine);
			}
			var runner = new Runner (stateMachine, scope);
			return new Action (runner.Run);
		}

		sealed class Runner
		{
			IAsyncStateMachine machine;
			ActivityScope scope;

			internal Runner (IAsyncStateMachine machine, ActivityScope scope)
			{
				this.machine = machine;
				this.scope = scope;
			}

			public void Run ()
			{
				if (!scope.IsUnavailable)
					machine.MoveNext ();
				else
					scope.OnCompleted (Run);
			}
		}
	}
}
