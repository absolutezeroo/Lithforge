namespace Lithforge.Voxel.Liquid
{
    /// <summary>
    /// Identifies the fluid type for a liquid simulation run.
    /// Stored in <see cref="LiquidJobConfig"/> to allow future lava support
    /// without job struct changes.
    /// </summary>
    public enum LiquidFluidType : byte
    {
        Water = 0,
        Lava = 1,
    }
}
