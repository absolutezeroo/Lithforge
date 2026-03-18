using Lithforge.Core.Data;
using Lithforge.Item;
using NUnit.Framework;

namespace Lithforge.Item.Tests
{
    [TestFixture]
    public sealed class InventoryTests
    {
        private Inventory _inventory;

        [SetUp]
        public void SetUp()
        {
            _inventory = new Inventory();
        }

        [Test]
        public void AddItem_FullInventory_ReturnsRemaining()
        {
            ResourceId itemA = new("lithforge", "stone");

            // Fill all 36 slots to max stack
            for (int i = 0; i < Inventory.SlotCount; i++)
            {
                _inventory.SetSlot(i, new ItemStack(itemA, 64));
            }

            // Try to add more
            int remaining = _inventory.AddItem(itemA, 10, 64);

            Assert.AreEqual(10, remaining, "Should return all items as remaining when full");
        }

        [Test]
        public void AddItem_SplitsAcrossStacks()
        {
            ResourceId itemA = new("lithforge", "stone");

            // Slot 0 has 60 of itemA with maxStack=64
            _inventory.SetSlot(0, new ItemStack(itemA, 60));

            // Add 10 more — 4 should fill slot 0, 6 should go to slot 1
            int remaining = _inventory.AddItem(itemA, 10, 64);

            Assert.AreEqual(0, remaining, "All items should fit");
            Assert.AreEqual(64, _inventory.GetSlot(0).Count, "Slot 0 should be full");
            Assert.AreEqual(6, _inventory.GetSlot(1).Count, "Slot 1 should have overflow");
        }

        [Test]
        public void RemoveFromSlot_MoreThanCount_ReturnsActual()
        {
            ResourceId itemA = new("lithforge", "stone");
            _inventory.SetSlot(0, new ItemStack(itemA, 3));

            int removed = _inventory.RemoveFromSlot(0, 10);

            Assert.AreEqual(3, removed, "Should return actual count removed");
            Assert.IsTrue(_inventory.GetSlot(0).IsEmpty, "Slot should be empty after removing all");
        }
    }
}
