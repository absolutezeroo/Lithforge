using System.Text;

namespace Lithforge.Runtime.Debug
{
    /// <summary>
    /// Zero-allocation text formatting utilities shared by F3DebugOverlay and BenchmarkCsvWriter.
    /// Migrated and extended from the original DebugOverlayHUD manual formatters.
    /// </summary>
    public static class DebugTextUtil
    {
        /// <summary>
        /// Pre-cached integer strings for values 0–9999. Allocated once at static init.
        /// </summary>
        public static readonly string[] IntCache = new string[10000];

        static DebugTextUtil()
        {
            for (int i = 0; i < IntCache.Length; i++)
            {
                IntCache[i] = i.ToString();
            }
        }

        /// <summary>
        /// Returns a cached string for integers 0–9999, or calls ToString() for values outside range.
        /// </summary>
        public static string CachedInt(int value)
        {
            if (value >= 0 && value < IntCache.Length)
            {
                return IntCache[value];
            }

            return value.ToString();
        }

        /// <summary>
        /// Appends a float with 1 decimal place without string.Format allocation.
        /// </summary>
        public static void AppendFloat1(StringBuilder sb, float value)
        {
            if (value < 0f)
            {
                sb.Append('-');
                value = -value;
            }

            int whole = (int)value;
            int frac = (int)((value - whole) * 10f + 0.5f);

            if (frac >= 10)
            {
                whole++;
                frac -= 10;
            }

            sb.Append(whole);
            sb.Append('.');
            sb.Append(frac);
        }

        /// <summary>
        /// Appends a float with 2 decimal places followed by "ms".
        /// </summary>
        public static void AppendMs(StringBuilder sb, float ms)
        {
            if (ms < 0f)
            {
                sb.Append('-');
                ms = -ms;
            }

            int whole = (int)ms;
            int frac = (int)((ms - whole) * 100f + 0.5f);

            if (frac >= 100)
            {
                whole++;
                frac -= 100;
            }

            sb.Append(whole);
            sb.Append('.');

            if (frac < 10)
            {
                sb.Append('0');
            }

            sb.Append(frac);
            sb.Append("ms");
        }

        /// <summary>
        /// Appends a byte count in human-readable form (B, KB, MB).
        /// </summary>
        public static void AppendBytes(StringBuilder sb, long bytes)
        {
            if (bytes < 1024)
            {
                sb.Append(bytes);
                sb.Append(" B");
            }
            else if (bytes < 1024 * 1024)
            {
                sb.Append(bytes / 1024);
                sb.Append(" KB");
            }
            else
            {
                int mb = (int)(bytes / (1024 * 1024));
                int remainder = (int)((bytes % (1024 * 1024)) / 104858);
                sb.Append(mb);
                sb.Append('.');
                sb.Append(remainder);
                sb.Append(" MB");
            }
        }

        /// <summary>
        /// Appends a vertex count in human-readable form (raw, K, M).
        /// </summary>
        public static void AppendVertCount(StringBuilder sb, int count)
        {
            if (count < 1000)
            {
                sb.Append(count);
            }
            else if (count < 1000000)
            {
                sb.Append(count / 1000);
                sb.Append('.');
                sb.Append((count % 1000) / 100);
                sb.Append('K');
            }
            else
            {
                sb.Append(count / 1000000);
                sb.Append('.');
                sb.Append((count % 1000000) / 100000);
                sb.Append('M');
            }
        }

        /// <summary>
        /// Appends an integer, using the pre-cached string if in range.
        /// </summary>
        public static void AppendInt(StringBuilder sb, int value)
        {
            if (value >= 0 && value < IntCache.Length)
            {
                sb.Append(IntCache[value]);
            }
            else
            {
                sb.Append(value);
            }
        }
    }
}
