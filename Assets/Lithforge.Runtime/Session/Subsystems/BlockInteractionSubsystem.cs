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
    public sealed class BlockInteractionSubsystem : IGameSubsystem
    {
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

            // Wire command processor
            LocalInventoryCommandProcessor inventoryProcessor = new(player.Inventory);
            LocalCommandProcessor commandProcessor = new(
                chunkManager,
                context.Content.NativeStateRegistry,
                player.Transform,
                physics.PlayerHalfWidth,
                physics.PlayerHeight,
                inventoryProcessor);
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

            // Wire StartDigging to client predictor if in client mode
            if (context.TryGet(out ClientBlockPredictor predictor))
            {
                blockInteraction.SetStartDiggingCallback(predictor.SendStartDigging);
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
