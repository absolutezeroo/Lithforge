using System.Collections.Generic;

using Lithforge.Runtime.Network;
using Lithforge.Runtime.Simulation;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Network-aware command processor. Forwards block place and break commands to
    ///     <see cref="ClientBlockPredictor" /> for optimistic local application and
    ///     server-side validation. Delegates inventory commands to a local processor.
    ///     Returns <see cref="CommandResult.Success" /> optimistically for block operations;
    ///     actual rejection arrives via <c>AcknowledgeBlockChangeMessage</c> and is
    ///     handled by the predictor's revert logic.
    /// </summary>
    public sealed class NetworkCommandProcessor : ICommandProcessor
    {
        /// <summary>Processor for inventory slot click commands (executed locally).</summary>
        private readonly IInventoryCommandProcessor _inventoryProcessor;

        /// <summary>Client-side block predictor for optimistic placement and breaking.</summary>
        private readonly ClientBlockPredictor _predictor;

        /// <summary>Creates a network command processor with a predictor and local inventory processor.</summary>
        public NetworkCommandProcessor(
            ClientBlockPredictor predictor,
            IInventoryCommandProcessor inventoryProcessor)
        {
            _predictor = predictor;
            _inventoryProcessor = inventoryProcessor;
        }

        /// <summary>Optimistically predicts block placement and sends to server for validation.</summary>
        public CommandResult ProcessPlace(in PlaceBlockCommand command, List<int3> dirtiedChunks)
        {
            if (!_predictor.IsReady)
            {
                return CommandResult.InvalidAction;
            }

            _predictor.PredictPlace(command.Position, command.BlockState, (byte)command.Face);
            dirtiedChunks.Clear();
            return CommandResult.Success;
        }

        /// <summary>Optimistically predicts block breaking and sends to server for validation.</summary>
        public CommandResult ProcessBreak(in BreakBlockCommand command, List<int3> dirtiedChunks)
        {
            if (!_predictor.IsReady)
            {
                return CommandResult.InvalidAction;
            }

            _predictor.PredictBreak(command.Position);
            dirtiedChunks.Clear();
            return CommandResult.Success;
        }

        /// <summary>No-op interaction for network mode (placeholder for future server-side handling).</summary>
        public CommandResult ProcessInteract(in InteractCommand command)
        {
            return CommandResult.Success;
        }

        /// <summary>Delegates the inventory slot click to the local inventory processor.</summary>
        public CommandResult ProcessSlotClick(in SlotClickCommand command)
        {
            return _inventoryProcessor.ProcessSlotClick(in command);
        }
    }
}
