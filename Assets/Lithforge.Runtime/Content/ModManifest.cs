using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewModManifest", menuName = "Lithforge/Content/Mod Manifest", order = 10)]
    public sealed class ModManifest : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique mod identifier")]
        [SerializeField] private string modId;

        [Tooltip("Display name")]
        [SerializeField] private string modName;

        [Tooltip("Mod version (semver)")]
        [SerializeField] private string version = "1.0.0";

        [Header("Metadata")]
        [Tooltip("Mod description")]
        [TextArea(2, 5)]
        [SerializeField] private string description;

        [Tooltip("Mod author(s)")]
        [SerializeField] private string author;

        [Header("Dependencies")]
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
