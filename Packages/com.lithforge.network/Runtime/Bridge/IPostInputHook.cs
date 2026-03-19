namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Hook called by <see cref="Server.ServerGameLoop" /> after Phase 2 (ProcessPlayerInputs)
    ///     completes. Used by <see cref="BridgedSimulation" /> to synchronize physics results
    ///     from the main thread before Phase 3 begins.
    /// </summary>
    internal interface IPostInputHook
    {
        /// <summary>Called after all player inputs have been processed for the current tick.</summary>
        public void AfterProcessPlayerInputs();
    }
}
