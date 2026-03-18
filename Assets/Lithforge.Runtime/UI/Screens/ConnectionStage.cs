namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Stages of the client connection lifecycle, displayed by
    /// <see cref="ConnectionProgressScreen"/>.
    /// </summary>
    public enum ConnectionStage
    {
        /// <summary>Not connected.</summary>
        Disconnected = 0,

        /// <summary>UTP handshake in progress.</summary>
        Connecting = 1,

        /// <summary>Exchanging content hashes and player info.</summary>
        Authenticating = 2,

        /// <summary>Receiving block registry and content definitions.</summary>
        SyncingContent = 3,

        /// <summary>Receiving spawn chunks and building meshes.</summary>
        LoadingTerrain = 4,

        /// <summary>Final entity sync and player spawn.</summary>
        EnteringWorld = 5,

        /// <summary>Fully connected and playing.</summary>
        Connected = 6,

        /// <summary>An error occurred at any stage.</summary>
        Error = 7,
    }
}
