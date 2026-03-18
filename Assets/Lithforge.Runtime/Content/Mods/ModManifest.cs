using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Mods
{
    /// <summary>
    ///     Metadata asset embedded in every <c>.lithmod</c> AssetBundle, providing identity,
    ///     versioning, and dependency declarations for the mod loader.
    /// </summary>
    [CreateAssetMenu(fileName = "NewModManifest", menuName = "Lithforge/Content/Mod Manifest", order = 10)]
    public sealed class ModManifest : ScriptableObject
    {
        /// <summary>Globally unique mod identifier used for dependency resolution (e.g. "lithforge.base").</summary>
        [FormerlySerializedAs("_modId"), Header("Identity"), Tooltip("Unique mod identifier"), SerializeField]
         private string modId;

        /// <summary>Human-readable name shown in the mod list UI.</summary>
        [FormerlySerializedAs("_modName"), Tooltip("Display name"), SerializeField]
         private string modName;

        /// <summary>Semver string indicating the current release of this mod.</summary>
        [FormerlySerializedAs("_version"), Tooltip("Mod version (semver)"), SerializeField]
         private string version = "1.0.0";

        /// <summary>Free-text summary displayed in the mod browser.</summary>
        [FormerlySerializedAs("_description"), Header("Metadata"), Tooltip("Mod description"), TextArea(2, 5), SerializeField]
         private string description;

        /// <summary>Name(s) of the mod creator(s).</summary>
        [FormerlySerializedAs("_author"), Tooltip("Mod author(s)"), SerializeField]
         private string author;

        /// <summary>Other mods that must be loaded before this one, with optional minimum versions.</summary>
        [FormerlySerializedAs("_dependencies"), Header("Dependencies"), Tooltip("Required mod dependencies (mod_id:min_version)"), SerializeField]
         private List<ModDependency> dependencies = new();

        /// <summary>Globally unique mod identifier used for dependency resolution.</summary>
        public string ModId
        {
            get { return modId; }
        }

        /// <summary>Human-readable display name.</summary>
        public string ModName
        {
            get { return modName; }
        }

        /// <summary>Semver version string for this mod release.</summary>
        public string Version
        {
            get { return version; }
        }

        /// <summary>Free-text summary of what the mod provides.</summary>
        public string Description
        {
            get { return description; }
        }

        /// <summary>Name(s) of the mod creator(s).</summary>
        public string Author
        {
            get { return author; }
        }

        /// <summary>Mods that must be loaded before this one.</summary>
        public IReadOnlyList<ModDependency> Dependencies
        {
            get { return dependencies; }
        }

        private void OnValidate()
        {
            if (string.IsNullOrEmpty(modName))
            {
                modName = name;
            }
        }
    }
}
