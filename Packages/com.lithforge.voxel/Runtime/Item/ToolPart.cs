using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    /// <summary>
    /// One physical component of a modular tool, combining a material with a
    /// structural role. The part's material determines base stats; the tool
    /// sums contributions from all its parts to compute final stats.
    /// </summary>
    public struct ToolPart
    {
        /// <summary>Structural role this part fills (head, handle, blade, etc.).</summary>
        public ToolPartType PartType;

        /// <summary>The material this part is made from (e.g., "lithforge:iron").</summary>
        public ResourceId MaterialId;

        /// <summary>Flat mining/attack speed bonus contributed by this part.</summary>
        public float SpeedContribution;

        /// <summary>Flat durability bonus contributed by this part.</summary>
        public int DurabilityContribution;

        /// <summary>Flat damage bonus contributed by this part.</summary>
        public float DamageContribution;

        /// <summary>Multiplicative durability scaling (1.0 = neutral). Applied after flat sums.</summary>
        public float DurabilityMultiplier;

        /// <summary>Multiplicative speed scaling (1.0 = neutral). Applied after flat sums.</summary>
        public float SpeedMultiplier;

        /// <summary>Material-granted traits (e.g., "lithforge:magnetic", "lithforge:lightweight").</summary>
        public ResourceId[] TraitIds;

        /// <summary>
        /// Returns a zero-contribution part with neutral multipliers and no traits.
        /// Used as a placeholder for unfilled part slots.
        /// </summary>
        public static ToolPart Empty
        {
            get
            {
                return new ToolPart
                {
                    DurabilityMultiplier = 1.0f,
                    SpeedMultiplier = 1.0f,
                    TraitIds = System.Array.Empty<ResourceId>(),
                };
            }
        }
    }
}
