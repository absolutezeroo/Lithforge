using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class TextureVariable
    {
        [Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string _variable;

        [Tooltip("Texture value (resource path like 'lithforge:block/stone' or #variable reference like '#all')")]
        [SerializeField] private string _value;

        public string Variable
        {
            get { return _variable; }
        }

        public string Value
        {
            get { return _value; }
        }
    }
}
