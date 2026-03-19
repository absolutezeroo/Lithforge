using System;
using System.Collections;

using Lithforge.Runtime.Spawn;
using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     Full-screen Minecraft-style loading overlay displayed while spawn chunks load.
    ///     Shows a dirt-brown background, game title, progress bar, and status text.
    ///     Fades out and self-destructs once spawn is complete.
    ///     Decoupled from SpawnManager — accepts a <see cref="Func{SpawnProgress}" />
    ///     delegate that abstracts the progress source. Both SP/Host and remote clients
    ///     use a unified <see cref="ClientReadinessTracker" /> to poll chunk availability.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour, IScreen
    {
        /// <summary>Duration of the fade-out animation in seconds.</summary>
        private const float FadeOutDuration = 0.4f;

        /// <summary>Width of the progress bar in pixels.</summary>
        private const int BarWidth = 400;

        /// <summary>Height of the progress bar in pixels.</summary>
        private const int BarHeight = 20;

        /// <summary>Dark dirt-brown background color for the loading screen.</summary>
        private static readonly Color s_backgroundColor = new(0.10f, 0.06f, 0.04f, 1.0f);

        /// <summary>Background color of the progress bar track.</summary>
        private static readonly Color s_progressTrackColor = new(0.20f, 0.20f, 0.20f, 1.0f);

        /// <summary>Fill color of the progress bar.</summary>
        private static readonly Color s_progressFillColor = new(0.55f, 0.45f, 0.25f, 1.0f);

        /// <summary>Color used for the game title text.</summary>
        private static readonly Color s_logoColor = new(1.0f, 0.95f, 0.80f, 1.0f);

        /// <summary>Color used for status and subtitle text.</summary>
        private static readonly Color s_statusColor = new(0.70f, 0.70f, 0.65f, 1.0f);

        /// <summary>Full-screen background visual element.</summary>
        private VisualElement _background;

        /// <summary>The UI Toolkit document hosting the loading screen.</summary>
        private UIDocument _document;

        /// <summary>True while the fade-out coroutine is running.</summary>
        private bool _fadingOut;

        /// <summary>Callback invoked after the fade-out animation completes.</summary>
        private Action _onFadeComplete;

        /// <summary>The visual element representing the filled portion of the progress bar.</summary>
        private VisualElement _progressFill;

        /// <summary>Delegate polled each frame to get the current spawn progress.</summary>
        private Func<SpawnProgress> _progressSource;

        /// <summary>Label displaying the current loading phase description.</summary>
        private Label _statusLabel;

        /// <summary>Polls the progress source each frame and updates the progress bar and status text.</summary>
        private void Update()
        {
            if (_progressSource == null || _progressFill == null || _fadingOut)
            {
                return;
            }

            SpawnProgress progress = _progressSource();

            float fraction = progress.TotalChunks > 0
                ? (float)progress.ReadyChunks / progress.TotalChunks
                : 0f;

            _progressFill.style.width = new StyleLength(
                new Length(fraction * 100f, LengthUnit.Percent));

            _statusLabel.text = BuildStatusText(progress);

            if (progress.Phase == SpawnState.Done)
            {
                _fadingOut = true;
                StartCoroutine(FadeOut());
            }
        }

        /// <summary>Unique screen name identifier for the loading screen.</summary>
        public string ScreenName
        {
            get
            {
                return ScreenNames.Loading;
            }
        }

        /// <summary>Returns true because the loading screen blocks all input beneath it.</summary>
        public bool IsInputOpaque
        {
            get
            {
                return true;
            }
        }

        /// <summary>Returns false because the loading screen does not need a visible mouse cursor.</summary>
        public bool RequiresCursor
        {
            get
            {
                return false;
            }
        }

        /// <summary>Makes the loading screen visible when the screen is shown.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
                _document.rootVisualElement.style.opacity = 1f;
            }
        }

        /// <summary>Hides the loading screen and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            onComplete();
        }

        /// <summary>Consumes Escape input to prevent other screens from handling it during loading.</summary>
        public bool HandleEscape()
        {
            // Loading screen does not respond to Escape
            return true;
        }

        /// <summary>Creates the UIDocument and builds the loading screen UI layout.</summary>
        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 500;

            BuildUI(_document.rootVisualElement);
        }

        /// <summary>
        ///     Forces the loading screen to fade out immediately.
        ///     Fallback for edge cases where no progress source is wired.
        /// </summary>
        public void ForceComplete()
        {
            if (_fadingOut)
            {
                return;
            }

            _fadingOut = true;
            StartCoroutine(FadeOut());
        }

        /// <summary>
        ///     Sets the progress source delegate and fade-complete callback.
        ///     The delegate is polled each frame to drive the progress bar.
        ///     Both SP/Host and remote clients use the same progress source
        ///     backed by <see cref="ClientReadinessTracker" />.
        /// </summary>
        public void SetProgressSource(Func<SpawnProgress> source, Action onFadeComplete)
        {
            _progressSource = source;
            _onFadeComplete = onFadeComplete;
        }

        /// <summary>
        ///     Updates the status text to show the current content loading phase.
        ///     Used before a progress source is available.
        /// </summary>
        public void SetContentPhase(string phase)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = phase;
            }
        }

        /// <summary>Constructs the loading screen layout: background, logo, subtitle, progress bar, and status label.</summary>
        private void BuildUI(VisualElement root)
        {
            // Full-screen dirt background
            _background = new VisualElement
            {
                name = "loading-background",
                pickingMode = PickingMode.Ignore,
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
            root.Add(_background);

            // Logo label
            Label logo = new("LITHFORGE")
            {
                name = "loading-logo",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 64,
                    color = s_logoColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 8,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 6,
                },
            };
            _background.Add(logo);

            // Subtitle
            Label subtitle = new("Loading World...")
            {
                name = "loading-subtitle",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 18, color = s_statusColor, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 32,
                },
            };
            _background.Add(subtitle);

            // Progress bar track
            VisualElement progressTrack = new()
            {
                name = "progress-track",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = BarWidth,
                    height = BarHeight,
                    backgroundColor = s_progressTrackColor,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    overflow = Overflow.Hidden,
                    marginBottom = 12,
                },
            };
            _background.Add(progressTrack);

            // Progress bar fill
            _progressFill = new VisualElement
            {
                name = "progress-fill",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    bottom = 0,
                    width = new StyleLength(new Length(0f, LengthUnit.Percent)),
                    backgroundColor = s_progressFillColor,
                },
            };
            progressTrack.Add(_progressFill);

            // Status label
            _statusLabel = new Label("Generating terrain...")
            {
                name = "loading-status",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 14, color = s_statusColor, unityTextAlign = TextAnchor.MiddleCenter,
                },
            };
            _background.Add(_statusLabel);
        }

        /// <summary>Converts the current spawn phase into a human-readable status string.</summary>
        private string BuildStatusText(SpawnProgress progress)
        {
            switch (progress.Phase)
            {
                case SpawnState.Checking:
                    return $"Generating terrain... {progress.ReadyChunks}/{progress.TotalChunks} chunks";

                case SpawnState.FindingY:
                    return "Locating spawn position...";

                case SpawnState.Teleporting:
                    return "Placing player...";

                case SpawnState.Done:
                    return "Ready!";

                default:
                    return "";
            }
        }

        /// <summary>Coroutine that fades the loading screen to transparent and then destroys its GameObject.</summary>
        private IEnumerator FadeOut()
        {
            VisualElement root = _document.rootVisualElement;
            float elapsed = 0f;

            while (elapsed < FadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / FadeOutDuration);
                root.style.opacity = 1f - t;
                yield return null;
            }

            if (_onFadeComplete != null)
            {
                _onFadeComplete();
            }

            Destroy(gameObject);
        }
    }
}
