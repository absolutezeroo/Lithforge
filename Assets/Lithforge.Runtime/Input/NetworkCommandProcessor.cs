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
        private readonly IInventoryCommandProcessor _inventoryProcessor;
        private readonly ClientBlockPredictor _predictor;

        public NetworkCommandProcessor(
            ClientBlockPredictor predictor,
            IInventoryCommandProcessor inventoryProcessor)
        {
            _predictor = predictor;
            _inventoryProcessor = inventoryProcessor;
        }

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

        public CommandResult ProcessInteract(in InteractCommand command)
        {
            return CommandResult.Success;
        }

        public CommandResult ProcessSlotClick(in SlotClickCommand command)
        {
            return _inventoryProcessor.ProcessSlotClick(in command);
        }
    }
}
