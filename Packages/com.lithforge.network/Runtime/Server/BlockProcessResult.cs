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
        public CommandResult Result;
        public StateId PreviousState;
        public StateId AcceptedState;
        public bool Accepted;

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
