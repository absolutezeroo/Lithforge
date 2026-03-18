using System;
using System.Collections.Generic;

using Lithforge.Runtime.BlockEntity;
using Lithforge.Runtime.BlockEntity.UI;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class ContainerScreenSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "ContainerScreen";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(BlockInteractionSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            // Create ContainerScreenManager
            GameObject screenManagerObject = new("ContainerScreenManager");
            ContainerScreenManager screenManager =
                screenManagerObject.AddComponent<ContainerScreenManager>();

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
                screenManager);

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

        public void PostInitialize(SessionContext context)
        {
            // Wire container screen manager to BlockInteraction
            BlockInteraction blockInteraction = context.Get<BlockInteraction>();
            ContainerScreenManager screenManager = context.Get<ContainerScreenManager>();
            BlockEntityTickScheduler beScheduler = context.Get<BlockEntityTickScheduler>();

            blockInteraction.SetBlockEntityReferences(
                beScheduler, screenManager, context.Content.ToolTraitRegistry);
        }

        public void Shutdown()
        {
        }

        public void Dispose()
        {
        }
    }
}
