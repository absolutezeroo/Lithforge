using System.Collections.Generic;

namespace Lithforge.Item.Loot
{
    /// <summary>
    /// A condition that must be met for a loot entry to be selected.
    /// Evaluated at resolution time against the loot context.
    /// </summary>
    public sealed class LootCondition
    {
        public string Type { get; set; } = "";
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
