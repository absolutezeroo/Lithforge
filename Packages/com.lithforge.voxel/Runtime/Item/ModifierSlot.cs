using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    public struct ModifierSlot
    {
        public bool IsOccupied;
        public ResourceId ModifierId;
        public int Level;
    }
}
