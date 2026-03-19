using System;
using System.Collections.Generic;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Tracks <see cref="IDisposable" /> objects in initialization order and
    ///     disposes them in reverse order when the session ends.
    /// </summary>
    public sealed class SessionLifetimeTracker : IDisposable
    {
        /// <summary>LIFO stack of disposables for reverse-order cleanup.</summary>
        private readonly Stack<IDisposable> _disposables = new();

        /// <summary>Whether this tracker has already been disposed.</summary>
        private bool _disposed;

        /// <summary>
        ///     Disposes all tracked objects in reverse order, logging errors but
        ///     continuing through all items to prevent resource leaks.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            while (_disposables.Count > 0)
            {
                IDisposable disposable = _disposables.Pop();

                try
                {
                    disposable.Dispose();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError(
                        $"[Lithforge] Error disposing {disposable.GetType().Name}: {ex}");
                }
            }
        }

        /// <summary>
        ///     Registers a disposable to be cleaned up when the session ends.
        ///     Objects are disposed in reverse registration order (LIFO).
        /// </summary>
        public T Track<T>(T disposable) where T : IDisposable
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SessionLifetimeTracker));
            }

            _disposables.Push(disposable);

            return disposable;
        }
    }
}
