using Lithforge.Voxel.Item;
using UnityEngine;

namespace Lithforge.Runtime.Content.Items
{
    /// <summary>
    /// Data-driven affix that modifies mining calculations via the IMiningModifier pipeline.
    /// Configured as a ScriptableObject and attached to tool ItemDefinitions.
    /// </summary>
    [CreateAssetMenu(fileName = "NewAffix",
        menuName = "Lithforge/Content/Affix Definition")]
    public sealed class AffixDefinitionSO : ScriptableObject, IMiningModifier
    {
        [Header("Identity")]
        public string AffixId;
        public string DisplayName;
        public int Tier;

        public int Priority
        {
            get { return 10; }
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
