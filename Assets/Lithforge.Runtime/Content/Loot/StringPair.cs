using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class StringPair
    {
        [FormerlySerializedAs("_key"),SerializeField] private string key;

        [FormerlySerializedAs("_value"),SerializeField] private string value;

        public string Key
        {
            get { return key; }
        }

        public string Value
        {
            get { return value; }
        }
    }
}
