using System.Collections.Generic;
using Lithforge.Voxel.Command;
using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    /// Abstraction over command execution. Singleplayer uses
    /// <see cref="LocalCommandProcessor"/> which validates and applies commands
    /// directly to world state. A future multiplayer implementation would
    /// serialize commands to the server for authoritative processing.
    /// </summary>
    public interface ICommandProcessor
    {
        /// <summary>
        /// Validates and executes a block placement command.
        /// Checks that the placement position is air and does not overlap the player.
        /// Follows the fill pattern: caller owns <paramref name="dirtiedChunks"/>,
        /// callee clears and appends.
        /// </summary>
        public CommandResult ProcessPlace(in PlaceBlockCommand command, List<int3> dirtiedChunks);

        /// <summary>
        /// Validates and executes a block break command.
        /// Checks that the target block exists (is not air).
        /// Follows the fill pattern: caller owns <paramref name="dirtiedChunks"/>,
        /// callee clears and appends.
        /// </summary>
        public CommandResult ProcessBreak(in BreakBlockCommand command, List<int3> dirtiedChunks);

        /// <summary>
        /// Validates and executes a block entity interaction command
        /// (e.g., open container, use item on block).
        /// </summary>
        public CommandResult ProcessInteract(in InteractCommand command);

        /// <summary>
        /// Validates and executes an inventory slot click command
        /// (left click, right click, shift-click, paint drag, number key swap).
        /// </summary>
        public CommandResult ProcessSlotClick(in SlotClickCommand command);
    }
}
