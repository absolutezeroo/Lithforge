namespace Lithforge.Item
{
    /// <summary>
    /// Extends IMiningModifier for tool-specific traits.
    /// Future: ICombatModifier will be added here.
    /// </summary>
    public interface IToolTrait : IMiningModifier
    {
        public string TraitId { get; }

        /// <summary>
        /// Allows duplicate detection — only the highest level wins.
        /// </summary>
        public int TraitLevel { get; }
    }
}
