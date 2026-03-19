using System;

using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     Full-screen overlay displayed while the game saves before returning to title.
    ///     Matches the LoadingScreen visual style (dirt-brown background, progress bar, status text).
    ///     Progress is pushed via SetProgress from the QuitToTitleCoroutine — not polled.
    /// </summary>
    public sealed class SavingScreen : MonoBehaviour, IScreen
    {
        /// <summary>Width of the progress bar in pixels.</summary>
        private const int BarWidth = 400;

        /// <summary>Height of the progress bar in pixels.</summary>
        private const int BarHeight = 20;

        /// <summary>Dark dirt-brown background color matching the loading screen style.</summary>
        private static readonly Color s_backgroundColor = new(0.10f, 0.06f, 0.04f, 1.0f);

        /// <summary>Background color of the progress bar track.</summary>
        private static readonly Color s_progressTrackColor = new(0.20f, 0.20f, 0.20f, 1.0f);

        /// <summary>Fill color of the progress bar.</summary>
        private static readonly Color s_progressFillColor = new(0.55f, 0.45f, 0.25f, 1.0f);

        /// <summary>Color used for the game title text.</summary>
        private static readonly Color s_logoColor = new(1.0f, 0.95f, 0.80f, 1.0f);

        /// <summary>Color used for status and subtitle text.</summary>
        private static readonly Color s_statusColor = new(0.70f, 0.70f, 0.65f, 1.0f);

        /// <summary>The UI Toolkit document hosting the saving screen.</summary>
        private UIDocument _document;

        /// <summary>The visual element representing the filled portion of the progress bar.</summary>
        private VisualElement _progressFill;

        /// <summary>Label displaying the current save phase description.</summary>
        private Label _statusLabel;

        /// <summary>Unique screen name identifier for the saving screen.</summary>
        public string ScreenName { get { return ScreenNames.Saving; } }

        /// <summary>Returns true because the saving screen blocks all input beneath it.</summary>
        public bool IsInputOpaque { get { return true; } }

        /// <summary>Returns false because the saving screen does not need a visible mouse cursor.</summary>
        public bool RequiresCursor { get { return false; } }

        /// <summary>Makes the saving screen visible when the screen is shown.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>Hides the saving screen and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            onComplete();
        }

        /// <summary>Consumes Escape input to prevent other screens from handling it during saving.</summary>
        public bool HandleEscape()
        {
            // Saving screen does not respond to Escape
            return true;
        }

        /// <summary>Creates the UIDocument and builds the saving screen UI layout.</summary>
        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 500;

            BuildUI(_document.rootVisualElement);
        }

        /// <summary>
        ///     Updates the displayed progress. Called by the coroutine each yield.
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

        /// <summary>Computes the weighted progress fraction across all save phases (0 to 1).</summary>
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

        /// <summary>Converts the current save phase into a human-readable status string.</summary>
        private static string BuildStatusText(SaveProgress progress)
        {
            return progress.Phase switch
            {
                SaveState.CompletingJobs => "Completing jobs...",
                SaveState.SavingChunks => $"Saving chunks... {progress.SavedChunks}/{progress.TotalChunks}",
                SaveState.FlushingRegions => $"Flushing regions... {progress.FlushedRegions}/{progress.TotalRegions}",
                SaveState.Done => "Save complete!",
                _ => "",
            };
        }

        /// <summary>Constructs the saving screen layout: background, logo, subtitle, progress bar, and status label.</summary>
        private void BuildUI(VisualElement root)
        {
            // Full-screen dirt background
            VisualElement background = new()
            {
                name = "saving-background",
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
            root.Add(background);

            // Logo label
            Label logo = new("LITHFORGE")
            {
                name = "saving-logo",
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
            background.Add(logo);

            // Subtitle
            Label subtitle = new("Saving World...")
            {
                name = "saving-subtitle",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 18, color = s_statusColor, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 32,
                },
            };
            background.Add(subtitle);

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
            background.Add(progressTrack);

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
            _statusLabel = new Label("Preparing to save...")
            {
                name = "saving-status",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = 14, color = s_statusColor, unityTextAlign = TextAnchor.MiddleCenter,
                },
            };
            background.Add(_statusLabel);
        }
    }
}
