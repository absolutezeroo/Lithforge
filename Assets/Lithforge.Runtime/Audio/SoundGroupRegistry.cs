using System.Collections.Generic;

namespace Lithforge.Runtime.Audio
{
    /// <summary>
    /// Maps sound group names (e.g. "stone", "wood") to their
    /// <see cref="SoundGroupDefinition"/> ScriptableObjects.
    /// Built once during ContentPipeline Phase 17.
    /// </summary>
    public sealed class SoundGroupRegistry
    {
        private readonly Dictionary<string, SoundGroupDefinition> _groups = new();

        private readonly HashSet<string> _warnedGroups = new();

        /// <summary>
        /// Registers a sound group definition. Duplicate names overwrite silently.
        /// </summary>
        public void Register(string groupName, SoundGroupDefinition definition)
        {
            _groups[groupName] = definition;
        }

        /// <summary>
        /// Looks up the definition for a sound group name.
        /// Returns null if the group is not registered (caller should handle gracefully).
        /// </summary>
        public SoundGroupDefinition Get(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                return null;
            }

            if (_groups.TryGetValue(groupName, out SoundGroupDefinition definition))
            {
                return definition;
            }

            if (_warnedGroups.Add(groupName))
            {
                UnityEngine.Debug.LogWarning(
                    $"[Audio] Sound group '{groupName}' not found. Sounds will be silent.");
            }

            return null;
        }

        public int Count
        {
            get { return _groups.Count; }
        }
    }
}
