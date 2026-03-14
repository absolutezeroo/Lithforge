using Lithforge.Core.Data;

namespace Lithforge.Voxel.Item
{
    public struct ToolPart
    {
        public ToolPartType PartType;
        public ResourceId MaterialId;
        public float SpeedContribution;
        public int DurabilityContribution;
        public float DamageContribution;
        public float DurabilityMultiplier;
        public float SpeedMultiplier;
        public ResourceId[] TraitIds;

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
