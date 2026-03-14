using Lithforge.Runtime.Content.Items.Affixes;
using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items.Enchantments
{
    /// <summary>
    /// Data-driven enchantment with multiple levels, each providing mining effects.
    /// Configured as a ScriptableObject. Applied to items at runtime via enchantment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnchantment",
        menuName = "Lithforge/Content/Enchantment Definition")]
    public sealed class EnchantmentDefinition : ScriptableObject
    {
        [FormerlySerializedAs("EnchantmentId"),Header("Identity")]
        public string enchantmentId;
        [FormerlySerializedAs("DisplayName")]
        public string displayName;
        [FormerlySerializedAs("MaxLevel")]
        public int maxLevel;
        [FormerlySerializedAs("Category")]
        public EnchantmentCategory category;

        [FormerlySerializedAs("_levels")]
        [Header("Levels")]
        [SerializeField] private EnchantmentLevelData[] levels;

        public MiningContext Apply(MiningContext ctx, int level)
        {
            if (levels == null || level < 1 || level > levels.Length)
            {
                return ctx;
            }

            return levels[level - 1].Apply(ctx);
        }
    }
}
