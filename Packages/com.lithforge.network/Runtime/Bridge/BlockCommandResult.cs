using Lithforge.Network.Server;
using Lithforge.Voxel.Block;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Carries the result of a block command from the main thread back to the server thread.
    /// </summary>
    internal sealed class BlockCommandResult
    {
        /// <summary>Full result for TryBreakBlock/TryPlaceBlock commands.</summary>
        public BlockProcessResult ProcessResult;

        /// <summary>Return value for StartDigging (true = valid, false = invalid).</summary>
        public bool StartDiggingResult;

        /// <summary>Return value for GetBlock queries.</summary>
        public StateId GetBlockResult;
    }
}
