namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Identifies the structural role a part plays within a modular tool.
    /// Each tool definition declares which part slots it requires; parts
    /// contribute stats (speed, durability, damage) based on their type.
    /// </summary>
    public enum ToolPartType : byte
    {
        /// <summary>No part (empty slot).</summary>
        None      = 0,

        /// <summary>Primary striking surface of pickaxes, axes, and hammers.</summary>
        Head      = 1,

        /// <summary>Grip shaft shared by most hand tools.</summary>
        Handle    = 2,

        /// <summary>Wrapping that connects head to handle, adding durability.</summary>
        Binding   = 3,

        /// <summary>Cutting edge for swords and cleavers.</summary>
        Blade     = 4,

        /// <summary>Cross-guard that protects the wielder's hand on swords.</summary>
        Guard     = 5,

        /// <summary>Piercing tip for spears and daggers.</summary>
        Point     = 6,

        /// <summary>Long pole for spears and halberds.</summary>
        Shaft     = 7,

        /// <summary>Flexible arms of a bow that store draw energy.</summary>
        Limbs     = 8,

        /// <summary>String connecting bow limbs that launches projectiles.</summary>
        BowString = 9,

        /// <summary>Ergonomic hold section for crossbows and shields.</summary>
        Grip      = 10,

        /// <summary>Shoulder brace for crossbows.</summary>
        Stock     = 11,

        /// <summary>Launching rail of a crossbow.</summary>
        Prod      = 12,

        /// <summary>Curved catch for fishing rods and grapples.</summary>
        Hook      = 13,

        /// <summary>Flexible line connecting a handle to a hook or weight.</summary>
        Cable     = 14,

        /// <summary>Internal moving parts (e.g., crossbow trigger assembly).</summary>
        Mechanism = 15,
    }
}
