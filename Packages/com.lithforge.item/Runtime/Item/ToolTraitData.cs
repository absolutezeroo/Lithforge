namespace Lithforge.Item
{
    /// <summary>
    /// Resolved tool trait data implementing IToolTrait.
    /// Built from ToolTraitDefinition during content pipeline.
    /// </summary>
    public sealed class ToolTraitData : IToolTrait
    {
        private readonly string _traitId;
        private readonly int _traitLevel;
        private readonly int _priority;
        private readonly ToolTraitEffect[] _effects;

        public ToolTraitData(string traitId, int traitLevel, int priority, ToolTraitEffect[] effects)
        {
            _traitId = traitId;
            _traitLevel = traitLevel;
            _priority = priority;
            _effects = effects;
        }

        public string TraitId
        {
            get { return _traitId; }
        }

        public int TraitLevel
        {
            get { return _traitLevel; }
        }

        public int Priority
        {
            get { return _priority; }
        }

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
