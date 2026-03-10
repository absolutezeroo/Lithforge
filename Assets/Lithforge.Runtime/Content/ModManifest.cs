using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [CreateAssetMenu(fileName = "NewModManifest", menuName = "Lithforge/Content/Mod Manifest", order = 10)]
    public sealed class ModManifest : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique mod identifier")]
        [SerializeField] private string _modId;

        [Tooltip("Display name")]
        [SerializeField] private string _modName;

        [Tooltip("Mod version (semver)")]
        [SerializeField] private string _version = "1.0.0";

        [Header("Metadata")]
        [Tooltip("Mod description")]
        [TextArea(2, 5)]
        [SerializeField] private string _description;

        [Tooltip("Mod author(s)")]
        [SerializeField] private string _author;

        [Header("Dependencies")]
        [Tooltip("Required mod dependencies (mod_id:min_version)")]
        [SerializeField] private List<ModDependency> _dependencies = new List<ModDependency>();

        public string ModId
        {
            get { return _modId; }
        }

        public string ModName
        {
            get { return _modName; }
        }

        public string Version
        {
            get { return _version; }
        }

        public string Description
        {
            get { return _description; }
        }

        public string Author
        {
            get { return _author; }
        }

        public IReadOnlyList<ModDependency> Dependencies
        {
            get { return _dependencies; }
        }
    }

    [System.Serializable]
    public sealed class ModDependency
    {
        [Tooltip("Required mod id")]
        [SerializeField] private string _modId;

        [Tooltip("Minimum required version")]
        [SerializeField] private string _minVersion;

        public string ModId
        {
            get { return _modId; }
        }

        public string MinVersion
        {
            get { return _minVersion; }
        }
    }
}
