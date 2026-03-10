using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class StringPair
    {
        [SerializeField] private string _key;

        [SerializeField] private string _value;

        public string Key
        {
            get { return _key; }
        }

        public string Value
        {
            get { return _value; }
        }

        public StringPair(string key, string value)
        {
            _key = key;
            _value = value;
        }
    }
}
