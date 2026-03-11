using System.Collections.Generic;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// A function that modifies loot output (e.g. set count, apply enchantment).
    /// Applied after an entry is selected.
    /// Int values are pre-parsed at load time to avoid int.TryParse in hot paths.
    /// </summary>
    public sealed class LootFunction
    {
        public string Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        /// <summary>Pre-parsed "min" parameter. int.MinValue if not present or invalid.</summary>
        public int MinValue { get; private set; }

        /// <summary>Pre-parsed "max" parameter. int.MinValue if not present or invalid.</summary>
        public int MaxValue { get; private set; }

        /// <summary>Pre-parsed "count" parameter. int.MinValue if not present or invalid.</summary>
        public int CountValue { get; private set; }

        public LootFunction()
        {
            Type = "";
            Parameters = new Dictionary<string, string>();
            MinValue = int.MinValue;
            MaxValue = int.MinValue;
            CountValue = int.MinValue;
        }

        /// <summary>
        /// Pre-parses int values from Parameters. Call once after Parameters are set
        /// (e.g. during ContentPipeline loading).
        /// </summary>
        public void PreParseValues()
        {
            if (Parameters.TryGetValue("min", out string minStr) && int.TryParse(minStr, out int min))
            {
                MinValue = min;
            }

            if (Parameters.TryGetValue("max", out string maxStr) && int.TryParse(maxStr, out int max))
            {
                MaxValue = max;
            }

            if (Parameters.TryGetValue("count", out string countStr) && int.TryParse(countStr, out int count))
            {
                CountValue = count;
            }
        }
    }
}
