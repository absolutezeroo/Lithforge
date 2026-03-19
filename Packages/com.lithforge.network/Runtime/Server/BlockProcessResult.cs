using Lithforge.Voxel.Block;
using Lithforge.Voxel.Command;

namespace Lithforge.Network.Server
{
    /// <summary>
    /// Result of a server-side block command validation and execution.
    /// Carries the <see cref="CommandResult"/> reason, the block state before
    /// and after mutation, and a convenience <see cref="Accepted"/> flag.
    /// Used by <see cref="ServerGameLoop"/> to construct
    /// <see cref="Lithforge.Network.Messages.AcknowledgeBlockChangeMessage"/>.
    /// </summary>
    public struct BlockProcessResult
    {
        /// <summary>
        /// The command result code indicating success or the reason for rejection.
        /// </summary>
        public CommandResult Result;

        /// <summary>
        /// The block state that existed before the command was processed.
        /// </summary>
        public StateId PreviousState;

        /// <summary>
        /// The authoritative block state after processing (unchanged if rejected).
        /// </summary>
        public StateId AcceptedState;

        /// <summary>
        /// Whether the block command was accepted and applied.
        /// </summary>
        public bool Accepted;

        /// <summary>
        /// Creates a rejected result with the given reason, keeping the current block state unchanged.
        /// </summary>
        public static BlockProcessResult Reject(CommandResult reason, StateId currentState)
        {
            return new BlockProcessResult
            {
                Result = reason,
                PreviousState = currentState,
                AcceptedState = currentState,
                Accepted = false,
            };
        }

        /// <summary>
        /// Creates an accepted result recording the state transition from previous to new.
        /// </summary>
        public static BlockProcessResult Accept(StateId previousState, StateId newState)
        {
            return new BlockProcessResult
            {
                Result = CommandResult.Success,
                PreviousState = previousState,
                AcceptedState = newState,
                Accepted = true,
            };
        }
    }
}
