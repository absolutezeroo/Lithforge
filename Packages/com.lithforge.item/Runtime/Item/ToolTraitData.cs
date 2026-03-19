namespace Lithforge.Item
{
    /// <summary>
    /// Resolved tool trait data implementing IToolTrait.
    /// Built from ToolTraitDefinition during content pipeline.
    /// </summary>
    public sealed class ToolTraitData : IToolTrait
    {
        private readonly ToolTraitEffect[] _effects;

        public ToolTraitData(string traitId, int traitLevel, int priority, ToolTraitEffect[] effects)
        {
            TraitId = traitId;
            TraitLevel = traitLevel;
            Priority = priority;
            _effects = effects;
        }

        public string TraitId { get; }

        public int TraitLevel { get; }

        public int Priority { get; }

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
