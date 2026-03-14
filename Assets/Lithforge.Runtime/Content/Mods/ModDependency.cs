using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Mods
{
    [System.Serializable]
    public sealed class ModDependency
    {
        [FormerlySerializedAs("_modId"),Tooltip("Required mod id")]
        [SerializeField] private string modId;

        [FormerlySerializedAs("_minVersion"),Tooltip("Minimum required version")]
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
