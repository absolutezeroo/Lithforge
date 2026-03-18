using System.Collections.Generic;

using Lithforge.Physics;
using Lithforge.Runtime.Simulation;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Command;

using Unity.Mathematics;

using UnityEngine;

namespace Lithforge.Runtime.Input
{
    /// <summary>
    ///     Singleplayer command processor that validates and executes commands
    ///     directly against local world state. Handles block placement validation
    ///     (air check, player overlap) and block break validation (non-air check).
    ///     Inventory side effects (item consumption, loot drops, durability) remain
    ///     in <see cref="BlockInteraction" /> for now and will be migrated when
    ///     MiningService is extracted (P2.3).
    /// </summary>
    public sealed class LocalCommandProcessor : ICommandProcessor
    {
        private readonly ChunkManager _chunkManager;
        private readonly IInventoryCommandProcessor _inventoryProcessor;
        private readonly NativeStateRegistry _nativeStateRegistry;
        private readonly float _playerHalfWidth;
        private readonly float _playerHeight;
        private readonly Transform _playerTransform;

        public LocalCommandProcessor(
            ChunkManager chunkManager,
            NativeStateRegistry nativeStateRegistry,
            Transform playerTransform,
            float playerHalfWidth,
            float playerHeight,
            IInventoryCommandProcessor inventoryProcessor)
        {
            _chunkManager = chunkManager;
            _nativeStateRegistry = nativeStateRegistry;
            _playerTransform = playerTransform;
            _playerHalfWidth = playerHalfWidth;
            _playerHeight = playerHeight;
            _inventoryProcessor = inventoryProcessor;
        }

        public CommandResult ProcessPlace(in PlaceBlockCommand command, List<int3> dirtiedChunks)
        {
            // PlaceBlockCommand.Position is already the target air block coordinate
            int3 placeCoord = command.Position;

            // Check the target position is air or a replaceable fluid
            StateId existing = _chunkManager.GetBlock(placeCoord);

            if (existing != StateId.Air)
            {
                bool isFluid = _nativeStateRegistry.States.IsCreated &&
                               existing.Value < _nativeStateRegistry.States.Length &&
                               _nativeStateRegistry.States[existing.Value].IsFluid;

                if (!isFluid)
                {
                    return CommandResult.TargetOccupied;
                }
            }

            // Check that the placed block does not overlap the player AABB
            if (_playerTransform != null)
            {
                float3 feetPos = new(
                    _playerTransform.position.x,
                    _playerTransform.position.y,
                    _playerTransform.position.z);

                Aabb playerBox = new(
                    new float3(
                        feetPos.x - _playerHalfWidth,
                        feetPos.y,
                        feetPos.z - _playerHalfWidth),
                    new float3(
                        feetPos.x + _playerHalfWidth,
                        feetPos.y + _playerHeight,
                        feetPos.z + _playerHalfWidth));

                Aabb blockBox = Aabb.FromBlockCoord(placeCoord);

                if (playerBox.Intersects(blockBox))
                {
                    return CommandResult.PlayerOverlap;
                }
            }

            // Execute placement (fill pattern: caller owns list, callee clears + adds)
            dirtiedChunks.Clear();
            _chunkManager.SetBlock(placeCoord, command.BlockState, dirtiedChunks);

            return CommandResult.Success;
        }

        public CommandResult ProcessBreak(in BreakBlockCommand command, List<int3> dirtiedChunks)
        {
            StateId stateId = _chunkManager.GetBlock(command.Position);

            if (stateId == StateId.Air)
            {
                return CommandResult.BlockNotFound;
            }

            // Hardness/breakability is validated during mining progression
            // (BlockInteraction.StartMining). By the time ProcessBreak is called,
            // mining has completed and the break is authorized.

            // Execute break — set to air (fill pattern: caller owns list)
            dirtiedChunks.Clear();
            _chunkManager.SetBlock(command.Position, StateId.Air, dirtiedChunks);

            return CommandResult.Success;
        }

        public CommandResult ProcessInteract(in InteractCommand command)
        {
            // Block entity interaction (open container, use item) will be wired
            // when BlockInteraction delegates to this processor.
            return CommandResult.Success;
        }

        public CommandResult ProcessSlotClick(in SlotClickCommand command)
        {
            return _inventoryProcessor.ProcessSlotClick(in command);
        }
    }
}
