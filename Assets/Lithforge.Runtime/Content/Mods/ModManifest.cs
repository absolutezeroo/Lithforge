using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Mods
{
    [CreateAssetMenu(fileName = "NewModManifest", menuName = "Lithforge/Content/Mod Manifest", order = 10)]
    public sealed class ModManifest : ScriptableObject
    {
        [FormerlySerializedAs("_modId"),Header("Identity")]
        [Tooltip("Unique mod identifier")]
        [SerializeField] private string modId;

        [FormerlySerializedAs("_modName"),Tooltip("Display name")]
        [SerializeField] private string modName;

        [FormerlySerializedAs("_version"),Tooltip("Mod version (semver)")]
        [SerializeField] private string version = "1.0.0";

        [FormerlySerializedAs("_description"),Header("Metadata")]
        [Tooltip("Mod description")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        [FormerlySerializedAs("_author"),Tooltip("Mod author(s)")]
        [SerializeField] private string author;

        [FormerlySerializedAs("_dependencies"),Header("Dependencies")]
        [Tooltip("Required mod dependencies (mod_id:min_version)")]
        [SerializeField] private List<ModDependency> dependencies = new List<ModDependency>();

        public string ModId
        {
            get { return modId; }
        }

        public string ModName
        {
            get { return modName; }
        }

        public string Version
        {
            get { return version; }
        }

        public string Description
        {
            get { return description; }
        }

        public string Author
        {
            get { return author; }
        }

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
