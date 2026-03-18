using System;

using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    /// Title screen with five buttons: Singleplayer, Host Game, Join Game, Settings, Quit.
    /// Routes to the appropriate sub-screen via <see cref="ScreenManager"/> push.
    /// </summary>
    public sealed class MainMenuScreen : MonoBehaviour, IScreen
    {
        private static readonly Color s_backgroundColor = new(0.06f, 0.06f, 0.08f, 1.0f);
        private static readonly Color s_panelColor = new(0.10f, 0.10f, 0.13f, 0.95f);
        private static readonly Color s_buttonColor = new(0.18f, 0.40f, 0.22f, 1.0f);
        private static readonly Color s_buttonHoverColor = new(0.22f, 0.50f, 0.28f, 1.0f);
        private static readonly Color s_secondaryButtonColor = new(0.22f, 0.22f, 0.28f, 1.0f);
        private static readonly Color s_secondaryButtonHoverColor = new(0.30f, 0.30f, 0.38f, 1.0f);
        private static readonly Color s_quitButtonColor = new(0.45f, 0.18f, 0.18f, 1.0f);
        private static readonly Color s_quitButtonHoverColor = new(0.58f, 0.22f, 0.22f, 1.0f);
        private static readonly Color s_textColor = new(0.92f, 0.92f, 0.90f, 1.0f);
        private static readonly Color s_dimTextColor = new(0.50f, 0.50f, 0.48f, 1.0f);
        private static readonly Color s_logoColor = new(1.0f, 0.95f, 0.80f, 1.0f);

        private UIDocument _document;
        private ScreenManager _screenManager;

        public string ScreenName { get { return ScreenNames.MainMenu; } }
        public bool IsInputOpaque { get { return true; } }
        public bool RequiresCursor { get { return true; } }

        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
            }
        }

        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            onComplete();
        }

        public bool HandleEscape()
        {
            // Main menu is the root — Escape does nothing
            return true;
        }

        /// <summary>
        /// Initializes the main menu with its UIDocument and screen manager reference.
        /// </summary>
        public void Initialize(PanelSettings panelSettings, ScreenManager screenManager)
        {
            _screenManager = screenManager;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 700;

            BuildUI(_document.rootVisualElement);
        }

        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            VisualElement background = new()
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    backgroundColor = s_backgroundColor,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    flexDirection = FlexDirection.Column,
                },
            };
            root.Add(background);

            // Logo
            Label logo = new("LITHFORGE")
            {
                style =
                {
                    fontSize = 64,
                    color = s_logoColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 6,
                    marginBottom = 8,
                },
            };
            background.Add(logo);

            // Subtitle
            Label subtitle = new("Voxel Creation Platform")
            {
                style =
                {
                    fontSize = 16,
                    color = s_dimTextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 40,
                },
            };
            background.Add(subtitle);

            // Button panel
            VisualElement panel = new()
            {
                style =
                {
                    width = 320,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 28,
                    paddingBottom = 28,
                    paddingLeft = 36,
                    paddingRight = 36,
                    alignItems = Align.Center,
                },
            };
            background.Add(panel);

            Button singleplayerBtn = BuildMenuButton(
                "Singleplayer", s_buttonColor, s_buttonHoverColor);
            singleplayerBtn.clicked += OnSingleplayerClicked;
            panel.Add(singleplayerBtn);

            Button hostBtn = BuildMenuButton(
                "Host Game", s_buttonColor, s_buttonHoverColor);
            hostBtn.clicked += OnHostGameClicked;
            panel.Add(hostBtn);

            Button joinBtn = BuildMenuButton(
                "Join Game", s_buttonColor, s_buttonHoverColor);
            joinBtn.clicked += OnJoinGameClicked;
            panel.Add(joinBtn);

            Button settingsBtn = BuildMenuButton(
                "Settings", s_secondaryButtonColor, s_secondaryButtonHoverColor);
            settingsBtn.clicked += OnSettingsClicked;
            panel.Add(settingsBtn);

            Button quitBtn = BuildMenuButton(
                "Quit", s_quitButtonColor, s_quitButtonHoverColor);
            quitBtn.clicked += OnQuitClicked;
            panel.Add(quitBtn);

            // Version label
            Label version = new($"v{Application.version}")
            {
                style =
                {
                    fontSize = 12,
                    color = s_dimTextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 20,
                },
            };
            background.Add(version);
        }

        private void OnSingleplayerClicked()
        {
            _screenManager.Push(ScreenNames.WorldSelection, "singleplayer");
        }

        private void OnHostGameClicked()
        {
            _screenManager.Push(ScreenNames.WorldSelection, "host");
        }

        private void OnJoinGameClicked()
        {
            _screenManager.Push(ScreenNames.JoinGame);
        }

        private void OnSettingsClicked()
        {
            _screenManager.Push(ScreenNames.Settings);
        }

        private void OnQuitClicked()
        {
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
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
