using Lithforge.Runtime.Debug;
using Lithforge.Runtime.UI.Screens;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Centralizes show/hide control for all gameplay HUD elements.
    /// Used by the spawn system to hide HUDs during loading and reveal them on spawn.
    /// </summary>
    public sealed class HudVisibilityController
    {
        private readonly CrosshairHUD _crosshairHud;
        private readonly HotbarDisplay _hotbarDisplay;
        private readonly IContainerScreen _inventoryScreen;
        private readonly F3DebugOverlay _debugOverlay;
        private readonly SettingsScreen _settingsScreen;
        private readonly PauseMenuScreen _pauseMenuScreen;
        private readonly ContainerScreenManager _screenManager;

        public HudVisibilityController(
            CrosshairHUD crosshairHud,
            HotbarDisplay hotbarDisplay,
            IContainerScreen inventoryScreen,
            F3DebugOverlay debugOverlay,
            SettingsScreen settingsScreen,
            PauseMenuScreen pauseMenuScreen,
            ContainerScreenManager screenManager)
        {
            _crosshairHud = crosshairHud;
            _hotbarDisplay = hotbarDisplay;
            _inventoryScreen = inventoryScreen;
            _debugOverlay = debugOverlay;
            _settingsScreen = settingsScreen;
            _pauseMenuScreen = pauseMenuScreen;
            _screenManager = screenManager;
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

            if (_hotbarDisplay != null)
            {
                _hotbarDisplay.SetVisible(false);
            }

            if (_inventoryScreen != null)
            {
                _inventoryScreen.SetVisible(false);
            }

            if (_debugOverlay != null)
            {
                _debugOverlay.SetVisible(false);
            }

            if (_settingsScreen != null)
            {
                _settingsScreen.SetVisible(false);
            }

            if (_pauseMenuScreen != null)
            {
                _pauseMenuScreen.SetVisible(false);
            }

            if (_screenManager != null)
            {
                _screenManager.SetAllVisible(false);
            }
        }

        /// <summary>
        /// Shows gameplay HUD elements after spawn is complete.
        /// InventoryScreen is deliberately left hidden — the player opens it with E.
        /// Block entity screens are shown lazily by ContainerScreenManager
        /// when they are first opened — no SetAllVisible(true) needed here.
        /// </summary>
        public void ShowGameplay()
        {
            if (_crosshairHud != null)
            {
                _crosshairHud.SetVisible(true);
            }

            if (_hotbarDisplay != null)
            {
                _hotbarDisplay.SetVisible(true);
            }

            if (_debugOverlay != null)
            {
                _debugOverlay.SetVisible(true);
            }

            // Restore InventoryScreen root so the E-key toggle can show _panel.
            // The panel itself starts hidden — SetVisible only controls the UIDocument root.
            if (_inventoryScreen != null)
            {
                _inventoryScreen.SetVisible(true);
            }

            // Restore SettingsScreen root so the pause menu can open it.
            if (_settingsScreen != null)
            {
                _settingsScreen.SetVisible(true);
            }

            // Restore PauseMenuScreen root so the Escape key toggle can show it.
            if (_pauseMenuScreen != null)
            {
                _pauseMenuScreen.SetVisible(true);
            }
        }
    }
}
