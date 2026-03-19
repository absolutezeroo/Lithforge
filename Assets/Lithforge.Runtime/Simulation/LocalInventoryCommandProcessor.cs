using Lithforge.Voxel.Command;
using Lithforge.Item;

namespace Lithforge.Runtime.Simulation
{
    /// <summary>
    /// Singleplayer inventory command processor. Validates slot bounds and
    /// state ID, then delegates click execution to SlotInteractionController
    /// (via the caller — delegation is deferred until the full command pipeline
    /// is wired). In singleplayer, state ID always matches (no latency), but
    /// the check exists so multiplayer can rely on it.
    ///
    /// Anti-dupe: when full delegation is wired, this processor will count
    /// total items in Inventory + HeldStack before and after execution,
    /// rejecting and rolling back if the total changes.
    /// </summary>
    public sealed class LocalInventoryCommandProcessor : IInventoryCommandProcessor
    {
        /// <summary>The player inventory used for state ID validation.</summary>
        private readonly Inventory _inventory;

        /// <summary>Creates a new local processor backed by the given inventory.</summary>
        public LocalInventoryCommandProcessor(Inventory inventory)
        {
            _inventory = inventory;
        }

        /// <summary>Validates slot bounds, click type, and state ID, returning a result code.</summary>
        public CommandResult ProcessSlotClick(in SlotClickCommand command)
        {
            // Validate slot bounds (-1 is valid: outside container = drop)
            if (command.SlotIndex < -1 || command.SlotIndex >= Inventory.SlotCount)
            {
                return CommandResult.InvalidSlot;
            }

            // Validate click type
            if (command.ClickType > 5)
            {
                return CommandResult.InvalidAction;
            }

            // Validate state ID — in singleplayer this always matches,
            // but the check is here for multiplayer readiness.
            if (command.StateId != _inventory.StateId)
            {
                return CommandResult.StateIdMismatch;
            }

            // Click execution is currently handled by SlotInteractionController
            // at the UI layer. When full command pipeline delegation is wired,
            // this processor will wrap the execution with anti-dupe item count
            // conservation checks.
            return CommandResult.Success;
        }
    }
}
