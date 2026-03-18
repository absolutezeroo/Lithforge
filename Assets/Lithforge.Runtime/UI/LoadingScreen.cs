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
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour, IScreen
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

        public string ScreenName { get { return ScreenNames.Loading; } }
        public bool IsInputOpaque { get { return true; } }
        public bool RequiresCursor { get { return false; } }

        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
                _document.rootVisualElement.style.opacity = 1f;
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
            // Loading screen does not respond to Escape
            return true;
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
            _background = new VisualElement();
            _background.name = "loading-background";
            _background.pickingMode = PickingMode.Ignore;
            _background.style.position = Position.Absolute;
            _background.style.left = 0;
            _background.style.top = 0;
            _background.style.right = 0;
            _background.style.bottom = 0;
            _background.style.backgroundColor = s_backgroundColor;
            _background.style.alignItems = Align.Center;
            _background.style.justifyContent = Justify.Center;
            _background.style.flexDirection = FlexDirection.Column;
            root.Add(_background);

            // Logo label
            Label logo = new("LITHFORGE");
            logo.name = "loading-logo";
            logo.pickingMode = PickingMode.Ignore;
            logo.style.fontSize = 64;
            logo.style.color = s_logoColor;
            logo.style.unityTextAlign = TextAnchor.MiddleCenter;
            logo.style.marginBottom = 8;
            logo.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.style.letterSpacing = 6;
            _background.Add(logo);

            // Subtitle
            Label subtitle = new("Loading World...");
            subtitle.name = "loading-subtitle";
            subtitle.pickingMode = PickingMode.Ignore;
            subtitle.style.fontSize = 18;
            subtitle.style.color = s_statusColor;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginBottom = 32;
            _background.Add(subtitle);

            // Progress bar track
            VisualElement progressTrack = new();
            progressTrack.name = "progress-track";
            progressTrack.pickingMode = PickingMode.Ignore;
            progressTrack.style.width = BarWidth;
            progressTrack.style.height = BarHeight;
            progressTrack.style.backgroundColor = s_progressTrackColor;
            progressTrack.style.borderTopLeftRadius = 3;
            progressTrack.style.borderTopRightRadius = 3;
            progressTrack.style.borderBottomLeftRadius = 3;
            progressTrack.style.borderBottomRightRadius = 3;
            progressTrack.style.overflow = Overflow.Hidden;
            progressTrack.style.marginBottom = 12;
            _background.Add(progressTrack);

            // Progress bar fill
            _progressFill = new VisualElement();
            _progressFill.name = "progress-fill";
            _progressFill.pickingMode = PickingMode.Ignore;
            _progressFill.style.position = Position.Absolute;
            _progressFill.style.left = 0;
            _progressFill.style.top = 0;
            _progressFill.style.bottom = 0;
            _progressFill.style.width = new StyleLength(new Length(0f, LengthUnit.Percent));
            _progressFill.style.backgroundColor = s_progressFillColor;
            progressTrack.Add(_progressFill);

            // Status label
            _statusLabel = new Label("Generating terrain...");
            _statusLabel.name = "loading-status";
            _statusLabel.pickingMode = PickingMode.Ignore;
            _statusLabel.style.fontSize = 14;
            _statusLabel.style.color = s_statusColor;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
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
