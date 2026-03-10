using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "GameplaySettings", menuName = "Lithforge/Settings/Gameplay", order = 5)]
    public sealed class GameplaySettings : ScriptableObject
    {
        [Header("Inventory")]
        [Tooltip("Total inventory slot count (advisory — Inventory.SlotCount is the runtime constant)")]
        [Min(9)]
        [SerializeField] private int inventorySlotCount = 36;

        [Tooltip("Hotbar size (advisory — Inventory.HotbarSize is the runtime constant)")]
        [Min(1)]
        [SerializeField] private int hotbarSize = 9;

        [Tooltip("Player crafting grid dimension (2 = 2x2)")]
        [Range(2, 3)]
        [SerializeField] private int craftingGridSize = 2;

        [Header("Starting Items")]
        [Tooltip("Items granted to the player at first spawn")]
        [SerializeField] private StartingItemEntry[] startingItems = new StartingItemEntry[]
        {
            new StartingItemEntry { itemNamespace = "lithforge", itemName = "cobblestone", count = 64 },
        };

        public int InventorySlotCount
        {
            get { return inventorySlotCount; }
        }

        public int HotbarSize
        {
            get { return hotbarSize; }
        }

        public int CraftingGridSize
        {
            get { return craftingGridSize; }
        }

        public IReadOnlyList<StartingItemEntry> StartingItems
        {
            get { return startingItems; }
        }
    }
}
