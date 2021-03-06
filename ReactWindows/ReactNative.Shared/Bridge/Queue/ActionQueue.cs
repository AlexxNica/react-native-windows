using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace ReactNative.Bridge.Queue
{
    /// <summary>
    /// A queue for executing actions in order.
    /// </summary>
    public class ActionQueue : IActionQueue
    {
        private readonly object _disposeGate = new object();
        private readonly CompositeDisposable _disposable = new CompositeDisposable(2);
        private readonly ThreadLocal<bool> _threadLocal = new ThreadLocal<bool>();
        private readonly Action<Exception> _onError;
        private readonly IObserver<Action> _dispatchObserver;

        private bool _isDisposed = false;

        /// <summary>
        /// Creates an action queue.
        /// </summary>
        /// <param name="onError">The error handler.</param>
        public ActionQueue(Action<Exception> onError)
            : this(onError, Scheduler.Default)
        {
        }

        /// <summary>
        /// Creates an action queue where the actions are performed on the
        /// given scheduler.
        /// </summary>
        /// <param name="onError">The error handler.</param>
        /// <param name="scheduler">The scheduler.</param>
        public ActionQueue(Action<Exception> onError, IScheduler scheduler)
        {
            if (onError == null)
                throw new ArgumentNullException(nameof(onError));
            if (scheduler == null)
                throw new ArgumentNullException(nameof(scheduler));

            _onError = onError;
            _dispatchObserver = SetupSubscription(scheduler);
        }

        /// <summary>
        /// Dispatch an action to the queue.
        /// </summary>
        /// <param name="action">The action.</param>
        /// <remarks>
        /// Returns immediately.
        /// </remarks>
        public void Dispatch(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (_disposeGate)
            {
                if (!_isDisposed)
                {
                    _dispatchObserver.OnNext(action);
                }
            }
        }

        /// <summary>
        /// Checks if the current thread is running in the context of this
        /// action queue.
        /// </summary>
        /// <returns>
        /// <code>true</code> if the current thread is running an action
        /// dispatched by this action queue, otherwise <code>false</code>.
        /// </returns>
        public bool IsOnThread()
        {
            return _threadLocal.Value;
        }

        /// <summary>
        /// Disposes the action queue.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Disposes the action queue.
        /// </summary>
        /// <param name="disposing">
        /// <code>true</code> if disposing directly, <code>false</code>
        /// if called by finalizer.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    lock (_disposeGate)
                    {
                        _disposable.Dispose();
                    }
                }

                _isDisposed = true;
            }
        }

        private IObserver<Action> SetupSubscription(IScheduler scheduler)
        {
            var subject = new Subject<Action>();
            var observer = Observer.Create<Action>(OnNext);
            var subscription = subject.ObserveOn(scheduler).Subscribe(observer);
            _disposable.Add(subscription);
            _disposable.Add(subject);
            return subject;
        }

        private void OnNext(Action action)
        {
            _threadLocal.Value = true;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _onError(ex);
            }
            finally
            {
                _threadLocal.Value = false;
            }
        }
    }
}
