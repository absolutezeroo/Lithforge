using System;
using System.Collections.Generic;

using Lithforge.Runtime.UI;
using Lithforge.Runtime.UI.Screens;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>Subsystem that creates the crosshair and hotbar HUD elements.</summary>
    public sealed class HudSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "HUD";
            }
        }

        /// <summary>Depends on player subsystem for inventory and item data.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(PlayerSubsystem),
        };

        /// <summary>Only created for sessions that render.</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the crosshair and hotbar display GameObjects.</summary>
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

        /// <summary>Creates the HUD visibility controller and wires loading screen dismissal.</summary>
        public void PostInitialize(SessionContext context)
        {
            // Create HudVisibilityController and register it for other subsystems.
            // Loading screen progress wiring is handled by ClientChunkHandlerSubsystem.
            SessionInitArgs args = SessionInitArgsHolder.Current;

            if (args?.LoadingScreen != null)
            {
                CrosshairHUD crosshair = context.Get<CrosshairHUD>();
                HotbarDisplay hotbar = context.Get<HotbarDisplay>();

                HudVisibilityController hudVisibility = new(
                    crosshair, hotbar, null, null, null, null, null);
                hudVisibility.HideAll();

                context.Register(hudVisibility);
            }
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>No owned disposable resources; HUD GameObjects cleaned up separately.</summary>
        public void Dispose()
        {
        }
    }
}
