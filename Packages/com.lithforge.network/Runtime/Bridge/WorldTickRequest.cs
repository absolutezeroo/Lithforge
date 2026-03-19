namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Signals the main thread to run one world-systems tick (block entities, time of day, liquids).
    /// </summary>
    internal sealed class WorldTickRequest
    {
        /// <summary>Fixed delta time for this world tick.</summary>
        public float TickDt;
    }
}
