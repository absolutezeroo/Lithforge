using System;

using Lithforge.Core.Data;

namespace Lithforge.Item
{
    /// <summary>
    ///     Represents a stack of items in an inventory slot.
    ///     Value type — default is empty (count 0).
    /// </summary>
    public struct ItemStack : IEquatable<ItemStack>
    {
        public ResourceId ItemId { get; set; }

        public int Count { get; set; }

        public int Durability { get; set; }

        public DataComponentMap Components { get; set; }

        public bool HasComponents
        {
            get
            {
                return Components is
                {
                    IsEmpty: false,
                };
            }
        }

        public static readonly ItemStack Empty = default;

        public ItemStack(ResourceId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
            Durability = -1;
            Components = null;
        }

        public ItemStack(ResourceId itemId, int count, int durability)
        {
            ItemId = itemId;
            Count = count;
            Durability = durability;
            Components = null;
        }

        public bool IsEmpty
        {
            get { return Count <= 0; }
        }

        /// <summary>
        ///     Returns true when two stacks can be merged (same item, neither has components).
        ///     This is the ONLY authority on stacking decisions.
        /// </summary>
        public static bool CanStack(ItemStack a, ItemStack b)
        {
            return a.ItemId == b.ItemId && !a.HasComponents && !b.HasComponents;
        }

        public bool Equals(ItemStack other)
        {
            if (ItemId != other.ItemId)
            {
                return false;
            }

            if (Count != other.Count)
            {
                return false;
            }

            if (Durability != other.Durability)
            {
                return false;
            }

            bool thisHas = HasComponents;
            bool otherHas = other.HasComponents;

            if (!thisHas && !otherHas)
            {
                return true;
            }

            if (thisHas != otherHas)
            {
                return false;
            }

            return Components.ContentEquals(other.Components);
        }

        public override bool Equals(object obj)
        {
            return obj is ItemStack other && Equals(other);
        }

        public override int GetHashCode()
        {
            int hash = ItemId.GetHashCode();
            hash = hash * 397 ^ Count;
            hash = hash * 397 ^ Durability;
            return hash;
        }

        public static bool operator ==(ItemStack left, ItemStack right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ItemStack left, ItemStack right)
        {
            return !left.Equals(right);
        }
    }
}
