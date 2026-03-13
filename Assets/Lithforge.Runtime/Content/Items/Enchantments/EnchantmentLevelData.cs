using Lithforge.Voxel.Item;

namespace Lithforge.Runtime.Content.Items
{
    /// <summary>
    /// Per-level data for an enchantment, containing display info and mining effects.
    /// </summary>
    [System.Serializable]
    public struct EnchantmentLevelData
    {
        public string DisplaySuffix;
        public AffixMiningEffect[] Effects;

        public MiningContext Apply(MiningContext ctx)
        {
            if (Effects == null)
            {
                return ctx;
            }

            for (int i = 0; i < Effects.Length; i++)
            {
                ctx = Effects[i].Apply(ctx);
            }

            return ctx;
        }
    }
}
