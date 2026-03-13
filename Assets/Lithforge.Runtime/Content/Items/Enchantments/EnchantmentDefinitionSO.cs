using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Items
{
    /// <summary>
    /// Data-driven enchantment with multiple levels, each providing mining effects.
    /// Configured as a ScriptableObject. Applied to items at runtime via enchantment slots.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEnchantment",
        menuName = "Lithforge/Content/Enchantment Definition")]
    public sealed class EnchantmentDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string EnchantmentId;
        public string DisplayName;
        public int MaxLevel;
        public EnchantmentCategory Category;

        [Header("Levels")]
        [SerializeField] private EnchantmentLevelData[] _levels;

        public MiningContext Apply(MiningContext ctx, int level)
        {
            if (_levels == null || level < 1 || level > _levels.Length)
            {
                return ctx;
            }

            return _levels[level - 1].Apply(ctx);
        }
    }
}
