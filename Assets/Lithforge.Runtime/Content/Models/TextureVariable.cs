using UnityEngine;

namespace Lithforge.Runtime.Content.Models
{
    [System.Serializable]
    public sealed class TextureVariable
    {
        [Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string variable;

        [Tooltip("Direct texture reference (drag-drop a Texture2D asset)")]
        [SerializeField] private Texture2D texture;

        [Tooltip("Variable reference (e.g. '#all') for indirection to another variable")]
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
