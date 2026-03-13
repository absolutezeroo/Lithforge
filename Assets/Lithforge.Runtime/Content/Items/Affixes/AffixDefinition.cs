using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Items.Affixes
{
    /// <summary>
    /// Data-driven affix that modifies mining calculations via the IMiningModifier pipeline.
    /// Configured as a ScriptableObject and attached to tool ItemDefinitions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAffix",
        menuName = "Lithforge/Content/Affix Definition")]
    public sealed class AffixDefinition : ScriptableObject, IMiningModifier
    {
        [Header("Identity")]
        public string AffixId;
        public string DisplayName;
        public int Tier;

        [Header("Priority")]
        [Tooltip("Application order: Additive=0-9, Multiplicative=10-19, Override=20+")]
        [SerializeField] private int _priority = 10;

        public int Priority
        {
            get { return _priority; }
        }

        [Header("Mining Effects")]
        [SerializeField] private AffixMiningEffect[] _effects
            = System.Array.Empty<AffixMiningEffect>();

        public MiningContext Apply(MiningContext ctx)
        {
            for (int i = 0; i < _effects.Length; i++)
            {
                ctx = _effects[i].Apply(ctx);
            }

            return ctx;
        }
    }
}
