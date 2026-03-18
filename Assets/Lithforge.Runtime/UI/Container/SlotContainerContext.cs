using System.Collections.Generic;

namespace Lithforge.Runtime.UI.Container
{
    /// <summary>
    ///     Holds all named ISlotContainers for a screen session.
    ///     Keyed by name (e.g. "hotbar", "main", "craft", "output").
    /// </summary>
    public sealed class SlotContainerContext
    {
        private readonly Dictionary<string, ISlotContainer> _containers = new();

        public IEnumerable<KeyValuePair<string, ISlotContainer>> All
        {
            get { return _containers; }
        }

        public void Register(string name, ISlotContainer container)
        {
            _containers[name] = container;
        }

        public ISlotContainer Get(string name)
        {
            if (_containers.TryGetValue(name, out ISlotContainer container))
            {
                return container;
            }

            return null;
        }

        public bool TryGet(string name, out ISlotContainer container)
        {
            return _containers.TryGetValue(name, out container);
        }
    }
}
