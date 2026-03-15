using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Minecraft-style pause overlay. Appears over the world when Escape is pressed
    /// during gameplay. Buttons delegate to callers via injected Actions.
    /// sortingOrder=400: above SettingsScreen(300), below LoadingScreen(500).
    /// </summary>
    public sealed class PauseMenuScreen : MonoBehaviour
    {
        private static readonly Color s_overlayColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color s_panelColor = new Color(0.12f, 0.12f, 0.15f, 0.97f);
        private static readonly Color s_buttonColor = new Color(0.22f, 0.22f, 0.28f, 1f);
        private static readonly Color s_buttonHoverColor = new Color(0.30f, 0.30f, 0.38f, 1f);
        private static readonly Color s_quitButtonColor = new Color(0.50f, 0.20f, 0.20f, 1f);
        private static readonly Color s_quitButtonHoverColor = new Color(0.65f, 0.25f, 0.25f, 1f);
        private static readonly Color s_textColor = Color.white;

        private UIDocument _document;
        private VisualElement _overlay;
        private bool _isOpen;

        private Action _onPause;
        private Action _onResume;
        private Action _onOptions;
        private Action _onQuitToTitle;

        private SettingsScreen _settingsScreen;

        public bool IsOpen
        {
            get { return _isOpen; }
        }

        /// <summary>
        /// Initializes the pause menu with its dependencies and callback actions.
        /// </summary>
        /// <param name="panelSettings">Shared PanelSettings for UIDocument.</param>
        /// <param name="settingsScreen">Reference for Escape-key coordination.</param>
        /// <param name="onPause">Called when Escape opens the pause menu (set game state).</param>
        /// <param name="onResume">Called when Resume button or Escape closes pause (restore game state).</param>
        /// <param name="onOptions">Called when Options button is clicked (hide pause, show settings).</param>
        /// <param name="onQuitToTitle">Called when Save and Quit is clicked.</param>
        public void Initialize(
            PanelSettings panelSettings,
            SettingsScreen settingsScreen,
            Action onPause,
            Action onResume,
            Action onOptions,
            Action onQuitToTitle)
        {
            _settingsScreen = settingsScreen;
            _onPause = onPause;
            _onResume = onResume;
            _onOptions = onOptions;
            _onQuitToTitle = onQuitToTitle;

            _document = gameObject.AddComponent<UIDocument>();
            _document.sortingOrder = 400;

            if (panelSettings != null)
            {
                _document.panelSettings = panelSettings;
            }

            BuildUI(_document.rootVisualElement);
            _overlay.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (!keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Priority 1: If settings is open (from Options), close it back to pause menu
            if (_settingsScreen != null && _settingsScreen.IsOpen)
            {
                _settingsScreen.Close(true);
                Open();
                return;
            }

            // Priority 2: If pause menu is open, resume game
            if (_isOpen)
            {
                if (_onResume != null)
                {
                    _onResume();
                }

                return;
            }

            // Priority 3: If playing and cursor locked, enter pause
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                if (_onPause != null)
                {
                    _onPause();
                }

                return;
            }
        }

        public void Open()
        {
            _isOpen = true;
            _overlay.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            _isOpen = false;
            _overlay.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        /// Hides the pause overlay without re-locking the cursor.
        /// Used when transitioning to another screen (e.g. settings) that
        /// manages its own cursor state.
        /// </summary>
        public void HideOverlay()
        {
            _isOpen = false;
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Controls root document visibility (used by HudVisibilityController).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.backgroundColor = s_overlayColor;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            root.Add(_overlay);

            VisualElement panel = new VisualElement();
            panel.style.width = 340;
            panel.style.backgroundColor = s_panelColor;
            panel.style.borderTopLeftRadius = 8;
            panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = 8;
            panel.style.borderBottomRightRadius = 8;
            panel.style.paddingTop = 32;
            panel.style.paddingBottom = 32;
            panel.style.paddingLeft = 40;
            panel.style.paddingRight = 40;
            panel.style.alignItems = Align.Center;
            _overlay.Add(panel);

            Label title = new Label("Game Paused");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = s_textColor;
            title.style.marginBottom = 28;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(title);

            Button resumeBtn = BuildMenuButton("Resume Game", s_buttonColor, s_buttonHoverColor);
            resumeBtn.clicked += () =>
            {
                if (_onResume != null)
                {
                    _onResume();
                }
            };
            panel.Add(resumeBtn);

            Button optionsBtn = BuildMenuButton("Options...", s_buttonColor, s_buttonHoverColor);
            optionsBtn.clicked += () =>
            {
                if (_onOptions != null)
                {
                    _onOptions();
                }
            };
            panel.Add(optionsBtn);

            Button quitBtn = BuildMenuButton(
                "Save and Quit to Title", s_quitButtonColor, s_quitButtonHoverColor);
            quitBtn.clicked += () =>
            {
                if (_onQuitToTitle != null)
                {
                    _onQuitToTitle();
                }
            };
            panel.Add(quitBtn);
        }

        private Button BuildMenuButton(string text, Color normalColor, Color hoverColor)
        {
            Button btn = new Button();
            btn.text = text;
            btn.style.width = new Length(100, LengthUnit.Percent);
            btn.style.height = 44;
            btn.style.fontSize = 16;
            btn.style.color = s_textColor;
            btn.style.backgroundColor = normalColor;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.marginBottom = 10;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;

            btn.RegisterCallback<PointerEnterEvent>((PointerEnterEvent evt) =>
            {
                btn.style.backgroundColor = hoverColor;
            });

            btn.RegisterCallback<PointerLeaveEvent>((PointerLeaveEvent evt) =>
            {
                btn.style.backgroundColor = normalColor;
            });

            return btn;
        }
    }
}
