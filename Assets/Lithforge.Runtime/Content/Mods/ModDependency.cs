using UnityEngine;

namespace Lithforge.Runtime.Content.Mods
{
    [System.Serializable]
    public sealed class ModDependency
    {
        [Tooltip("Required mod id")]
        [SerializeField] private string modId;

        [Tooltip("Minimum required version")]
        [SerializeField] private string minVersion;

        public string ModId
        {
            get { return modId; }
        }

        public string MinVersion
        {
            get { return minVersion; }
        }
    }
}
