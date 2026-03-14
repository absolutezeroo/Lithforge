using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class TextureVariable
    {
        [FormerlySerializedAs("_variable"),Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string variable;

        [FormerlySerializedAs("_texture"),Tooltip("Direct texture reference (drag-drop a Texture2D asset)")]
        [SerializeField] private Texture2D texture;

        [FormerlySerializedAs("_variableReference"),Tooltip("Variable reference (e.g. '#all') for indirection to another variable")]
        [SerializeField] private string variableReference;

        public string Variable
        {
            get { return variable; }
        }

        public Texture2D Texture
        {
            get { return texture; }
        }

        public string VariableReference
        {
            get { return variableReference; }
        }

        public bool IsVariableReference
        {
            get { return !string.IsNullOrEmpty(variableReference) && variableReference.StartsWith("#"); }
        }
    }
}
