using System.Collections.Generic;

namespace Lithforge.Item
{
    /// <summary>
    /// Registry mapping trait IDs to resolved ToolTraitData instances.
    /// Built during content pipeline from ToolTraitDefinition assets.
    /// </summary>
    public sealed class ToolTraitRegistry
    {
        private readonly Dictionary<string, ToolTraitData> _traits = new();

        public void Register(ToolTraitData trait)
        {
            _traits[trait.TraitId] = trait;
        }

        public ToolTraitData Get(string traitId)
        {
            if (_traits.TryGetValue(traitId, out ToolTraitData data))
            {
                return data;
            }

            return null;
        }

        public bool Contains(string traitId)
        {
            return _traits.ContainsKey(traitId);
        }

        public int Count
        {
            get { return _traits.Count; }
        }
    }
}
