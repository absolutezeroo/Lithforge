using System;

using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Displays connection progress through six stages (Connecting → EnteringWorld)
    ///     with a spinner, stage label, optional progress bar, and a Cancel button.
    ///     Pushed by <see cref="JoinGameScreen" /> or the bootstrap when starting a client
    ///     session. Polls an external <see cref="ConnectionStageProvider" /> delegate each
    ///     frame to update the displayed state.
    /// </summary>
    public sealed class ConnectionProgressScreen : MonoBehaviour, IScreen
    {
        /// <summary>Rotation speed for the spinner in degrees per second.</summary>
        private const float SpinnerSpeed = 360f;
        /// <summary>Semi-transparent background color for the full-screen overlay.</summary>
        private static readonly Color s_backgroundColor = new(0.06f, 0.06f, 0.08f, 0.95f);

        /// <summary>Background color for the center progress panel.</summary>
        private static readonly Color s_panelColor = new(0.10f, 0.10f, 0.13f, 0.95f);

        /// <summary>Standard text color for stage labels.</summary>
        private static readonly Color s_textColor = new(0.92f, 0.92f, 0.90f, 1.0f);

        /// <summary>Dimmed text color for detail labels.</summary>
        private static readonly Color s_dimTextColor = new(0.50f, 0.50f, 0.48f, 1.0f);

        /// <summary>Background color for the progress bar track.</summary>
        private static readonly Color s_progressBgColor = new(0.05f, 0.05f, 0.07f, 1.0f);

        /// <summary>Fill color for the progress bar and spinner border.</summary>
        private static readonly Color s_progressFillColor = new(0.18f, 0.40f, 0.22f, 1.0f);

        /// <summary>Normal color for the Cancel button.</summary>
        private static readonly Color s_cancelButtonColor = new(0.45f, 0.18f, 0.18f, 1.0f);

        /// <summary>Hover color for the Cancel button.</summary>
        private static readonly Color s_cancelButtonHoverColor = new(0.58f, 0.22f, 0.22f, 1.0f);

        /// <summary>Normal color for the Retry and Back buttons.</summary>
        private static readonly Color s_retryButtonColor = new(0.22f, 0.22f, 0.28f, 1.0f);

        /// <summary>Hover color for the Retry and Back buttons.</summary>
        private static readonly Color s_retryButtonHoverColor = new(0.30f, 0.30f, 0.38f, 1.0f);

        /// <summary>Text color for error messages.</summary>
        private static readonly Color s_errorColor = new(0.90f, 0.30f, 0.30f, 1.0f);

        /// <summary>Button that returns to the previous screen after an error.</summary>
        private Button _backButton;

        /// <summary>Button that cancels the active connection attempt.</summary>
        private Button _cancelButton;

        /// <summary>Detail string from the latest stage provider poll.</summary>
        private string _currentDetail;

        /// <summary>Progress value (0-1) from the latest stage provider poll.</summary>
        private float _currentProgress;

        /// <summary>Current connection stage being displayed.</summary>
        private ConnectionStage _currentStage;

        /// <summary>Label showing additional detail text below the stage name.</summary>
        private Label _detailLabel;

        /// <summary>UI Toolkit document hosting the connection progress panel.</summary>
        private UIDocument _document;

        /// <summary>Container holding the error message label, hidden during normal connection.</summary>
        private VisualElement _errorContainer;

        /// <summary>Label displaying the error message text.</summary>
        private Label _errorLabel;

        /// <summary>Cached error message for display in the error state.</summary>
        private string _errorMessage;

        /// <summary>True when this screen is currently visible and animating.</summary>
        private bool _isVisible;

        /// <summary>Callback invoked when the user clicks Cancel to abort the connection.</summary>
        private Action _onCancel;

        /// <summary>Container element for the progress bar track.</summary>
        private VisualElement _progressBarContainer;

        /// <summary>Fill element inside the progress bar that expands based on connection progress.</summary>
        private VisualElement _progressBarFill;

        /// <summary>Button that retries the connection after an error.</summary>
        private Button _retryButton;

        /// <summary>Screen manager for navigating back on error.</summary>
        private ScreenManager _screenManager;

        /// <summary>Current rotation angle of the spinner element in degrees.</summary>
        private float _spinnerAngle;

        /// <summary>Circular spinner element that rotates during active connection.</summary>
        private VisualElement _spinnerElement;

        /// <summary>Label displaying the current connection stage name.</summary>
        private Label _stageLabel;

        /// <summary>
        ///     Delegate that provides the current connection state. Set by the bootstrap
        ///     or JoinGameScreen before pushing this screen.
        /// </summary>
        public Func<(ConnectionStage stage, float progress, string detail)> StageProvider { get; set; }

        /// <summary>
        ///     Delegate invoked when the user clicks Retry after an error.
        /// </summary>
        public Action OnRetry { get; set; }

        /// <summary>Animates the spinner and polls the stage provider delegate to update the displayed state.</summary>
        private void Update()
        {
            if (!_isVisible)
            {
                return;
            }

            // Animate spinner
            _spinnerAngle += SpinnerSpeed * Time.deltaTime;

            if (_spinnerAngle >= 360f)
            {
                _spinnerAngle -= 360f;
            }

            if (_spinnerElement != null)
            {
                _spinnerElement.style.rotate = new Rotate(_spinnerAngle);
            }

            // Poll stage provider if set
            if (StageProvider != null && _currentStage != ConnectionStage.Error)
            {
                (ConnectionStage stage, float progress, string detail) state = StageProvider();
                _currentStage = state.stage;
                _currentProgress = state.progress;
                _currentDetail = state.detail;
                UpdateDisplay();
            }
        }

        /// <summary>Returns the screen name identifier for the connection progress screen.</summary>
        public string ScreenName
        {
            get
            {
                return ScreenNames.ConnectionProgress;
            }
        }

        /// <summary>Returns true because this screen consumes all input during connection.</summary>
        public bool IsInputOpaque
        {
            get
            {
                return true;
            }
        }

        /// <summary>Returns true because this screen requires a visible cursor for the Cancel button.</summary>
        public bool RequiresCursor
        {
            get
            {
                return true;
            }
        }

        /// <summary>Shows the screen, resets to Connecting stage, and displays the spinner and progress bar.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
            }

            _isVisible = true;
            _currentStage = ConnectionStage.Connecting;
            _errorMessage = null;
            _spinnerAngle = 0f;

            ShowConnectingState();
        }

        /// <summary>Hides the screen and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            _isVisible = false;
            onComplete();
        }

        /// <summary>Triggers Cancel on Escape and returns true to consume the key event.</summary>
        public bool HandleEscape()
        {
            // Escape triggers cancel
            OnCancelClicked();
            return true;
        }

        /// <summary>
        ///     Initializes the connection progress screen.
        /// </summary>
        public void Initialize(PanelSettings panelSettings, ScreenManager screenManager, Action onCancel)
        {
            _screenManager = screenManager;
            _onCancel = onCancel;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 750;

            BuildUI(_document.rootVisualElement);

            // Start hidden
            _document.rootVisualElement.style.display = DisplayStyle.None;
        }

        /// <summary>
        ///     Sets the screen to display an error state with the given message.
        ///     Call from the bootstrap when connection fails.
        /// </summary>
        public void ShowError(string message)
        {
            _errorMessage = message;
            _currentStage = ConnectionStage.Error;
            ShowErrorState(message);
        }

        /// <summary>
        ///     Updates the displayed connection stage directly (for callers that
        ///     don't use the polling <see cref="StageProvider" /> delegate).
        /// </summary>
        public void SetStage(ConnectionStage stage, float progress, string detail)
        {
            _currentStage = stage;
            _currentProgress = progress;
            _currentDetail = detail;
            UpdateDisplay();
        }

        /// <summary>Constructs the panel with spinner, stage/detail labels, progress bar, error container, and buttons.</summary>
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

            // Center panel
            VisualElement panel = new()
            {
                style =
                {
                    width = 400,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 32,
                    paddingBottom = 24,
                    paddingLeft = 32,
                    paddingRight = 32,
                    alignItems = Align.Center,
                },
            };
            background.Add(panel);

            // Spinner
            _spinnerElement = new VisualElement
            {
                style =
                {
                    width = 32,
                    height = 32,
                    borderTopWidth = 3,
                    borderBottomWidth = 3,
                    borderLeftWidth = 3,
                    borderRightWidth = 3,
                    borderTopColor = s_progressFillColor,
                    borderRightColor = s_progressFillColor,
                    borderBottomColor = Color.clear,
                    borderLeftColor = Color.clear,
                    borderTopLeftRadius = 16,
                    borderTopRightRadius = 16,
                    borderBottomLeftRadius = 16,
                    borderBottomRightRadius = 16,
                    marginBottom = 20,
                },
            };
            panel.Add(_spinnerElement);

            // Stage label
            _stageLabel = new Label("Connecting...")
            {
                style =
                {
                    fontSize = 18,
                    color = s_textColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 8,
                },
            };
            panel.Add(_stageLabel);

            // Detail label
            _detailLabel = new Label("")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 16,
                },
            };
            panel.Add(_detailLabel);

            // Progress bar
            _progressBarContainer = new VisualElement
            {
                style =
                {
                    width = new Length(100, LengthUnit.Percent),
                    height = 6,
                    backgroundColor = s_progressBgColor,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    marginBottom = 20,
                    overflow = Overflow.Hidden,
                },
            };
            panel.Add(_progressBarContainer);

            _progressBarFill = new VisualElement
            {
                style =
                {
                    width = new Length(0, LengthUnit.Percent),
                    height = new Length(100, LengthUnit.Percent),
                    backgroundColor = s_progressFillColor,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                },
            };
            _progressBarContainer.Add(_progressBarFill);

            // Error container (hidden by default)
            _errorContainer = new VisualElement
            {
                style =
                {
                    display = DisplayStyle.None, width = new Length(100, LengthUnit.Percent), alignItems = Align.Center, marginBottom = 16,
                },
            };
            panel.Add(_errorContainer);

            _errorLabel = new Label("")
            {
                style =
                {
                    fontSize = 13, color = s_errorColor, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal,
                },
            };
            _errorContainer.Add(_errorLabel);

            // Button container
            VisualElement buttonContainer = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.Center,
                },
            };
            panel.Add(buttonContainer);

            _cancelButton = BuildButton("Cancel", s_cancelButtonColor, s_cancelButtonHoverColor);
            _cancelButton.clicked += OnCancelClicked;
            buttonContainer.Add(_cancelButton);

            _retryButton = BuildButton("Retry", s_retryButtonColor, s_retryButtonHoverColor);
            _retryButton.style.display = DisplayStyle.None;
            _retryButton.style.marginRight = 10;
            _retryButton.clicked += OnRetryClicked;
            buttonContainer.Add(_retryButton);

            _backButton = BuildButton("Back", s_retryButtonColor, s_retryButtonHoverColor);
            _backButton.style.display = DisplayStyle.None;
            _backButton.clicked += OnBackClicked;
            buttonContainer.Add(_backButton);
        }

        /// <summary>Switches the UI to the normal connecting state: spinner visible, error hidden, Cancel shown.</summary>
        private void ShowConnectingState()
        {
            _spinnerElement.style.display = DisplayStyle.Flex;
            _progressBarContainer.style.display = DisplayStyle.Flex;
            _errorContainer.style.display = DisplayStyle.None;
            _cancelButton.style.display = DisplayStyle.Flex;
            _retryButton.style.display = DisplayStyle.None;
            _backButton.style.display = DisplayStyle.None;
        }

        /// <summary>Switches the UI to the error state: spinner hidden, error shown, Retry and Back visible.</summary>
        private void ShowErrorState(string message)
        {
            _spinnerElement.style.display = DisplayStyle.None;
            _progressBarContainer.style.display = DisplayStyle.None;
            _errorContainer.style.display = DisplayStyle.Flex;
            _errorLabel.text = message;
            _stageLabel.text = "Connection Failed";
            _stageLabel.style.color = s_errorColor;
            _detailLabel.text = "";
            _cancelButton.style.display = DisplayStyle.None;
            _retryButton.style.display = DisplayStyle.Flex;
            _backButton.style.display = DisplayStyle.Flex;
        }

        /// <summary>Updates the stage label, detail text, and progress bar width based on current connection state.</summary>
        private void UpdateDisplay()
        {
            if (_currentStage == ConnectionStage.Error)
            {
                ShowErrorState(_errorMessage ?? "An unknown error occurred.");
                return;
            }

            if (_currentStage == ConnectionStage.Connected)
            {
                _stageLabel.text = "Connected!";
                _detailLabel.text = "Entering world...";
                _progressBarFill.style.width = new Length(100, LengthUnit.Percent);
                return;
            }

            ShowConnectingState();
            _stageLabel.style.color = s_textColor;

            _stageLabel.text = _currentStage switch
            {
                ConnectionStage.Disconnected => "Disconnected",
                ConnectionStage.Connecting => "Connecting...",
                ConnectionStage.Authenticating => "Authenticating...",
                ConnectionStage.SyncingContent => "Syncing Content...",
                ConnectionStage.LoadingTerrain => "Loading Terrain...",
                ConnectionStage.EnteringWorld => "Entering World...",
                _ => "Connecting...",
            };

            _detailLabel.text = _currentDetail ?? "";

            // Show progress for stages that report it
            bool hasProgress = _currentStage == ConnectionStage.SyncingContent ||
                               _currentStage == ConnectionStage.LoadingTerrain;

            if (hasProgress && _currentProgress > 0f)
            {
                float percent = Mathf.Clamp01(_currentProgress) * 100f;
                _progressBarFill.style.width = new Length(percent, LengthUnit.Percent);
            }
            else
            {
                // Indeterminate: fill proportionally based on stage ordinal
                // EnteringWorld (5) is the last stage before Connected/Error
                float stagePercent = (int)_currentStage / (float)(int)ConnectionStage.EnteringWorld * 100f;
                _progressBarFill.style.width = new Length(Mathf.Clamp(stagePercent, 0f, 100f), LengthUnit.Percent);
            }
        }

        /// <summary>Invokes the cancel callback to abort the active connection attempt.</summary>
        private void OnCancelClicked()
        {
            _onCancel?.Invoke();
        }

        /// <summary>Resets the screen to Connecting state and invokes the retry delegate.</summary>
        private void OnRetryClicked()
        {
            _currentStage = ConnectionStage.Connecting;
            _errorMessage = null;
            ShowConnectingState();
            _stageLabel.style.color = s_textColor;
            OnRetry?.Invoke();
        }

        /// <summary>Pops the screen manager stack to return to the previous screen.</summary>
        private void OnBackClicked()
        {
            _screenManager.Pop();
        }

        /// <summary>Creates a styled button with hover color transition effects.</summary>
        private Button BuildButton(string text, Color normalColor, Color hoverColor)
        {
            Button btn = new()
            {
                text = text,
                style =
                {
                    width = 120,
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
                    marginLeft = 5,
                    marginRight = 5,
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
