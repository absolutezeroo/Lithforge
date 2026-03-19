using System;
using System.Collections.Generic;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.Simulation;
using Lithforge.Runtime.Tick;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Block;
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Loot;

using Unity.Mathematics;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class BlockInteractionSubsystem : IGameSubsystem
    {
        private LocalInventoryCommandProcessor _inventoryProcessor;

        public string Name
        {
            get
            {
                return "BlockInteraction";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
            typeof(BlockEntitySubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            ChunkManager chunkManager = context.Get<ChunkManager>();
            PhysicsSettings physics = context.App.Settings.Physics;

            LootResolver lootResolver = new(context.Content.LootTables);

            BlockInteraction blockInteraction =
                player.MainCamera.gameObject.AddComponent<BlockInteraction>();

            blockInteraction.Initialize(
                chunkManager,
                context.Content.NativeStateRegistry,
                context.Content.StateRegistry,
                context.Get<BlockHighlight>(),
                player.Inventory,
                context.Content.ItemRegistry,
                lootResolver,
                context.Content.TagRegistry,
                player.Transform,
                physics,
                player.Controller);

            // Wire command processor (LocalCommandProcessor as default; PostInitialize may
            // replace it with NetworkCommandProcessor when ClientBlockPredictor is available)
            _inventoryProcessor = new LocalInventoryCommandProcessor(player.Inventory);
            LocalCommandProcessor commandProcessor = new(
                chunkManager,
                context.Content.NativeStateRegistry,
                player.Transform,
                physics.PlayerHalfWidth,
                physics.PlayerHeight,
                _inventoryProcessor);
            blockInteraction.SetCommandProcessor(commandProcessor);

            // Wire block entity references
            BlockEntityTickScheduler beScheduler = context.Get<BlockEntityTickScheduler>();
            blockInteraction.SetBlockEntityReferences(
                beScheduler, null, context.Content.ToolTraitRegistry);

            context.Register(blockInteraction);
        }

        public void PostInitialize(SessionContext context)
        {
            BlockInteraction blockInteraction = context.Get<BlockInteraction>();

            // Wire tick adapter
            if (context.TryGet(out TickRegistry registry))
            {
                registry.Register(new MiningTickAdapter(blockInteraction));
            }

            // Wire block commands through the network when ClientBlockPredictor is available.
            // This replaces LocalCommandProcessor with NetworkCommandProcessor so that
            // place/break commands go through optimistic prediction + server validation.
            if (context.TryGet(out ClientBlockPredictor predictor))
            {
                blockInteraction.SetStartDiggingCallback(predictor.SendStartDigging);

                NetworkCommandProcessor networkProcessor = new(predictor, _inventoryProcessor);
                blockInteraction.SetCommandProcessor(networkProcessor);

                // Wire prediction expiry sweep into the tick loop (reuses registry from above)
                registry?.Register(new BlockPredictionTickAdapter(predictor));

                // Wire collision override so physics resolves against server-confirmed state,
                // not optimistically-applied predictions (prevents cascading mispredictions)
                if (context.TryGet(out PlayerTransformHolder player))
                {
                    player.PhysicsBody.SetCollisionOverride(coord =>
                    {
                        if (predictor.TryGetOriginalState(coord, out StateId originalState))
                        {
                            return originalState;
                        }

                        return null;
                    });
                }
            }
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
