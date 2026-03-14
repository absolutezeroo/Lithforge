using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Mods
{
    [System.Serializable]
    public sealed class ModDependency
    {
        [FormerlySerializedAs("modId")]
        [Tooltip("Required mod id")]
        [SerializeField] private string _modId;

        [FormerlySerializedAs("minVersion")]
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
