using Lithforge.Voxel.Command;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    ///     Abstraction over inventory command validation and execution.
    ///     Singleplayer uses <see cref="LocalInventoryCommandProcessor" /> which
    ///     validates and executes directly. A future multiplayer implementation
    ///     would forward commands to the server for authoritative processing.
    /// </summary>
    public interface IInventoryCommandProcessor
    {
        /// <summary>
        ///     Validates and executes an inventory slot click.
        ///     Returns Success if the action was applied, or a rejection code.
        ///     If stateId mismatch, returns StateIdMismatch and the caller
        ///     must full-resync via Inventory.ApplyFullSnapshot().
        /// </summary>
        public CommandResult ProcessSlotClick(in SlotClickCommand command);
    }
}
