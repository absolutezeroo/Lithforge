using System;
using System.Collections.Generic;

using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    public sealed class HudSubsystem : IGameSubsystem
    {
        public string Name
        {
            get
            {
                return "HUD";
            }
        }

        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
            typeof(SpawnSubsystem),
        };

        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        public void Initialize(SessionContext context)
        {
            PlayerTransformHolder player = context.Get<PlayerTransformHolder>();
            PanelSettings panelSettings = SessionInitArgsHolder.Current?.PanelSettings;

            // Crosshair
            GameObject crosshairObject = new("CrosshairHUD");
            CrosshairHUD crosshairHUD = crosshairObject.AddComponent<CrosshairHUD>();
            crosshairHUD.Initialize(panelSettings);

            // Hotbar
            GameObject hotbarObject = new("HotbarDisplay");
            HotbarDisplay hotbarDisplay = hotbarObject.AddComponent<HotbarDisplay>();
            hotbarDisplay.Initialize(
                player.Inventory, panelSettings,
                context.Content.ItemRegistry,
                context.Content.ItemSpriteAtlas,
                context.Content.ToolPartTextures);

            context.Register(crosshairHUD);
            context.Register(hotbarDisplay);
        }

        public void PostInitialize(SessionContext context)
        {
            // Wire loading screen to spawn manager
            SessionInitArgs args = SessionInitArgsHolder.Current;

            if (args?.LoadingScreen != null && context.TryGet(out SpawnManager spawn))
            {
                CrosshairHUD crosshair = context.Get<CrosshairHUD>();
                HotbarDisplay hotbar = context.Get<HotbarDisplay>();

                HudVisibilityController hudVisibility = new(
                    crosshair, hotbar, null, null, null, null, null);
                hudVisibility.HideAll();

                args.LoadingScreen.SetSpawnManager(spawn, () => { hudVisibility.ShowGameplay(); });

                context.Register(hudVisibility);
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
