using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class StringPair
    {
        [SerializeField] private string key;

        [SerializeField] private string value;

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
