using System;

using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Modal overlay that appears after world selection in Host mode.
    ///     Collects server port, max players, then produces a
    ///     <see cref="SessionConfig.Host" /> and invokes the session callback.
    /// </summary>
    public sealed class HostSettingsModal : MonoBehaviour, IScreen
    {
        private static readonly Color s_overlayColor = new(0.0f, 0.0f, 0.0f, 0.7f);
        private static readonly Color s_panelColor = new(0.10f, 0.10f, 0.13f, 0.95f);
        private static readonly Color s_textColor = new(0.92f, 0.92f, 0.90f, 1.0f);
        private static readonly Color s_dimTextColor = new(0.50f, 0.50f, 0.48f, 1.0f);
        private static readonly Color s_fieldBgColor = new(0.05f, 0.05f, 0.07f, 1.0f);
        private static readonly Color s_fieldBorderColor = new(0.25f, 0.25f, 0.30f, 1.0f);
        private static readonly Color s_buttonColor = new(0.18f, 0.40f, 0.22f, 1.0f);
        private static readonly Color s_buttonHoverColor = new(0.22f, 0.50f, 0.28f, 1.0f);
        private static readonly Color s_cancelButtonColor = new(0.22f, 0.22f, 0.28f, 1.0f);
        private static readonly Color s_cancelButtonHoverColor = new(0.30f, 0.30f, 0.38f, 1.0f);
        private string _displayName;

        private UIDocument _document;
        private GameMode _gameMode;
        private bool _isNewWorld;
        private TextField _maxPlayersField;
        private Action<SessionConfig> _onSessionCreated;

        private TextField _portField;
        private ScreenManager _screenManager;
        private long _seed;

        // World data received from WorldSelectionScreen via context
        private string _worldPath;

        public string ScreenName { get { return ScreenNames.HostSettings; } }
        public bool IsInputOpaque { get { return true; } }
        public bool RequiresCursor { get { return true; } }

        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
            }

            // Extract world data from context
            if (args.Context is HostWorldContext ctx)
            {
                _worldPath = ctx.WorldPath;
                _displayName = ctx.DisplayName;
                _seed = ctx.Seed;
                _gameMode = ctx.GameMode;
                _isNewWorld = ctx.IsNewWorld;
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
            // Let ScreenManager pop us back to WorldSelectionScreen
            return false;
        }

        /// <summary>
        ///     Initializes the host settings modal.
        /// </summary>
        public void Initialize(
            PanelSettings panelSettings,
            ScreenManager screenManager,
            Action<SessionConfig> onSessionCreated)
        {
            _screenManager = screenManager;
            _onSessionCreated = onSessionCreated;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 750;

            BuildUI(_document.rootVisualElement);

            // Start hidden
            _document.rootVisualElement.style.display = DisplayStyle.None;
        }

        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            // Semi-transparent overlay
            VisualElement overlay = new()
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    backgroundColor = s_overlayColor,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                },
            };
            root.Add(overlay);

            // Modal panel
            VisualElement panel = new()
            {
                style =
                {
                    width = 360,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 24,
                    paddingBottom = 24,
                    paddingLeft = 28,
                    paddingRight = 28,
                },
            };
            overlay.Add(panel);

            // Title
            Label title = new("Host Settings")
            {
                style =
                {
                    fontSize = 22,
                    color = s_textColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 20,
                },
            };
            panel.Add(title);

            // Port field
            Label portLabel = new("Server Port")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, marginBottom = 4,
                },
            };
            panel.Add(portLabel);

            _portField = new TextField
            {
                value = "7777",
            };
            ApplyFieldStyle(_portField);
            _portField.style.marginBottom = 16;
            panel.Add(_portField);

            // Max players field
            Label maxPlayersLabel = new("Max Players")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, marginBottom = 4,
                },
            };
            panel.Add(maxPlayersLabel);

            _maxPlayersField = new TextField
            {
                value = "8",
            };
            ApplyFieldStyle(_maxPlayersField);
            _maxPlayersField.style.marginBottom = 24;
            panel.Add(_maxPlayersField);

            // Button row
            VisualElement buttonRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.Center,
                },
            };
            panel.Add(buttonRow);

            Button cancelBtn = BuildButton("Cancel", s_cancelButtonColor, s_cancelButtonHoverColor);
            cancelBtn.style.marginRight = 10;
            cancelBtn.clicked += OnCancelClicked;
            buttonRow.Add(cancelBtn);

            Button startBtn = BuildButton("Start Hosting", s_buttonColor, s_buttonHoverColor);
            startBtn.style.width = 140;
            startBtn.clicked += OnStartClicked;
            buttonRow.Add(startBtn);
        }

        private void OnStartClicked()
        {
            string portText = _portField.value?.Trim();
            string maxPlayersText = _maxPlayersField.value?.Trim();

            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            {
                UnityEngine.Debug.LogWarning("[HostSettingsModal] Invalid port.");

                return;
            }

            if (!int.TryParse(maxPlayersText, out int maxPlayers) || maxPlayers < 1)
            {
                UnityEngine.Debug.LogWarning("[HostSettingsModal] Invalid max players.");

                return;
            }

            SessionConfig.Host hostConfig = new(
                _worldPath,
                _displayName,
                _seed,
                _gameMode,
                _isNewWorld,
                port,
                maxPlayers);

            _onSessionCreated?.Invoke(hostConfig);
        }

        private void OnCancelClicked()
        {
            _screenManager.Pop();
        }

        private void ApplyFieldStyle(TextField field)
        {
            field.style.fontSize = 14;
            field.style.color = s_textColor;
            field.style.backgroundColor = s_fieldBgColor;
            field.style.borderTopWidth = 1;
            field.style.borderBottomWidth = 1;
            field.style.borderLeftWidth = 1;
            field.style.borderRightWidth = 1;
            field.style.borderTopColor = s_fieldBorderColor;
            field.style.borderBottomColor = s_fieldBorderColor;
            field.style.borderLeftColor = s_fieldBorderColor;
            field.style.borderRightColor = s_fieldBorderColor;
            field.style.borderTopLeftRadius = 4;
            field.style.borderTopRightRadius = 4;
            field.style.borderBottomLeftRadius = 4;
            field.style.borderBottomRightRadius = 4;
            field.style.paddingTop = 6;
            field.style.paddingBottom = 6;
            field.style.paddingLeft = 8;
            field.style.paddingRight = 8;
        }

        private Button BuildButton(string text, Color normalColor, Color hoverColor)
        {
            Button btn = new()
            {
                text = text,
                style =
                {
                    width = 110,
                    height = 38,
                    fontSize = 14,
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
