using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    /// <summary>
    /// Simple key-value pair serializable by Unity. Used for loot condition and function parameters
    /// where Inspector editing of arbitrary string maps is needed.
    /// </summary>
    [System.Serializable]
    public sealed class StringPair
    {
        /// <summary>Parameter name (e.g. "min", "max", "enchantment").</summary>
        [FormerlySerializedAs("_key"),SerializeField] private string key;

        /// <summary>Parameter value as a string — parsed at load time by the loot system.</summary>
        [FormerlySerializedAs("_value"),SerializeField] private string value;

        /// <summary>Parameter name.</summary>
        public string Key
        {
            get { return key; }
        }

        /// <summary>Parameter value as a string.</summary>
        public string Value
        {
            get { return value; }
        }
    }
}
