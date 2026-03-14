using Lithforge.Runtime.Content.Items.Affixes;
using Lithforge.Voxel.Item;

using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items.Enchantments
{
    /// <summary>
    /// Per-level data for an enchantment, containing display info and mining effects.
    /// </summary>
    [System.Serializable]
    public struct EnchantmentLevelData
    {
        [FormerlySerializedAs("DisplaySuffix")]
        public string displaySuffix;
        [FormerlySerializedAs("Effects")]
        public AffixMiningEffect[] effects;

        public MiningContext Apply(MiningContext ctx)
        {
            if (effects == null)
            {
                return ctx;
            }

            for (int i = 0; i < effects.Length; i++)
            {
                ctx = effects[i].Apply(ctx);
            }

            return ctx;
        }
    }
}
