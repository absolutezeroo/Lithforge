using Lithforge.Runtime.Debug;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Centralizes show/hide control for all gameplay HUD elements.
    /// Used by the spawn system to hide HUDs during loading and reveal them on spawn.
    /// </summary>
    public sealed class HudVisibilityController
    {
        private readonly CrosshairHUD _crosshairHud;
        private readonly HotbarHUD _hotbarHud;
        private readonly InventoryScreen _inventoryScreen;
        private readonly DebugOverlayHUD _debugHud;

        public HudVisibilityController(
            CrosshairHUD crosshairHud,
            HotbarHUD hotbarHud,
            InventoryScreen inventoryScreen,
            DebugOverlayHUD debugHud)
        {
            _crosshairHud = crosshairHud;
            _hotbarHud = hotbarHud;
            _inventoryScreen = inventoryScreen;
            _debugHud = debugHud;
        }

        /// <summary>
        /// Hides all gameplay HUD elements. Called at startup before spawn is complete.
        /// </summary>
        public void HideAll()
        {
            if (_crosshairHud != null)
            {
                _crosshairHud.SetVisible(false);
            }

            if (_hotbarHud != null)
            {
                _hotbarHud.SetVisible(false);
            }

            if (_inventoryScreen != null)
            {
                _inventoryScreen.SetVisible(false);
            }

            if (_debugHud != null)
            {
                _debugHud.SetVisible(false);
            }
        }

        /// <summary>
        /// Shows gameplay HUD elements after spawn is complete.
        /// InventoryScreen is deliberately left hidden — the player opens it with E.
        /// </summary>
        public void ShowGameplay()
        {
            if (_crosshairHud != null)
            {
                _crosshairHud.SetVisible(true);
            }

            if (_hotbarHud != null)
            {
                _hotbarHud.SetVisible(true);
            }

            if (_debugHud != null)
            {
                _debugHud.SetVisible(true);
            }

            // Restore InventoryScreen root so the E-key toggle can show _panel.
            // The panel itself starts hidden — SetVisible only controls the UIDocument root.
            if (_inventoryScreen != null)
            {
                _inventoryScreen.SetVisible(true);
            }
        }
    }
}
