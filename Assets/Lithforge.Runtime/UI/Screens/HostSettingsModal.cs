using System;

using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.UIElements;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Modal overlay that appears after world selection in Host mode.
    ///     Collects server port, max players, then produces a
    ///     <see cref="SessionConfig.Host" /> and invokes the session callback.
    /// </summary>
    public sealed class HostSettingsModal : MonoBehaviour, IScreen
    {
        /// <summary>Semi-transparent black overlay behind the modal panel.</summary>
        private static readonly Color s_overlayColor = new(0.0f, 0.0f, 0.0f, 0.7f);

        /// <summary>Background color for the modal panel.</summary>
        private static readonly Color s_panelColor = new(0.10f, 0.10f, 0.13f, 0.95f);

        /// <summary>Standard text color for labels and titles.</summary>
        private static readonly Color s_textColor = new(0.92f, 0.92f, 0.90f, 1.0f);

        /// <summary>Dimmed text color for field labels.</summary>
        private static readonly Color s_dimTextColor = new(0.50f, 0.50f, 0.48f, 1.0f);

        /// <summary>Background color for text input fields.</summary>
        private static readonly Color s_fieldBgColor = new(0.05f, 0.05f, 0.07f, 1.0f);

        /// <summary>Border color for text input fields.</summary>
        private static readonly Color s_fieldBorderColor = new(0.25f, 0.25f, 0.30f, 1.0f);

        /// <summary>Normal color for the Start Hosting button.</summary>
        private static readonly Color s_buttonColor = new(0.18f, 0.40f, 0.22f, 1.0f);

        /// <summary>Hover color for the Start Hosting button.</summary>
        private static readonly Color s_buttonHoverColor = new(0.22f, 0.50f, 0.28f, 1.0f);

        /// <summary>Normal color for the Cancel button.</summary>
        private static readonly Color s_cancelButtonColor = new(0.22f, 0.22f, 0.28f, 1.0f);

        /// <summary>Hover color for the Cancel button.</summary>
        private static readonly Color s_cancelButtonHoverColor = new(0.30f, 0.30f, 0.38f, 1.0f);

        /// <summary>Display name of the selected world, received from the context.</summary>
        private string _displayName;

        /// <summary>UI Toolkit document hosting the modal overlay.</summary>
        private UIDocument _document;

        /// <summary>Game mode of the selected world (creative/survival).</summary>
        private GameMode _gameMode;

        /// <summary>Whether the selected world is newly created or existing.</summary>
        private bool _isNewWorld;

        /// <summary>Text field for the maximum number of players allowed on the server.</summary>
        private TextField _maxPlayersField;

        /// <summary>Callback invoked with the session config when the user clicks Start Hosting.</summary>
        private Action<SessionConfig> _onSessionCreated;

        /// <summary>Text field for the server port number.</summary>
        private TextField _portField;

        /// <summary>Screen manager for navigating back to the world selection screen.</summary>
        private ScreenManager _screenManager;

        /// <summary>World seed received from the context for new world creation.</summary>
        private long _seed;

        /// <summary>Logger for host settings modal diagnostics.</summary>
        private ILogger _logger;

        /// <summary>File system path of the selected world, received from the context.</summary>
        private string _worldPath;

        /// <summary>Returns the screen name identifier for the host settings modal.</summary>
        public string ScreenName { get { return ScreenNames.HostSettings; } }

        /// <summary>Returns true because this modal consumes all input.</summary>
        public bool IsInputOpaque { get { return true; } }

        /// <summary>Returns true because this modal requires a visible cursor for its fields and buttons.</summary>
        public bool RequiresCursor { get { return true; } }

        /// <summary>Shows the modal and extracts world data from the HostWorldContext.</summary>
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

        /// <summary>Hides the modal and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            onComplete();
        }

        /// <summary>Returns false to allow the screen manager to pop back to world selection.</summary>
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
            Action<SessionConfig> onSessionCreated,
            ILogger logger = null)
        {
            _screenManager = screenManager;
            _onSessionCreated = onSessionCreated;
            _logger = logger;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 750;

            BuildUI(_document.rootVisualElement);

            // Start hidden
            _document.rootVisualElement.style.display = DisplayStyle.None;
        }

        /// <summary>Constructs the modal overlay with port field, max players field, and action buttons.</summary>
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

        /// <summary>Validates port and max players fields, builds a Host session config, and invokes the callback.</summary>
        private void OnStartClicked()
        {
            string portText = _portField.value?.Trim();
            string maxPlayersText = _maxPlayersField.value?.Trim();

            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            {
                _logger?.LogWarning("[HostSettingsModal] Invalid port.");

                return;
            }

            if (!int.TryParse(maxPlayersText, out int maxPlayers) || maxPlayers < 1)
            {
                _logger?.LogWarning("[HostSettingsModal] Invalid max players.");

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

        /// <summary>Pops the screen manager stack to dismiss the modal.</summary>
        private void OnCancelClicked()
        {
            _screenManager.Pop();
        }

        /// <summary>Applies consistent dark-theme styling to a text input field including inner text element.</summary>
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

            // Style the inner text input element (UI Toolkit nests it under #unity-text-input)
            VisualElement textInput = field.Q("unity-text-input");
            if (textInput != null)
            {
                textInput.style.color = s_textColor;
                textInput.style.backgroundColor = s_fieldBgColor;
                textInput.style.borderTopWidth = 0;
                textInput.style.borderBottomWidth = 0;
                textInput.style.borderLeftWidth = 0;
                textInput.style.borderRightWidth = 0;
            }
        }

        /// <summary>Creates a styled button with hover color transition effects.</summary>
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
