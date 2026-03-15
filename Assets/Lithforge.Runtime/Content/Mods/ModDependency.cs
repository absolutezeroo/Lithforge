using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Mods
{
    /// <summary>
    /// Declares that a mod requires another mod to be loaded, with an optional minimum version.
    /// Serialized inside <see cref="ModManifest.Dependencies"/> and checked during mod load ordering.
    /// </summary>
    [System.Serializable]
    public sealed class ModDependency
    {
        /// <summary>Unique identifier of the required mod (e.g. "lithforge.base").</summary>
        [FormerlySerializedAs("_modId"),Tooltip("Required mod id")]
        [SerializeField] private string modId;

        /// <summary>Semver lower bound the dependency must satisfy (e.g. "1.2.0").</summary>
        [FormerlySerializedAs("_minVersion"),Tooltip("Minimum required version")]
        [SerializeField] private string minVersion;

        /// <summary>Unique identifier of the required mod.</summary>
        public string ModId
        {
            get { return modId; }
        }

        /// <summary>Minimum semver version required, or empty/null if any version is acceptable.</summary>
        public string MinVersion
        {
            get { return minVersion; }
        }
    }
}
