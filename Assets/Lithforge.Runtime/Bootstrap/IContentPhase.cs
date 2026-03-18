namespace Lithforge.Runtime.Bootstrap
{
    /// <summary>
    ///     A single phase of the content loading pipeline.
    ///     Phases execute sequentially and can read/write the shared context.
    /// </summary>
    public interface IContentPhase
    {
        /// <summary>Description shown on the loading screen while this phase runs.</summary>
        public string Description { get; }

        /// <summary>Execute the phase. Reads inputs from and writes outputs to the context.</summary>
        public void Execute(ContentPhaseContext context);
    }
}
