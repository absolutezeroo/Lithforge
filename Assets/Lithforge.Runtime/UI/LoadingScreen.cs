using System;
using System.Collections;

using Lithforge.Runtime.Spawn;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     Full-screen Minecraft-style loading overlay displayed while spawn chunks load.
    ///     Shows a dirt-brown background, game title, progress bar, and status text.
    ///     Fades out and self-destructs once spawn is complete.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        private const float FadeOutDuration = 0.4f;
        private const int BarWidth = 400;
        private const int BarHeight = 20;
        private static readonly Color s_backgroundColor = new(0.10f, 0.06f, 0.04f, 1.0f);
        private static readonly Color s_progressTrackColor = new(0.20f, 0.20f, 0.20f, 1.0f);
        private static readonly Color s_progressFillColor = new(0.55f, 0.45f, 0.25f, 1.0f);
        private static readonly Color s_logoColor = new(1.0f, 0.95f, 0.80f, 1.0f);
        private static readonly Color s_statusColor = new(0.70f, 0.70f, 0.65f, 1.0f);

        private VisualElement _background;

        private UIDocument _document;

        private bool _fadingOut;
        private Action _onFadeComplete;
        private VisualElement _progressFill;
        private SpawnManager _spawnManager;
        private Label _statusLabel;

        private void Update()
        {
            if (_spawnManager == null || _progressFill == null || _fadingOut)
            {
                return;
            }

            SpawnProgress progress = _spawnManager.GetProgress();

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

        public void Initialize(SpawnManager spawnManager, PanelSettings panelSettings, Action onFadeComplete)
        {
            _spawnManager = spawnManager;
            _onFadeComplete = onFadeComplete;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 500;

            BuildUI(_document.rootVisualElement);
        }

        /// <summary>
        ///     Sets the SpawnManager after content loading is complete.
        ///     The loading screen transitions from content phase display to spawn progress display.
        /// </summary>
        public void SetSpawnManager(SpawnManager spawnManager, Action onFadeComplete)
        {
            _spawnManager = spawnManager;
            _onFadeComplete = onFadeComplete;
        }

        /// <summary>
        ///     Updates the status text to show the current content loading phase.
        ///     Used before SpawnManager is available.
        /// </summary>
        public void SetContentPhase(string phase)
        {
            if (_statusLabel != null)
            {
                _statusLabel.text = phase;
            }
        }

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
