using System;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     RAII handle that holds an OS file lock on a world's session.lock file.
    ///     Disposing the handle releases the lock, allowing other processes to open the world.
    /// </summary>
    public sealed class SessionLockHandle : IDisposable
    {
        /// <summary>Underlying FileStream whose OS file lock prevents concurrent access.</summary>
        private FileStream _lockStream;

        /// <summary>Whether this handle has been disposed.</summary>
        private bool _disposed;

        /// <summary>Wraps an already-opened FileStream as a session lock handle.</summary>
        internal SessionLockHandle(FileStream lockStream)
        {
            _lockStream = lockStream;
        }

        /// <summary>Releases the OS file lock by closing and disposing the underlying FileStream.</summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                if (_lockStream != null)
                {
                    try
                    {
                        _lockStream.Close();
                        _lockStream.Dispose();
                    }
                    catch
                    {
                        // Best effort — OS releases file lock on stream close
                    }

                    _lockStream = null;
                }
            }
        }
    }
}
