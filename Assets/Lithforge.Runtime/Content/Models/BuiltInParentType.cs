namespace Lithforge.Runtime.Content.Models
{
    /// <summary>
    /// Identifies a built-in parent model that provides a default face-to-texture-variable
    /// mapping. Acts as the terminal node in a BlockModel parent chain -- once the resolver
    /// reaches a model with a non-None built-in parent, it stops walking and uses that
    /// mapping to assign textures to cube faces.
    /// </summary>
    public enum BuiltInParentType
    {
        /// <summary>
        /// No built-in parent; the model inherits from another BlockModel asset or has
        /// no parent at all.
        /// </summary>
        None = 0,

        /// <summary>
        /// All six faces use the same "all" texture variable.
        /// </summary>
        CubeAll = 1,

        /// <summary>
        /// Each face resolves independently via named variables ("north", "south", "east",
        /// "west", "up", "down") with fallback chains (e.g., "north" falls back to "front",
        /// then "side").
        /// </summary>
        Cube = 2,

        /// <summary>
        /// Top and bottom use the "end" variable; four sides use the "side" variable.
        /// Typical for logs, pillars, and similar axis-aligned blocks.
        /// </summary>
        CubeColumn = 3,

        /// <summary>
        /// Top uses "top" (fallback "end"), bottom uses "bottom" (fallback "end"),
        /// four sides use "side". Similar to CubeColumn but with distinct top and bottom.
        /// </summary>
        CubeBottomTop = 4,

        /// <summary>
        /// Front face uses "front" variable, other sides use "side", top/bottom use
        /// "top"/"bottom". For blocks with a distinguished front face like furnaces.
        /// </summary>
        Orientable = 5,

        /// <summary>
        /// Two intersecting diagonal planes using the "cross" variable (fallback "all").
        /// Used for vegetation like flowers and tall grass.
        /// </summary>
        Cross = 6,
    }
}
