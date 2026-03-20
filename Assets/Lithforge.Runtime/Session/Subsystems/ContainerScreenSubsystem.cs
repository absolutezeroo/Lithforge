using System;
using System.Collections.Generic;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.UI;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Network;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates all container UI screens (inventory, chest, furnace, etc.).</summary>
    public sealed class ContainerScreenSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "ContainerScreen";
            }
        }

        /// <summary>Depends on player and block interaction for inventory and entity access.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(BlockInteractionSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the container screen manager and registers all block entity screen factories.</summary>
        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            // Create ContainerScreenManager
            GameObject screenManagerObject = new("ContainerScreenManager");
            ContainerScreenManager screenManager =
                screenManagerObject.AddComponent<ContainerScreenManager>();

            screenManager.SetLogger(context.App.Logger);

            KeyBindingConfig keyBindings = context.Get<KeyBindingConfig>();
            context.TryGet(out ClientInventorySyncHandler syncHandler);

            ScreenContext screenContext = new(
                player.Inventory,
                context.Content.ItemRegistry,
                context.Content.ItemSpriteAtlas,
                panelSettings,
                context.Content.CraftingEngine,
                context.Content.ToolTraitRegistry,
                context.Content.ToolPartTextures,
                context.Content.ToolMaterials,
                context.Content.ToolTemplateRegistry,
                context.Content.PartBuilderRecipeRegistry,
                context.Content.ToolMaterialRegistry,
                context.Content.MaterialInputRegistry,
                screenManager,
                keyBindings,
                context.App.Logger,
                syncHandler);

            // Player inventory screen
            GameObject inventoryObject = new("PlayerInventoryScreen");
            PlayerInventoryScreen inventoryScreen = inventoryObject.AddComponent<PlayerInventoryScreen>();
            inventoryScreen.Initialize(screenContext);

            // Register block entity screen factories
            screenManager.Register(
                ChestBlockEntity.TypeIdValue,
                () =>
                {
                    GameObject obj = new("ChestScreen");
                    ChestScreen screen = obj.AddComponent<ChestScreen>();
                    screen.Initialize(screenContext);
                    return screen;
                },
                (s, e) => ((ChestScreen)s).OpenForEntity(e));

            screenManager.Register(
                FurnaceBlockEntity.TypeIdValue,
                () =>
                {
                    GameObject obj = new("FurnaceScreen");
                    FurnaceScreen screen = obj.AddComponent<FurnaceScreen>();
                    screen.Initialize(screenContext);
                    return screen;
                },
                (s, e) => ((FurnaceScreen)s).OpenForEntity(e));

            screenManager.Register(
                ToolStationBlockEntity.TypeIdValue,
                () =>
                {
                    GameObject obj = new("ToolStationScreen");
                    ToolStationScreen screen = obj.AddComponent<ToolStationScreen>();
                    screen.Initialize(screenContext);
                    return screen;
                },
                (s, e) => ((ToolStationScreen)s).OpenForEntity(e));

            screenManager.Register(
                CraftingTableBlockEntity.TypeIdValue,
                () =>
                {
                    GameObject obj = new("CraftingTableScreen");
                    CraftingTableScreen screen = obj.AddComponent<CraftingTableScreen>();
                    screen.Initialize(screenContext);
                    return screen;
                },
                (s, e) => ((CraftingTableScreen)s).OpenForEntity(e));

            screenManager.Register(
                PartBuilderBlockEntity.TypeIdValue,
                () =>
                {
                    GameObject obj = new("PartBuilderScreen");
                    PartBuilderScreen screen = obj.AddComponent<PartBuilderScreen>();
                    screen.Initialize(screenContext);
                    return screen;
                },
                (s, e) => ((PartBuilderScreen)s).OpenForEntity(e));

            context.Register(screenManager);
            context.Register(inventoryScreen);
        }

        /// <summary>Wires the container screen manager to block interaction for entity UI opening.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Wire container screen manager to BlockInteraction
            BlockInteraction blockInteraction = context.Get<BlockInteraction>();
            ContainerScreenManager screenManager = context.Get<ContainerScreenManager>();
            BlockEntityTickScheduler beScheduler = context.Get<BlockEntityTickScheduler>();

            blockInteraction.SetBlockEntityReferences(
                beScheduler, screenManager, context.Content.ToolTraitRegistry);
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources; GameObjects cleaned up separately.</summary>
        public void Dispose()
        {
        }
    }
}
