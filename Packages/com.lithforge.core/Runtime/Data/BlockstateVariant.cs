using System;

namespace Lithforge.Core.Data
{
    /// <summary>
    /// A single variant entry from a blockstate JSON file.
    /// Maps a property combination to a model reference with optional rotation.
    /// </summary>
    public sealed class BlockstateVariant
    {
        public ResourceId Model { get; set; }

        public int RotationX { get; set; }

        public int RotationY { get; set; }

        public bool Uvlock { get; set; }

        public BlockstateVariant(ResourceId model)
        {
            Model = model;
            RotationX = 0;
            RotationY = 0;
            Uvlock = false;
        }
    }
}
