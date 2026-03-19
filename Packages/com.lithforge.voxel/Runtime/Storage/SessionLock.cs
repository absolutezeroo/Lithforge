using System;
using System.Diagnostics;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    /// <summary>
    ///     OS-level file locking to prevent multiple processes from opening the same world.
    ///     Uses exclusive FileShare.None on session.lock. Includes stale lock detection
    ///     via PID and timestamp checks.
    /// </summary>
    public static class SessionLock
    {
        /// <summary>Name of the lock file created in each world directory.</summary>
        private const string LockFileName = "session.lock";

        /// <summary>Minutes after which a lock with a dead PID is considered stale.</summary>
        private const int StaleThresholdMinutes = 10;

        /// <summary>Returns the full path to the session.lock file for a world directory.</summary>
        public static string LockFilePath(string worldDir)
        {
            return Path.Combine(worldDir, LockFileName);
        }

        /// <summary>
        ///     Acquires an exclusive file lock on the world directory.
        ///     Writes a UTC timestamp and PID to the lock file for stale detection.
        ///     Throws IOException if another process holds the lock.
        /// </summary>
        public static SessionLockHandle Acquire(string worldDir)
        {
            string lockPath = LockFilePath(worldDir);

            if (!Directory.Exists(worldDir))
            {
                Directory.CreateDirectory(worldDir);
            }

            FileStream fs = new(
                lockPath,
                FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.None);

            // Write timestamp and PID for stale detection
            StreamWriter writer = new(fs);
            writer.WriteLine(DateTime.UtcNow.ToString("o"));
            writer.WriteLine(Process.GetCurrentProcess().Id);
            writer.Flush();

            // Keep stream open — the OS file lock is held while the stream lives
            return new SessionLockHandle(fs);
        }

        /// <summary>Attempts to acquire the session lock. Returns false on IOException without throwing.</summary>
        public static bool TryAcquire(string worldDir, out SessionLockHandle handle)
        {
            handle = null;

            try
            {
                handle = Acquire(worldDir);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>Returns true if the world directory's session.lock is held by another process.</summary>
        public static bool IsLocked(string worldDir)
        {
            string lockPath = LockFilePath(worldDir);

            if (!File.Exists(lockPath))
            {
                return false;
            }

            try
            {
                using (FileStream fs = new(
                           lockPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.None))
                {
                    // If we can open exclusively, it's not locked
                    return false;
                }
            }
            catch (IOException)
            {
                // Cannot open exclusively — another process holds the lock
                return true;
            }
        }

        /// <summary>
        ///     Returns true if the lock file is stale (timestamp older than threshold
        ///     and the recorded PID no longer exists).
        /// </summary>
        public static bool IsStale(string worldDir)
        {
            string lockPath = LockFilePath(worldDir);

            if (!File.Exists(lockPath))
            {
                return false;
            }

            try
            {
                // Try to read the lock file non-exclusively
                using (FileStream fs = new(
                           lockPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite))
                using (StreamReader reader = new(fs))
                {
                    string timestampLine = reader.ReadLine();
                    string pidLine = reader.ReadLine();

                    if (DateTime.TryParse(timestampLine, out DateTime lockTime))
                    {
                        if ((DateTime.UtcNow - lockTime).TotalMinutes > StaleThresholdMinutes)
                        {
                            // Check if the PID is still running
                            if (int.TryParse(pidLine, out int pid))
                            {
                                try
                                {
                                    Process.GetProcessById(pid);
                                    return false; // Process still alive
                                }
                                catch (ArgumentException)
                                {
                                    return true; // Process no longer exists
                                }
                            }

                            return true; // Can't parse PID, assume stale
                        }
                    }
                }
            }
            catch
            {
                // Can't read — file is locked by another process, not stale
            }

            return false;
        }

        /// <summary>Forcibly deletes the session.lock file (best effort). Use only for stale locks.</summary>
        public static void ForceBreak(string worldDir)
        {
            string lockPath = LockFilePath(worldDir);

            if (File.Exists(lockPath))
            {
                try
                {
                    File.Delete(lockPath);
                }
                catch
                {
                    // Best effort
                }
            }
        }
    }
}
