using System;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    public sealed class SessionLockHandle : IDisposable
    {
        private FileStream _lockStream;
        private bool _disposed;

        internal SessionLockHandle(FileStream lockStream)
        {
            _lockStream = lockStream;
        }

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
