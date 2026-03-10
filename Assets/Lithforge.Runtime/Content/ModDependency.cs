using UnityEngine;

namespace Lithforge.Runtime.Content
{
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
