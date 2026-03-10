using UnityEngine;

namespace Lithforge.Runtime.Content
{
    [System.Serializable]
    public sealed class TextureVariable
    {
        [Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string variable;

        [Tooltip("Texture value (resource path like 'lithforge:block/stone' or #variable reference like '#all')")]
        [SerializeField] private string value;

        public string Variable
        {
            get { return variable; }
        }

        public string Value
        {
            get { return value; }
        }
    }
}
