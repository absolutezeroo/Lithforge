using Lithforge.Core.Data;

namespace Lithforge.Item
{
    /// <summary>
    /// A single modifier socket on a tool or item. Modifiers grant passive
    /// effects (e.g., fortune, silk touch) and can be leveled up.
    /// </summary>
    public struct ModifierSlot
    {
        /// <summary>Whether a modifier is installed in this slot.</summary>
        public bool IsOccupied;

        /// <summary>The "namespace:name" identifier of the installed modifier, if any.</summary>
        public ResourceId ModifierId;

        /// <summary>Current level of the modifier (1-based). Higher levels amplify the effect.</summary>
        public int Level;
    }
}
