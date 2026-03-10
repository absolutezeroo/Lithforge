using UnityEngine;

namespace Lithforge.Runtime.Content.Settings
{
    [CreateAssetMenu(fileName = "GameplaySettings", menuName = "Lithforge/Settings/Gameplay", order = 5)]
    public sealed class GameplaySettings : ScriptableObject
    {
        [Header("Inventory")]
        [Tooltip("Total inventory slot count (advisory — Inventory.SlotCount is the runtime constant)")]
        [Min(9)]
        [SerializeField] private int _inventorySlotCount = 36;

        [Tooltip("Hotbar size (advisory — Inventory.HotbarSize is the runtime constant)")]
        [Min(1)]
        [SerializeField] private int _hotbarSize = 9;

        [Tooltip("Player crafting grid dimension (2 = 2x2)")]
        [Range(2, 3)]
        [SerializeField] private int _craftingGridSize = 2;

        [Header("Starting Items")]
        [Tooltip("Items granted to the player at first spawn")]
        [SerializeField] private StartingItemEntry[] _startingItems = new StartingItemEntry[]
        {
            new StartingItemEntry { Namespace = "lithforge", Name = "cobblestone", Count = 64 },
        };

        public int InventorySlotCount
        {
            get { return _inventorySlotCount; }
        }

        public int HotbarSize
        {
            get { return _hotbarSize; }
        }

        public int CraftingGridSize
        {
            get { return _craftingGridSize; }
        }

        public StartingItemEntry[] StartingItems
        {
            get { return _startingItems; }
        }
    }
}
