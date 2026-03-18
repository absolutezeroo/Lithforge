using System;
using System.Diagnostics;
using System.IO;

namespace Lithforge.Voxel.Storage
{
    public static class SessionLock
    {
        private const string LockFileName = "session.lock";
        private const int StaleThresholdMinutes = 10;

        public static string LockFilePath(string worldDir)
        {
            return Path.Combine(worldDir, LockFileName);
        }

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
