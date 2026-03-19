namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Discriminator for <see cref="BlockCommandRequest" /> to identify which
    ///     block processor method to invoke on the main thread.
    /// </summary>
    internal enum BlockCommandKind : byte
    {
        /// <summary>Call <see cref="Server.IServerBlockProcessor.TryBreakBlock" />.</summary>
        TryBreakBlock = 0,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.TryPlaceBlock" />.</summary>
        TryPlaceBlock = 1,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.StartDigging" />.</summary>
        StartDigging = 2,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.CancelDigging" />.</summary>
        CancelDigging = 3,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.RefillRateLimitTokens" />.</summary>
        RefillRateLimitTokens = 4,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.AddPlayer" />.</summary>
        AddPlayer = 5,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.RemovePlayer" />.</summary>
        RemovePlayer = 6,

        /// <summary>Call <see cref="Server.IServerBlockProcessor.GetBlock" />.</summary>
        GetBlock = 7,
    }
}
