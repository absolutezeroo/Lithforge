using System.Collections.Generic;

namespace Lithforge.Voxel.Loot
{
    /// <summary>
    /// A function that modifies loot output (e.g. set count, apply enchantment).
    /// Applied after an entry is selected.
    /// </summary>
    public sealed class LootFunction
    {
        public string Type { get; set; }
        public Dictionary<string, string> Parameters { get; set; }

        public LootFunction()
        {
            Type = "";
            Parameters = new Dictionary<string, string>();
        }
    }
}
