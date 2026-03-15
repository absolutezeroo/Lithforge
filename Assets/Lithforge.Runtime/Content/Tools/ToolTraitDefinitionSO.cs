using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Tools
{
    /// <summary>
    /// Data-driven tool trait definition. Configured as a ScriptableObject
    /// and referenced by ToolMaterialDefinition via trait ID strings.
    /// Converted to ToolTraitData (Tier 2) during content pipeline.
    /// </summary>
    [CreateAssetMenu(fileName = "NewToolTrait",
        menuName = "Lithforge/Content/Tool Trait Definition")]
    public sealed class ToolTraitDefinitionSO : ScriptableObject
    {
        [FormerlySerializedAs("TraitId"), Header("Identity")]
        [Tooltip("Unique trait identifier (e.g. lithforge:haste)")]
        public string traitId;

        [FormerlySerializedAs("DisplayName")]
        [Tooltip("Display name shown in tooltip")]
        public string displayName;

        [FormerlySerializedAs("TraitLevel"), Header("Level")]
        [Tooltip("Trait level for duplicate resolution (highest wins)")]
        [Min(1)]
        public int traitLevel = 1;

        [FormerlySerializedAs("Priority"), Header("Priority")]
        [Tooltip("Application order: Additive=0-9, Multiplicative=10-19, Override=20+")]
        public int priority = 10;

        [FormerlySerializedAs("Effects"), Header("Mining Effects")]
        [Tooltip("Effects applied when this trait is active")]
        public ToolTraitMiningEffect[] effects = System.Array.Empty<ToolTraitMiningEffect>();

        /// <summary>
        /// Converts this SO to a Tier 2 ToolTraitData instance.
        /// </summary>
        public ToolTraitData ToTier2()
        {
            ToolTraitEffect[] tier2Effects = new ToolTraitEffect[effects.Length];

            for (int i = 0; i < effects.Length; i++)
            {
                tier2Effects[i] = effects[i].ToTier2();
            }

            return new ToolTraitData(traitId, traitLevel, priority, tier2Effects);
        }
    }
}
