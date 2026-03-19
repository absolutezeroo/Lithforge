using Lithforge.Item;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Items.Affixes
{
    /// <summary>
    /// Data-driven affix that modifies mining calculations via the IMiningModifier pipeline.
    /// Configured as a ScriptableObject and attached to tool ItemDefinitions.
    /// </summary>
    [System.Obsolete("Affix system has no assets and is unused. May be reactivated later.")]
    [CreateAssetMenu(fileName = "NewAffix",
        menuName = "Lithforge/Content/Affix Definition")]
    public sealed class AffixDefinition : ScriptableObject, IMiningModifier
    {
        /// <summary>Unique identifier for this affix (e.g. "efficiency_1").</summary>
        [FormerlySerializedAs("AffixId"),Header("Identity")]
        public string affixId;

        /// <summary>Human-readable name shown in UI tooltips.</summary>
        [FormerlySerializedAs("DisplayName")]
        public string displayName;

        /// <summary>Power tier of this affix variant (higher = stronger effect).</summary>
        [FormerlySerializedAs("Tier")]
        public int tier;

        [FormerlySerializedAs("_priority")]
        [Header("Priority")]
        [Tooltip("Application order: Additive=0-9, Multiplicative=10-19, Override=20+")]
        [SerializeField] private int priority = 10;

        /// <summary>Application order: Additive=0-9, Multiplicative=10-19, Override=20+.</summary>
        public int Priority
        {
            get { return priority; }
        }

        [FormerlySerializedAs("_effects")]
        [Header("Mining Effects")]
        [SerializeField] private AffixMiningEffect[] effects
            = System.Array.Empty<AffixMiningEffect>();

        /// <summary>Applies all mining effects in this affix to the given mining context.</summary>
        public MiningContext Apply(MiningContext ctx)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                ctx = effects[i].Apply(ctx);
            }

            return ctx;
        }
    }
}
