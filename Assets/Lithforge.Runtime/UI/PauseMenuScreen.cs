using System;

using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     Minecraft-style pause overlay. Appears over the world when Escape is pressed
    ///     during gameplay. Buttons delegate to callers via injected Actions.
    ///     sortingOrder=400: above SettingsScreen(300), below LoadingScreen(500).
    ///     Escape key handling is delegated to <see cref="ScreenManager" /> via
    ///     <see cref="IScreen.HandleEscape" />.
    /// </summary>
    public sealed class PauseMenuScreen : MonoBehaviour, IScreen
    {
        private static readonly Color s_overlayColor = new(0f, 0f, 0f, 0.55f);
        private static readonly Color s_panelColor = new(0.12f, 0.12f, 0.15f, 0.97f);
        private static readonly Color s_buttonColor = new(0.22f, 0.22f, 0.28f, 1f);
        private static readonly Color s_buttonHoverColor = new(0.30f, 0.30f, 0.38f, 1f);
        private static readonly Color s_quitButtonColor = new(0.50f, 0.20f, 0.20f, 1f);
        private static readonly Color s_quitButtonHoverColor = new(0.65f, 0.25f, 0.25f, 1f);
        private static readonly Color s_textColor = Color.white;

        private UIDocument _document;
        private Action _onOptions;

        private Action _onPause;
        private Action _onQuitToTitle;
        private Action _onResume;
        private VisualElement _overlay;

        private SettingsScreen _settingsScreen;

        public bool IsOpen { get; private set; }

        public string ScreenName { get { return ScreenNames.Pause; } }
        public bool IsInputOpaque { get { return true; } }
        public bool RequiresCursor { get { return true; } }

        public void OnShow(ScreenShowArgs args)
        {
            Open();
            _onPause?.Invoke();
        }

        public void OnHide(Action onComplete)
        {
            Close();
            onComplete();
        }

        public bool HandleEscape()
        {
            // If settings is open (from Options), close it back to pause menu
            if (_settingsScreen != null && _settingsScreen.IsOpen)
            {
                _settingsScreen.Close(true);
                Open();
                return true;
            }

            // Otherwise, resume game (pop this screen)
            _onResume?.Invoke();
            return false;
        }

        /// <summary>
        ///     Initializes the pause menu with its dependencies and callback actions.
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

        public void Open()
        {
            IsOpen = true;
            _overlay.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            IsOpen = false;
            _overlay.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        /// <summary>
        ///     Hides the pause overlay without re-locking the cursor or clearing
        ///     the open state. Used when transitioning to another screen (e.g. settings)
        ///     that manages its own cursor state. The pause is still logically active.
        /// </summary>
        public void HideOverlay()
        {
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>
        ///     Controls root document visibility (used by HudVisibilityController).
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

            _overlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    bottom = 0,
                    left = 0,
                    right = 0,
                    backgroundColor = s_overlayColor,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                },
            };
            root.Add(_overlay);

            VisualElement panel = new()
            {
                style =
                {
                    width = 340,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 32,
                    paddingBottom = 32,
                    paddingLeft = 40,
                    paddingRight = 40,
                    alignItems = Align.Center,
                },
            };
            _overlay.Add(panel);

            Label title = new("Game Paused")
            {
                style =
                {
                    fontSize = 28,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_textColor,
                    marginBottom = 28,
                    unityTextAlign = TextAnchor.MiddleCenter,
                },
            };
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
            Button btn = new()
            {
                text = text,
                style =
                {
                    width = new Length(100, LengthUnit.Percent),
                    height = 44,
                    fontSize = 16,
                    color = s_textColor,
                    backgroundColor = normalColor,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    marginBottom = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            };

            btn.RegisterCallback((PointerEnterEvent evt) =>
            {
                btn.style.backgroundColor = hoverColor;
            });

            btn.RegisterCallback((PointerLeaveEvent evt) =>
            {
                btn.style.backgroundColor = normalColor;
            });

            return btn;
        }
    }
}
