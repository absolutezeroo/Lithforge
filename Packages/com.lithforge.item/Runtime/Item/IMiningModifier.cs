namespace Lithforge.Item
{
    /// <summary>
    /// Modifier that transforms a MiningContext during the mining calculation pipeline.
    /// Implemented by affixes, enchantments, and other item modifiers.
    /// </summary>
    public interface IMiningModifier
    {
        /// <summary>
        /// Applies modifications to the mining context and returns the modified result.
        /// </summary>
        public MiningContext Apply(MiningContext ctx);

        /// <summary>
        /// Priority for application ordering (lower = applied first).
        /// Convention: Additive (0-9) &lt; Multiplicative (10-19) &lt; Override (20+).
        /// </summary>
        public int Priority { get; }
    }
}
