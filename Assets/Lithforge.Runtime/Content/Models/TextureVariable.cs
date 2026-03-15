using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Maps a named texture slot to either a concrete Texture2D asset or an indirection
    /// to another variable via "#variable" syntax. During parent-chain resolution,
    /// child variables override parent variables with the same name.
    /// </summary>
    /// <remarks>
    /// Exactly one of <see cref="Texture"/> or <see cref="VariableReference"/> should be
    /// set. If both are present, the variable reference takes precedence
    /// (<see cref="IsVariableReference"/> is checked first by ContentModelResolver).
    /// </remarks>
    [System.Serializable]
    public sealed class TextureVariable
    {
        /// <summary>
        /// Slot name that face entries reference (e.g., "all", "side", "end", "north").
        /// Must be unique within a single BlockModel's texture list.
        /// </summary>
        [FormerlySerializedAs("_variable"),Tooltip("Variable name (e.g. 'all', 'side', 'end', 'north')")]
        [SerializeField] private string variable;

        /// <summary>
        /// Concrete texture asset for this slot. Used when this variable resolves
        /// directly to an image rather than forwarding to another variable.
        /// </summary>
        [FormerlySerializedAs("_texture"),Tooltip("Direct texture reference (drag-drop a Texture2D asset)")]
        [SerializeField] private Texture2D texture;

        /// <summary>
        /// Indirection to another variable in the merged texture map (e.g., "#all").
        /// ContentModelResolver follows these chains, detecting and logging cycles.
        /// </summary>
        [FormerlySerializedAs("_variableReference"),Tooltip("Variable reference (e.g. '#all') for indirection to another variable")]
        [SerializeField] private string variableReference;

        /// <summary>
        /// Slot name that face entries reference (e.g., "all", "side", "end").
        /// </summary>
        public string Variable
        {
            get { return variable; }
        }

        /// <summary>
        /// Concrete Texture2D for this slot, or null if this is a variable reference.
        /// </summary>
        public Texture2D Texture
        {
            get { return texture; }
        }

        /// <summary>
        /// Indirection string (e.g., "#all") pointing to another variable in the
        /// merged map, or null/empty if this variable resolves directly.
        /// </summary>
        public string VariableReference
        {
            get { return variableReference; }
        }

        /// <summary>
        /// True when this variable forwards to another via "#variable" syntax rather
        /// than holding a direct Texture2D reference.
        /// </summary>
        public bool IsVariableReference
        {
            get { return !string.IsNullOrEmpty(variableReference) && variableReference.StartsWith("#"); }
        }
    }
}
