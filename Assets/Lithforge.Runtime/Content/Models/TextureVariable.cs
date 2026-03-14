using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class TextureVariable
    {
        [FormerlySerializedAs("variable")]
        [Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string _variable;

        [FormerlySerializedAs("texture")]
        [Tooltip("Direct texture reference (drag-drop a Texture2D asset)")]
        [SerializeField] private Texture2D _texture;

        [FormerlySerializedAs("variableReference")]
        [Tooltip("Variable reference (e.g. '#all') for indirection to another variable")]
        [SerializeField] private string _variableReference;

        public string Variable
        {
            get { return _variable; }
        }

        public Texture2D Texture
        {
            get { return _texture; }
        }

        public string VariableReference
        {
            get { return _variableReference; }
        }

        public bool IsVariableReference
        {
            get { return !string.IsNullOrEmpty(_variableReference) && _variableReference.StartsWith("#"); }
        }
    }
}
