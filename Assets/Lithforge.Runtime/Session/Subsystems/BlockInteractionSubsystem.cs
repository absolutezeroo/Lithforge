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
using Lithforge.Voxel.Chunk;
using Lithforge.Voxel.Loot;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that wires block placement, breaking, and mining interaction for the
    ///     local player. In multiplayer, replaces the local command processor with a
    ///     network-backed one that uses optimistic prediction via ClientBlockPredictor.
    /// </summary>
    public sealed class BlockInteractionSubsystem : IGameSubsystem
    {
        /// <summary>Processes inventory-side effects of block commands (consume items, receive drops).</summary>
        private LocalInventoryCommandProcessor _inventoryProcessor;

        /// <summary>Display name of this subsystem used for diagnostics.</summary>
        public string Name
        {
            get
            {
                return "BlockInteraction";
            }
        }

        /// <summary>Subsystems that must be initialized before this one.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(ChunkManagerSubsystem),
            typeof(BlockEntitySubsystem),
            typeof(ClientChunkHandlerSubsystem),
        };

        /// <summary>Returns true for sessions that require rendering (local player present).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Constructs and wires all block interaction components into the session context.</summary>
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

        /// <summary>Wires network prediction, tick adapters, and collision override when available.</summary>
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

                // Collision override wiring moved to ClientPlayerBodyFactory callback
                // (body doesn't exist yet at PostInitialize time)
            }
        }

        /// <summary>No resources to release on shutdown.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No unmanaged resources held by this subsystem.</summary>
        public void Dispose()
        {
        }
    }
}
