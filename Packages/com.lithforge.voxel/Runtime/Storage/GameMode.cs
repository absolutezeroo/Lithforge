namespace Lithforge.Voxel.Storage
{
    /// <summary>
    /// Determines the core rule set for a world: resource gathering constraints,
    /// health/hunger, and inventory behavior.
    /// </summary>
    public enum GameMode
    {
        /// <summary>Normal gameplay with health, hunger, mining requirements, and finite resources.</summary>
        Survival = 0,

        /// <summary>Unlimited resources, instant block breaking, no health or hunger, flight enabled.</summary>
        Creative = 1,
    }
}
