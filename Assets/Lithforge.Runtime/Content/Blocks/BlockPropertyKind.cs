namespace Lithforge.Runtime.Content.Blocks
{
    /// <summary>
    /// Determines how a block property generates its set of valid values
    /// during the cartesian-product state expansion in ContentPipeline.
    /// </summary>
    public enum BlockPropertyKind
    {
        /// <summary>Two values: "true" and "false".</summary>
        Bool = 0,
        /// <summary>Consecutive integers from min to max (inclusive).</summary>
        IntRange = 1,
        /// <summary>Explicit string values defined in the property's value list.</summary>
        Enum = 2,
    }
}
