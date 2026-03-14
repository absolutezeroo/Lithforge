using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Loot
{
    [System.Serializable]
    public sealed class StringPair
    {
        [FormerlySerializedAs("key")]
        [SerializeField] private string _key;

        [FormerlySerializedAs("value")]
        [SerializeField] private string _value;

        public string Key
        {
            get { return _key; }
        }

        public string Value
        {
            get { return _value; }
        }
    }
}
