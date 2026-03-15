using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Full-screen overlay displayed while the game saves before returning to title.
    /// Matches the LoadingScreen visual style (dirt-brown background, progress bar, status text).
    /// Progress is pushed via SetProgress from the QuitToTitleCoroutine — not polled.
    /// </summary>
    public sealed class SavingScreen : MonoBehaviour
    {
        private static readonly Color s_backgroundColor = new Color(0.10f, 0.06f, 0.04f, 1.0f);
        private static readonly Color s_progressTrackColor = new Color(0.20f, 0.20f, 0.20f, 1.0f);
        private static readonly Color s_progressFillColor = new Color(0.55f, 0.45f, 0.25f, 1.0f);
        private static readonly Color s_logoColor = new Color(1.0f, 0.95f, 0.80f, 1.0f);
        private static readonly Color s_statusColor = new Color(0.70f, 0.70f, 0.65f, 1.0f);

        private const int BarWidth = 400;
        private const int BarHeight = 20;

        private UIDocument _document;
        private VisualElement _progressFill;
        private Label _statusLabel;

        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 500;

            BuildUI(_document.rootVisualElement);
        }

        /// <summary>
        /// Updates the displayed progress. Called by the coroutine each yield.
        /// </summary>
        public void SetProgress(SaveProgress progress)
        {
            if (_progressFill == null || _statusLabel == null)
            {
                return;
            }

            float fraction = ComputeFraction(progress);
            _progressFill.style.width = new StyleLength(
                new Length(fraction * 100f, LengthUnit.Percent));

            _statusLabel.text = BuildStatusText(progress);
        }

        private static float ComputeFraction(SaveProgress progress)
        {
            // Weight: CompletingJobs = 10%, SavingChunks = 70%, FlushingRegions = 20%
            switch (progress.Phase)
            {
                case SaveState.CompletingJobs:
                    return 0.05f;

                case SaveState.SavingChunks:
                {
                    float chunkFrac = progress.TotalChunks > 0
                        ? (float)progress.SavedChunks / progress.TotalChunks
                        : 1f;
                    return 0.10f + chunkFrac * 0.70f;
                }

                case SaveState.FlushingRegions:
                {
                    float regionFrac = progress.TotalRegions > 0
                        ? (float)progress.FlushedRegions / progress.TotalRegions
                        : 1f;
                    return 0.80f + regionFrac * 0.20f;
                }

                case SaveState.Done:
                    return 1f;

                default:
                    return 0f;
            }
        }

        private static string BuildStatusText(SaveProgress progress)
        {
            switch (progress.Phase)
            {
                case SaveState.CompletingJobs:
                    return "Completing jobs...";

                case SaveState.SavingChunks:
                    return $"Saving chunks... {progress.SavedChunks}/{progress.TotalChunks}";

                case SaveState.FlushingRegions:
                    return $"Flushing regions... {progress.FlushedRegions}/{progress.TotalRegions}";

                case SaveState.Done:
                    return "Save complete!";

                default:
                    return "";
            }
        }

        private void BuildUI(VisualElement root)
        {
            // Full-screen dirt background
            VisualElement background = new VisualElement();
            background.name = "saving-background";
            background.pickingMode = PickingMode.Ignore;
            background.style.position = Position.Absolute;
            background.style.left = 0;
            background.style.top = 0;
            background.style.right = 0;
            background.style.bottom = 0;
            background.style.backgroundColor = s_backgroundColor;
            background.style.alignItems = Align.Center;
            background.style.justifyContent = Justify.Center;
            background.style.flexDirection = FlexDirection.Column;
            root.Add(background);

            // Logo label
            Label logo = new Label("LITHFORGE");
            logo.name = "saving-logo";
            logo.pickingMode = PickingMode.Ignore;
            logo.style.fontSize = 64;
            logo.style.color = s_logoColor;
            logo.style.unityTextAlign = TextAnchor.MiddleCenter;
            logo.style.marginBottom = 8;
            logo.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.style.letterSpacing = 6;
            background.Add(logo);

            // Subtitle
            Label subtitle = new Label("Saving World...");
            subtitle.name = "saving-subtitle";
            subtitle.pickingMode = PickingMode.Ignore;
            subtitle.style.fontSize = 18;
            subtitle.style.color = s_statusColor;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginBottom = 32;
            background.Add(subtitle);

            // Progress bar track
            VisualElement progressTrack = new VisualElement();
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
            background.Add(progressTrack);

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
            _statusLabel = new Label("Preparing to save...");
            _statusLabel.name = "saving-status";
            _statusLabel.pickingMode = PickingMode.Ignore;
            _statusLabel.style.fontSize = 14;
            _statusLabel.style.color = s_statusColor;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            background.Add(_statusLabel);
        }
    }
}
