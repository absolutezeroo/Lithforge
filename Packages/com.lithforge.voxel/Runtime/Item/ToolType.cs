namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// Types of tools that can be used for mining and combat.
    /// Determines which blocks can be efficiently mined.
    /// </summary>
    public enum ToolType : byte
    {
        None = 0,
        Pickaxe = 1,
        Axe = 2,
        Shovel = 3,
        Hoe = 4,
        Sword = 5,
    }
}
