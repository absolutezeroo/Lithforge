using System.Collections.Generic;

using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    /// <summary>
    ///     Gameplay tuning: inventory layout, crafting grid dimensions, and items granted on first spawn.
    /// </summary>
    /// <remarks>
    ///     Slot counts here are advisory -- the authoritative runtime constants live on
    ///     <c>Inventory.SlotCount</c> and <c>Inventory.HotbarSize</c>.
    ///     Loaded from <c>Resources/Settings/GameplaySettings</c>.
    /// </remarks>
    [CreateAssetMenu(fileName = "GameplaySettings", menuName = "Lithforge/Settings/Gameplay", order = 5)]
    public sealed class GameplaySettings : ScriptableObject
    {
        /// <summary>Total number of inventory slots including hotbar (advisory, see remarks on class).</summary>
        [Header("Inventory"), Tooltip("Total inventory slot count (advisory — Inventory.SlotCount is the runtime constant)"), Min(9), SerializeField]
         private int inventorySlotCount = 36;

        /// <summary>Number of slots visible in the bottom hotbar row (advisory, see remarks on class).</summary>
        [Tooltip("Hotbar size (advisory — Inventory.HotbarSize is the runtime constant)"), Min(1), SerializeField]
         private int hotbarSize = 9;

        /// <summary>Side length of the player's personal crafting grid (2 = 2x2, 3 = 3x3 for crafting tables).</summary>
        [Tooltip("Player crafting grid dimension (2 = 2x2)"), Range(2, 3), SerializeField]
         private int craftingGridSize = 2;

        /// <summary>Items placed into the player's inventory on their very first spawn in a new world.</summary>
        [Header("Starting Items"), Tooltip("Items granted to the player at first spawn"), SerializeField]
         private StartingItemEntry[] startingItems =
        {
            new()
            {
                itemNamespace = "lithforge", itemName = "cobblestone", count = 64,
            },
        };

        /// <inheritdoc cref="inventorySlotCount" />
        public int InventorySlotCount
        {
            get { return inventorySlotCount; }
        }

        /// <inheritdoc cref="hotbarSize" />
        public int HotbarSize
        {
            get { return hotbarSize; }
        }

        /// <inheritdoc cref="craftingGridSize" />
        public int CraftingGridSize
        {
            get { return craftingGridSize; }
        }

        /// <inheritdoc cref="startingItems" />
        public IReadOnlyList<StartingItemEntry> StartingItems
        {
            get { return startingItems; }
        }
    }
}
