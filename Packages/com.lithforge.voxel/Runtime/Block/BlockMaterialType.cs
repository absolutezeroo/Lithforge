namespace Lithforge.Voxel.Block
{
    /// <summary>
    /// Physical material category of a block.
    /// Determines mining speed modifiers per tool type.
    /// </summary>
    public enum BlockMaterialType : byte
    {
        None    = 0,
        Stone   = 1,
        Wood    = 2,
        Dirt    = 3,
        Leaves  = 4,
        Metal   = 5,
        Glass   = 6,
        Organic = 7,
    }
}
