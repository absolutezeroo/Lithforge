using System;
using System.Collections.Generic;

using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.UI.Settings;
using Lithforge.Voxel.Chunk;

using Newtonsoft.Json;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     In-game settings screen built with UI Toolkit.
    ///     Organized into three tabs: Video, Audio, Controls.
    ///     All settings apply live as sliders/toggles/dropdowns are changed.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreen
    {
        /// <summary>Cached shader property ID for the AO strength parameter.</summary>
        private static readonly int s_aoStrengthId = Shader.PropertyToID("_AOStrength");

        /// <summary>Shadow distance values indexed by shadow quality level.</summary>
        private static readonly float[] s_shadowDistances = { 0f, 20f, 40f, 80f };

        /// <summary>MSAA sample count values indexed by dropdown position.</summary>
        private static readonly int[] s_msaaCounts = { 1, 2, 4, 8 };

        /// <summary>Max FPS choices for the dropdown.</summary>
        private static readonly string[] s_fpsChoices = { "30", "60", "120", "144", "240", "Unlimited" };

        /// <summary>Max FPS values corresponding to dropdown indices.</summary>
        private static readonly int[] s_fpsValues = { 30, 60, 120, 144, 240, 0 };

        /// <summary>Current ambient volume level (0 to 1).</summary>
        private float _ambientVolume;

        /// <summary>Current ambient occlusion strength (0 to 1).</summary>
        private float _aoStrength;

        /// <summary>Audio mixer controller for routing volume changes through the mixer groups.</summary>
        private AudioMixerController _audioMixerController;

        /// <summary>Key binding configuration for rebindable actions.</summary>
        private KeyBindingConfig _bindings;

        /// <summary>Camera controller for applying mouse sensitivity changes.</summary>
        private CameraController _cameraController;

        /// <summary>Chunk manager for applying render distance changes.</summary>
        private ChunkManager _chunkManager;

        /// <summary>Chunk mesh store for applying AO strength to materials.</summary>
        private ChunkMeshStore _chunkMeshStore;

        /// <summary>Current day length in seconds.</summary>
        private float _dayLength;

        /// <summary>The UI Toolkit document hosting the settings screen.</summary>
        private UIDocument _document;

        /// <summary>Current field of view in degrees.</summary>
        private float _fov;

        /// <summary>Whether a keybind rebind is currently in progress.</summary>
        private bool _isRebinding;

        /// <summary>Dictionary mapping keybind action names to their UI buttons for refresh.</summary>
        private Dictionary<string, Button> _keybindButtons;

        /// <summary>Reference to the main camera for field of view changes.</summary>
        private Camera _mainCamera;

        /// <summary>Current master volume level (0 to 1).</summary>
        private float _masterVolume;

        /// <summary>Current mouse sensitivity value.</summary>
        private float _mouseSensitivity;

        /// <summary>Current music volume level (0 to 1).</summary>
        private float _musicVolume;

        /// <summary>Callback invoked when the settings panel closes back to the pause menu.</summary>
        private Action _onCloseCallback;

        /// <summary>Callback invoked when the render distance slider changes.</summary>
        private Action<int> _onRenderDistanceChanged;

        /// <summary>Semi-transparent overlay covering the game world behind the panel.</summary>
        private VisualElement _overlay;

        /// <summary>The centered settings panel containing all tabs and controls.</summary>
        private VisualElement _panel;

        /// <summary>Persistent user preferences for saving and loading settings across sessions.</summary>
        private UserPreferences _preferences;

        /// <summary>Callback to invoke with the newly pressed key during rebind.</summary>
        private Action<Key> _rebindCallback;

        /// <summary>Overlay shown during keybind rebinding ("Press any key...").</summary>
        private VisualElement _rebindOverlay;

        /// <summary>Current render distance in chunks.</summary>
        private int _renderDistance;

        /// <summary>Current SFX volume level (0 to 1).</summary>
        private float _sfxVolume;

        /// <summary>Tab button elements for active-state styling.</summary>
        private Button[] _tabButtons;

        /// <summary>Tab content elements for show/hide switching.</summary>
        private VisualElement[] _tabContents;

        /// <summary>Time-of-day controller for applying day length changes.</summary>
        private TimeOfDayController _timeOfDayController;

        /// <summary>Cloned URP pipeline asset for runtime modification.</summary>
        private UniversalRenderPipelineAsset _urpClone;

        /// <summary>True while the settings overlay is visible.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        ///     When true, the Close button will return to the pause menu
        ///     instead of re-locking the cursor directly.
        /// </summary>
        public bool OpenedFromPause { get; set; }

        /// <summary>Unique screen name identifier for the settings screen.</summary>
        public string ScreenName
        {
            get { return ScreenNames.Settings; }
        }

        /// <summary>Returns true because the settings screen blocks all input beneath it.</summary>
        public bool IsInputOpaque
        {
            get { return true; }
        }

        /// <summary>Returns true because the settings screen requires a visible mouse cursor.</summary>
        public bool RequiresCursor
        {
            get { return true; }
        }

        /// <summary>Opens the settings overlay when the screen is shown.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            Open();
        }

        /// <summary>Closes the settings overlay if open and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (IsOpen)
            {
                Close();
            }

            onComplete();
        }

        /// <summary>Returns false so the ScreenManager will pop this screen on Escape.</summary>
        public bool HandleEscape()
        {
            return false;
        }

        /// <summary>Polls for key press during rebind and applies the result.</summary>
        private void Update()
        {
            if (!_isRebinding)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard is null)
            {
                // Keyboard disconnected during rebind — cancel gracefully
                CancelRebind();

                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelRebind();

                return;
            }

            // Check each key for a press
            foreach (KeyControl keyControl in keyboard.allKeys)
            {
                if (keyControl.wasPressedThisFrame)
                {
                    Key pressedKey = keyControl.keyCode;

                    // Ignore modifier-only keys that are ambiguous
                    if (pressedKey is Key.None or Key.IMESelected)
                    {
                        continue;
                    }

                    _rebindCallback?.Invoke(pressedKey);
                    EndRebind();

                    return;
                }
            }
        }

        /// <summary>Initializes the settings screen with system references, builds the UI, and loads persisted settings.</summary>
        public void Initialize(
            ChunkManager chunkManager,
            CameraController cameraController,
            TimeOfDayController timeOfDayController,
            ChunkMeshStore chunkMeshStore,
            PanelSettings panelSettings,
            UserPreferences preferences,
            Action<int> onRenderDistanceChanged = null,
            KeyBindingConfig keyBindings = null)
        {
            _chunkManager = chunkManager;
            _cameraController = cameraController;
            _timeOfDayController = timeOfDayController;
            _chunkMeshStore = chunkMeshStore;
            _mainCamera = Camera.main;
            _onRenderDistanceChanged = onRenderDistanceChanged;
            _preferences = preferences;
            _bindings = keyBindings ?? new KeyBindingConfig();

            // Read current values as defaults
            _renderDistance = chunkManager.RenderDistance;
            _fov = _mainCamera is not null ? _mainCamera.fieldOfView : 60f;
            _mouseSensitivity = cameraController is not null ? cameraController.LookSensitivity : 0.1f;
            _aoStrength = 0.4f;
            _dayLength = timeOfDayController is not null ? timeOfDayController.DayLengthSeconds : 600f;
            _masterVolume = 1.0f;
            _sfxVolume = 1.0f;
            _musicVolume = 1.0f;
            _ambientVolume = 1.0f;

            // Clone URP asset for runtime modification
            CloneUrpAsset();

            // Override with persisted values and apply to systems
            LoadPersistedSettings();

            _document = gameObject.AddComponent<UIDocument>();
            _document.sortingOrder = 300;

            if (panelSettings is not null)
            {
                _document.panelSettings = panelSettings;
            }

            // Load settings USS
            StyleSheet settingsUss = Resources.Load<StyleSheet>("UI/Themes/SettingsScreen");

            BuildUI();

            if (settingsUss is not null)
            {
                _document.rootVisualElement.styleSheets.Add(settingsUss);
            }

            // Start with overlay hidden
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>Destroys the cloned URP asset on teardown to prevent memory leaks.</summary>
        private void OnDestroy()
        {
            if (_urpClone is not null)
            {
                Destroy(_urpClone);
                _urpClone = null;
            }
        }

        /// <summary>Clones the active URP pipeline asset to avoid modifying the editor asset.</summary>
        private void CloneUrpAsset()
        {
            RenderPipelineAsset currentAsset = GraphicsSettings.currentRenderPipeline;

            if (currentAsset is UniversalRenderPipelineAsset urpAsset)
            {
                _urpClone = Instantiate(urpAsset);
                QualitySettings.renderPipeline = _urpClone;
            }
        }

        /// <summary>Constructs the tabbed settings screen layout with Video, Audio, and Controls tabs.</summary>
        private void BuildUI()
        {
            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Semi-transparent overlay background
            _overlay = new VisualElement();
            _overlay.AddToClassList("settings-overlay");
            root.Add(_overlay);

            // Panel container — centered
            _panel = new VisualElement();
            _panel.AddToClassList("settings-panel");
            _overlay.Add(_panel);

            // Title
            Label title = new("Settings");
            title.AddToClassList("settings-title");
            _panel.Add(title);

            // Tab bar
            VisualElement tabBar = new();
            tabBar.AddToClassList("settings-tab-bar");
            _panel.Add(tabBar);

            Button videoTab = new() { text = "Video" };
            videoTab.AddToClassList("settings-tab");

            Button audioTab = new() { text = "Audio" };
            audioTab.AddToClassList("settings-tab");

            Button controlsTab = new() { text = "Controls" };
            controlsTab.AddToClassList("settings-tab");

            tabBar.Add(videoTab);
            tabBar.Add(audioTab);
            tabBar.Add(controlsTab);

            _tabButtons = new[] { videoTab, audioTab, controlsTab };

            // Tab content container
            VisualElement tabContent = new()
            {
                style =
                {
                    flexGrow = 1,
                    overflow = Overflow.Hidden,
                },
            };
            _panel.Add(tabContent);

            // Video content
            ScrollView videoContent = new(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            BuildVideoTab(videoContent);
            tabContent.Add(videoContent);

            // Audio content
            ScrollView audioContent = new(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            BuildAudioTab(audioContent);
            tabContent.Add(audioContent);

            // Controls content
            ScrollView controlsContent = new(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            BuildControlsTab(controlsContent);
            tabContent.Add(controlsContent);

            _tabContents = new VisualElement[] { videoContent, audioContent, controlsContent };

            // Wire tab switching
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int tabIndex = i;
                _tabButtons[i].clicked += () => { SwitchTab(tabIndex); };
            }

            // Start on Video tab
            SwitchTab(0);

            // Close button
            Button closeButton = new(() => { Close(OpenedFromPause); })
            {
                text = "Close",
            };
            closeButton.AddToClassList("settings-close-button");
            _panel.Add(closeButton);

            // Rebind overlay (hidden by default)
            _rebindOverlay = new VisualElement();
            _rebindOverlay.AddToClassList("settings-rebind-overlay");
            _rebindOverlay.style.display = DisplayStyle.None;

            Label rebindLabel = new("Press any key...");
            rebindLabel.AddToClassList("settings-rebind-label");
            _rebindOverlay.Add(rebindLabel);

            Label rebindHint = new("Press Escape to cancel");
            rebindHint.AddToClassList("settings-rebind-hint");
            _rebindOverlay.Add(rebindHint);

            _panel.Add(_rebindOverlay);
        }

        /// <summary>Switches to the tab at the given index, showing its content and highlighting its button.</summary>
        private void SwitchTab(int activeIndex)
        {
            for (int i = 0; i < _tabContents.Length; i++)
            {
                _tabContents[i].style.display = i == activeIndex ? DisplayStyle.Flex : DisplayStyle.None;

                if (i == activeIndex)
                {
                    _tabButtons[i].AddToClassList("settings-tab--active");
                }
                else
                {
                    _tabButtons[i].RemoveFromClassList("settings-tab--active");
                }
            }
        }

        /// <summary>Populates the Video tab with display, quality, and camera settings.</summary>
        private void BuildVideoTab(ScrollView parent)
        {
            // --- Display Section ---
            SettingsTabBuilder.AddSectionHeader(parent, "Display");

            // Resolution dropdown
            List<Resolution> uniqueResolutions = GetUniqueResolutions();
            string[] resolutionChoices = new string[uniqueResolutions.Count];
            int currentResIndex = 0;

            for (int i = 0; i < uniqueResolutions.Count; i++)
            {
                Resolution res = uniqueResolutions[i];
                resolutionChoices[i] = res.width + " x " + res.height;

                if (res.width == Screen.currentResolution.width &&
                    res.height == Screen.currentResolution.height)
                {
                    currentResIndex = i;
                }
            }

            if (_preferences.HasResolution)
            {
                for (int i = 0; i < uniqueResolutions.Count; i++)
                {
                    if (uniqueResolutions[i].width == _preferences.ResolutionWidth &&
                        uniqueResolutions[i].height == _preferences.ResolutionHeight)
                    {
                        currentResIndex = i;
                        break;
                    }
                }
            }

            SettingsTabBuilder.AddDropdown(parent, "Resolution", resolutionChoices, currentResIndex, index =>
            {
                Resolution chosen = uniqueResolutions[index];
                FullScreenMode mode = GetCurrentFullScreenMode();
                Screen.SetResolution(chosen.width, chosen.height, mode);
                _preferences.ResolutionWidth = chosen.width;
                _preferences.ResolutionHeight = chosen.height;
            });

            // Fullscreen mode dropdown
            string[] fullscreenChoices = { "Borderless Window", "Exclusive Fullscreen", "Windowed" };
            int currentFsIndex = GetFullScreenModeIndex(Screen.fullScreenMode);

            if (_preferences.HasFullScreenMode)
            {
                currentFsIndex = GetFullScreenModeIndex((FullScreenMode)_preferences.FullScreenMode);
            }

            SettingsTabBuilder.AddDropdown(parent, "Fullscreen", fullscreenChoices, currentFsIndex, index =>
            {
                FullScreenMode mode = GetFullScreenModeFromIndex(index);
                Screen.fullScreenMode = mode;
                _preferences.FullScreenMode = (int)mode;
            });

            // VSync toggle
            bool vsyncOn = QualitySettings.vSyncCount > 0;

            if (_preferences.HasVSyncCount)
            {
                vsyncOn = _preferences.VSyncCount > 0;
            }

            SettingsTabBuilder.AddToggle(parent, "VSync", vsyncOn, value =>
            {
                QualitySettings.vSyncCount = value ? 1 : 0;
                _preferences.VSyncCount = value ? 1 : 0;
            });

            // Max FPS dropdown
            int currentFpsIndex = 5; // Unlimited default

            if (_preferences.HasMaxFrameRate)
            {
                currentFpsIndex = FindFpsIndex(_preferences.MaxFrameRate);
            }

            SettingsTabBuilder.AddDropdown(parent, "Max FPS", s_fpsChoices, currentFpsIndex, index =>
            {
                int fps = s_fpsValues[index];
                Application.targetFrameRate = fps == 0 ? -1 : fps;
                _preferences.MaxFrameRate = fps;
            });

            // GUI Scale dropdown (stub)
            string[] guiScaleChoices = { "Auto", "1", "2", "3", "4" };
            int currentGuiScale = 0;

            if (_preferences.HasGuiScale)
            {
                currentGuiScale = _preferences.GuiScale;
            }

            SettingsTabBuilder.AddDropdown(parent, "GUI Scale", guiScaleChoices, currentGuiScale, index =>
            {
                _preferences.GuiScale = index;
                // TODO: Apply GUI scale when panel scaling is implemented
            });

            // --- Quality Section ---
            SettingsTabBuilder.AddSectionHeader(parent, "Quality");

            // Render Distance
            SettingsTabBuilder.AddSliderInt(parent, "Render Distance", _renderDistance, 1, 32, value =>
            {
                _renderDistance = value;

                if (_chunkManager is not null)
                {
                    _chunkManager.SetRenderDistance(value);
                }

                _onRenderDistanceChanged?.Invoke(value);
                _preferences.RenderDistance = value;
            });

            // Field of View
            SettingsTabBuilder.AddSliderFloat(parent, "Field of View", _fov, 60f, 120f, value =>
            {
                _fov = value;

                if (_mainCamera is not null)
                {
                    _mainCamera.fieldOfView = value;
                }

                _preferences.FieldOfView = value;
            });

            // Shadow Quality
            string[] shadowChoices = { "Off", "Low", "Medium", "High" };
            int currentShadow = _urpClone is not null ? GetShadowQualityIndex(_urpClone.shadowDistance) : 3;

            if (_preferences.HasShadowQuality)
            {
                currentShadow = _preferences.ShadowQuality;
            }

            SettingsTabBuilder.AddDropdown(parent, "Shadow Quality", shadowChoices, currentShadow, index =>
            {
                if (_urpClone is not null)
                {
                    _urpClone.shadowDistance = s_shadowDistances[index];
                }

                _preferences.ShadowQuality = index;
            });

            // MSAA
            string[] msaaChoices = { "Off", "2x", "4x", "8x" };
            int currentMsaa = 0;

            if (_urpClone is not null)
            {
                currentMsaa = GetMsaaIndex(_urpClone.msaaSampleCount);
            }

            if (_preferences.HasMsaaLevel)
            {
                currentMsaa = GetMsaaIndex(_preferences.MsaaLevel);
            }

            SettingsTabBuilder.AddDropdown(parent, "MSAA", msaaChoices, currentMsaa, index =>
            {
                if (_urpClone is not null)
                {
                    _urpClone.msaaSampleCount = s_msaaCounts[index];
                }

                _preferences.MsaaLevel = s_msaaCounts[index];
            });

            // Render Scale
            float currentRenderScale = _urpClone is not null ? _urpClone.renderScale : 1f;

            if (_preferences.HasRenderScale)
            {
                currentRenderScale = _preferences.RenderScale;
            }

            SettingsTabBuilder.AddSliderFloat(parent, "Render Scale", currentRenderScale, 0.5f, 1.0f, value =>
            {
                if (_urpClone is not null)
                {
                    _urpClone.renderScale = value;
                }

                _preferences.RenderScale = value;
            });

            // AO Strength
            SettingsTabBuilder.AddSliderFloat(parent, "AO Strength", _aoStrength, 0f, 1f, value =>
            {
                _aoStrength = value;
                ApplyAOStrength(value);
                _preferences.AOStrength = value;
            });

            // Mipmap Levels
            int currentMipmap = 0;

            if (_preferences.HasMipmapLevel)
            {
                currentMipmap = _preferences.MipmapLevel;
            }

            SettingsTabBuilder.AddSliderInt(parent, "Mipmap Levels", currentMipmap, 0, 4, value =>
            {
                QualitySettings.globalTextureMipmapLimit = value;
                _preferences.MipmapLevel = value;
            });

            // Clouds (stub)
            string[] cloudChoices = { "Off", "Fast", "Fancy" };
            int currentCloud = 0;

            if (_preferences.HasCloudQuality)
            {
                currentCloud = _preferences.CloudQuality;
            }

            SettingsTabBuilder.AddDropdown(parent, "Clouds", cloudChoices, currentCloud, index =>
            {
                _preferences.CloudQuality = index;
                // TODO: Apply cloud quality when cloud system is implemented
            });

            // Particles (stub)
            string[] particleChoices = { "All", "Decreased", "Minimal" };
            int currentParticle = 0;

            if (_preferences.HasParticleQuality)
            {
                currentParticle = _preferences.ParticleQuality;
            }

            SettingsTabBuilder.AddDropdown(parent, "Particles", particleChoices, currentParticle, index =>
            {
                _preferences.ParticleQuality = index;
                // TODO: Apply particle quality when particle system is implemented
            });

            // --- Camera Section ---
            SettingsTabBuilder.AddSectionHeader(parent, "Camera");

            // Mouse Sensitivity
            SettingsTabBuilder.AddSliderFloat(parent, "Mouse Sensitivity", _mouseSensitivity, 0.01f, 1.0f,
                value =>
                {
                    _mouseSensitivity = value;

                    if (_cameraController is not null)
                    {
                        _cameraController.SetLookSensitivity(value);
                    }

                    _preferences.MouseSensitivity = value;
                });

            // Day Length
            SettingsTabBuilder.AddSliderFloat(parent, "Day Length (seconds)", _dayLength, 60f, 3600f, value =>
            {
                _dayLength = value;

                if (_timeOfDayController is not null)
                {
                    _timeOfDayController.DayLengthSeconds = value;
                }
            });
        }

        /// <summary>Populates the Audio tab with volume sliders.</summary>
        private void BuildAudioTab(ScrollView parent)
        {
            SettingsTabBuilder.AddSectionHeader(parent, "Volume");

            SettingsTabBuilder.AddSliderFloat(parent, "Master Volume", _masterVolume, 0f, 1f, value =>
            {
                _masterVolume = value;

                if (_audioMixerController is not null)
                {
                    _audioMixerController.SetMasterVolume(value);
                }
                else
                {
                    AudioListener.volume = value;
                }

                _preferences.MasterVolume = value;
            });

            SettingsTabBuilder.AddSliderFloat(parent, "SFX Volume", _sfxVolume, 0f, 1f, value =>
            {
                _sfxVolume = value;
                _audioMixerController?.SetSfxVolume(value);
                _preferences.SfxVolume = value;
            });

            SettingsTabBuilder.AddSliderFloat(parent, "Music Volume", _musicVolume, 0f, 1f, value =>
            {
                _musicVolume = value;
                _audioMixerController?.SetMusicVolume(value);
                _preferences.MusicVolume = value;
            });

            SettingsTabBuilder.AddSliderFloat(parent, "Ambient Volume", _ambientVolume, 0f, 1f, value =>
            {
                _ambientVolume = value;
                _audioMixerController?.SetAmbientVolume(value);
                _preferences.AmbientVolume = value;
            });
        }

        /// <summary>Populates the Controls tab with rebindable keybind rows and a reset button.</summary>
        private void BuildControlsTab(ScrollView parent)
        {
            SettingsTabBuilder.AddSectionHeader(parent, "Key Bindings");

            _keybindButtons = new Dictionary<string, Button>();

            AddKeybind(parent, "Move Forward", _bindings.MoveForward,
                key => { _bindings.MoveForward = key; });

            AddKeybind(parent, "Move Back", _bindings.MoveBack,
                key => { _bindings.MoveBack = key; });

            AddKeybind(parent, "Move Left", _bindings.MoveLeft,
                key => { _bindings.MoveLeft = key; });

            AddKeybind(parent, "Move Right", _bindings.MoveRight,
                key => { _bindings.MoveRight = key; });

            AddKeybind(parent, "Jump", _bindings.Jump,
                key => { _bindings.Jump = key; });

            AddKeybind(parent, "Sprint", _bindings.Sprint,
                key => { _bindings.Sprint = key; });

            AddKeybind(parent, "Inventory", _bindings.Inventory,
                key => { _bindings.Inventory = key; });

            AddKeybind(parent, "Fly Mode", _bindings.FlyToggle,
                key => { _bindings.FlyToggle = key; });

            AddKeybind(parent, "Noclip", _bindings.NoclipToggle,
                key => { _bindings.NoclipToggle = key; });

            // Non-rebindable info rows
            SettingsTabBuilder.AddSectionHeader(parent, "Fixed Controls");

            AddFixedKeybindRow(parent, "Break Block", "Left Click");
            AddFixedKeybindRow(parent, "Place Block", "Right Click");
            AddFixedKeybindRow(parent, "Hotbar 1-9", "Digit Keys");

            // Reset Defaults button
            Button resetButton = new(() => { ResetKeybindDefaults(); })
            {
                text = "Reset Defaults",
            };
            resetButton.AddToClassList("settings-reset-button");
            parent.Add(resetButton);
        }

        /// <summary>Adds a rebindable keybind row and stores the button reference for refresh.</summary>
        private void AddKeybind(VisualElement parent, string actionName, Key currentKey,
            Action<Key> onKeyChanged)
        {
            Button button = SettingsTabBuilder.AddKeybindRow(parent, actionName, currentKey,
                (_, callback) =>
                {
                    StartRebind(newKey =>
                    {
                        onKeyChanged(newKey);
                        callback(newKey);
                        PersistKeyBindings();
                    });
                });

            _keybindButtons[actionName] = button;
        }

        /// <summary>Adds a read-only keybind display row for non-rebindable controls.</summary>
        private void AddFixedKeybindRow(VisualElement parent, string action, string key)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4,
                    height = 26,
                },
            };

            Label actionLabel = new(action)
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent),
                    color = new Color(0.85f, 0.85f, 0.85f, 1f),
                    fontSize = 14,
                },
            };
            row.Add(actionLabel);

            Label keyLabel = new("[" + key + "]")
            {
                style =
                {
                    color = new Color(0.6f, 0.6f, 0.7f, 1f),
                    fontSize = 14,
                },
            };
            row.Add(keyLabel);

            parent.Add(row);
        }

        /// <summary>Begins a keybind rebind operation, showing the overlay.</summary>
        private void StartRebind(Action<Key> callback)
        {
            if (_isRebinding)
            {
                EndRebind();
            }

            _isRebinding = true;
            _rebindCallback = callback;
            _rebindOverlay.style.display = DisplayStyle.Flex;
        }

        /// <summary>Completes a rebind by hiding the overlay and clearing state.</summary>
        private void EndRebind()
        {
            _isRebinding = false;
            _rebindCallback = null;
            _rebindOverlay.style.display = DisplayStyle.None;
        }

        /// <summary>Cancels a rebind without applying changes.</summary>
        private void CancelRebind()
        {
            EndRebind();
        }

        /// <summary>Serializes the current keybindings to UserPreferences JSON.</summary>
        private void PersistKeyBindings()
        {
            Dictionary<string, string> dict = _bindings.ToDictionary();
            _preferences.KeyBindingsJson = JsonConvert.SerializeObject(dict);
        }

        /// <summary>Resets all keybindings to defaults and refreshes the UI buttons.</summary>
        private void ResetKeybindDefaults()
        {
            KeyBindingConfig defaults = new();

            _bindings.MoveForward = defaults.MoveForward;
            _bindings.MoveBack = defaults.MoveBack;
            _bindings.MoveLeft = defaults.MoveLeft;
            _bindings.MoveRight = defaults.MoveRight;
            _bindings.Jump = defaults.Jump;
            _bindings.Sprint = defaults.Sprint;
            _bindings.Inventory = defaults.Inventory;
            _bindings.FlyToggle = defaults.FlyToggle;
            _bindings.NoclipToggle = defaults.NoclipToggle;

            // Refresh button labels
            RefreshKeybindButton("Move Forward", _bindings.MoveForward);
            RefreshKeybindButton("Move Back", _bindings.MoveBack);
            RefreshKeybindButton("Move Left", _bindings.MoveLeft);
            RefreshKeybindButton("Move Right", _bindings.MoveRight);
            RefreshKeybindButton("Jump", _bindings.Jump);
            RefreshKeybindButton("Sprint", _bindings.Sprint);
            RefreshKeybindButton("Inventory", _bindings.Inventory);
            RefreshKeybindButton("Fly Mode", _bindings.FlyToggle);
            RefreshKeybindButton("Noclip", _bindings.NoclipToggle);

            PersistKeyBindings();
        }

        /// <summary>Updates the text of a keybind button by action name.</summary>
        private void RefreshKeybindButton(string actionName, Key key)
        {
            if (_keybindButtons.TryGetValue(actionName, out Button button))
            {
                button.text = "[" + SettingsTabBuilder.FormatKeyName(key) + "]";
            }
        }

        /// <summary>Sets the AO strength on all three voxel materials.</summary>
        private void ApplyAOStrength(float value)
        {
            if (_chunkMeshStore is null)
            {
                return;
            }

            _chunkMeshStore.OpaqueMaterial?.SetFloat(s_aoStrengthId, value);
            _chunkMeshStore.CutoutMaterial?.SetFloat(s_aoStrengthId, value);
            _chunkMeshStore.TranslucentMaterial?.SetFloat(s_aoStrengthId, value);
        }

        /// <summary>Shows the settings overlay and unlocks the cursor.</summary>
        public void Open()
        {
            IsOpen = true;
            OpenedFromPause = false;
            _overlay.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Sets a callback invoked when the settings panel closes back to pause menu.</summary>
        public void SetOnCloseCallback(Action callback)
        {
            _onCloseCallback = callback;
        }

        /// <summary>
        ///     Closes the settings panel. When returnToPause is true, the cursor is
        ///     left unlocked so the pause menu can manage cursor state.
        ///     Fires the onCloseCallback if set and returning to pause.
        /// </summary>
        public void Close(bool returnToPause = false)
        {
            // Cancel any in-progress rebind
            if (_isRebinding)
            {
                CancelRebind();
            }

            IsOpen = false;
            OpenedFromPause = false;
            _overlay.style.display = DisplayStyle.None;

            if (!returnToPause)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _preferences.Save();

            if (returnToPause && _onCloseCallback is not null)
            {
                _onCloseCallback();
            }
        }

        /// <summary>Sets the audio mixer controller for volume persistence through the mixer.</summary>
        public void SetAudioMixerController(AudioMixerController controller)
        {
            _audioMixerController = controller;
        }

        /// <summary>Controls the root document visibility (used by HudVisibilityController).</summary>
        public void SetVisible(bool visible)
        {
            if (_document is not null && _document.rootVisualElement is not null)
            {
                _document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        ///     Loads persisted settings from UserPreferences and applies them to the live systems.
        ///     Called once during Initialize after reading default values.
        /// </summary>
        private void LoadPersistedSettings()
        {
            if (_preferences.HasRenderDistance)
            {
                _renderDistance = _preferences.RenderDistance;

                if (_chunkManager is not null)
                {
                    _chunkManager.SetRenderDistance(_renderDistance);
                }

                _onRenderDistanceChanged?.Invoke(_renderDistance);
            }

            if (_preferences.HasFieldOfView)
            {
                _fov = _preferences.FieldOfView;

                if (_mainCamera is not null)
                {
                    _mainCamera.fieldOfView = _fov;
                }
            }

            if (_preferences.HasMouseSensitivity)
            {
                _mouseSensitivity = _preferences.MouseSensitivity;

                if (_cameraController is not null)
                {
                    _cameraController.SetLookSensitivity(_mouseSensitivity);
                }
            }

            if (_preferences.HasAOStrength)
            {
                _aoStrength = _preferences.AOStrength;
                ApplyAOStrength(_aoStrength);
            }

            if (_preferences.HasMasterVolume)
            {
                _masterVolume = _preferences.MasterVolume;
            }

            if (_preferences.HasSfxVolume)
            {
                _sfxVolume = _preferences.SfxVolume;
            }

            if (_preferences.HasMusicVolume)
            {
                _musicVolume = _preferences.MusicVolume;
            }

            if (_preferences.HasAmbientVolume)
            {
                _ambientVolume = _preferences.AmbientVolume;
            }

            // Video settings
            if (_preferences.HasResolution && _preferences.HasFullScreenMode)
            {
                FullScreenMode mode = (FullScreenMode)_preferences.FullScreenMode;
                Screen.SetResolution(_preferences.ResolutionWidth, _preferences.ResolutionHeight, mode);
            }
            else if (_preferences.HasResolution)
            {
                Screen.SetResolution(_preferences.ResolutionWidth, _preferences.ResolutionHeight,
                    Screen.fullScreenMode);
            }

            if (_preferences.HasFullScreenMode)
            {
                Screen.fullScreenMode = (FullScreenMode)_preferences.FullScreenMode;
            }

            if (_preferences.HasVSyncCount)
            {
                QualitySettings.vSyncCount = _preferences.VSyncCount;
            }

            if (_preferences.HasMaxFrameRate)
            {
                int fps = _preferences.MaxFrameRate;
                Application.targetFrameRate = fps == 0 ? -1 : fps;
            }

            if (_urpClone is not null)
            {
                if (_preferences.HasShadowQuality)
                {
                    int sq = Mathf.Clamp(_preferences.ShadowQuality, 0, 3);
                    _urpClone.shadowDistance = s_shadowDistances[sq];
                }

                if (_preferences.HasMsaaLevel)
                {
                    _urpClone.msaaSampleCount = _preferences.MsaaLevel;
                }

                if (_preferences.HasRenderScale)
                {
                    _urpClone.renderScale = _preferences.RenderScale;
                }
            }

            if (_preferences.HasMipmapLevel)
            {
                QualitySettings.globalTextureMipmapLimit = _preferences.MipmapLevel;
            }
        }

        /// <summary>
        ///     Applies persisted volume values to the mixer. Must be called after
        ///     SetAudioMixerController so the mixer reference is available.
        /// </summary>
        public void ApplyPersistedVolumes()
        {
            if (_audioMixerController is not null)
            {
                _audioMixerController.SetMasterVolume(_masterVolume);
                _audioMixerController.SetSfxVolume(_sfxVolume);
                _audioMixerController.SetMusicVolume(_musicVolume);
                _audioMixerController.SetAmbientVolume(_ambientVolume);
            }
            else
            {
                AudioListener.volume = _masterVolume;
            }
        }

        /// <summary>Gets deduplicated screen resolutions sorted by width then height.</summary>
        private static List<Resolution> GetUniqueResolutions()
        {
            Resolution[] allRes = Screen.resolutions;
            List<Resolution> unique = new();
            HashSet<long> seen = new();

            for (int i = 0; i < allRes.Length; i++)
            {
                long key = (long)allRes[i].width << 32 | (uint)allRes[i].height;

                if (seen.Add(key))
                {
                    unique.Add(allRes[i]);
                }
            }

            return unique;
        }

        /// <summary>Converts a FullScreenMode to dropdown index.</summary>
        private static int GetFullScreenModeIndex(FullScreenMode mode)
        {
            return mode switch
            {
                FullScreenMode.FullScreenWindow => 0,
                FullScreenMode.ExclusiveFullScreen => 1,
                FullScreenMode.Windowed => 2,
                _ => 0,
            };
        }

        /// <summary>Converts a dropdown index to FullScreenMode.</summary>
        private static FullScreenMode GetFullScreenModeFromIndex(int index)
        {
            return index switch
            {
                0 => FullScreenMode.FullScreenWindow,
                1 => FullScreenMode.ExclusiveFullScreen,
                2 => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow,
            };
        }

        /// <summary>Gets the current fullscreen mode for resolution changes.</summary>
        private static FullScreenMode GetCurrentFullScreenMode()
        {
            return Screen.fullScreenMode;
        }

        /// <summary>Finds the FPS dropdown index for a given frame rate value.</summary>
        private static int FindFpsIndex(int fps)
        {
            for (int i = 0; i < s_fpsValues.Length; i++)
            {
                if (s_fpsValues[i] == fps)
                {
                    return i;
                }
            }

            return 5; // Unlimited
        }

        /// <summary>Gets the shadow quality index from a shadow distance value.</summary>
        private static int GetShadowQualityIndex(float distance)
        {
            if (distance <= 0f)
            {
                return 0;
            }

            if (distance <= 20f)
            {
                return 1;
            }

            if (distance <= 40f)
            {
                return 2;
            }

            return 3;
        }

        /// <summary>Gets the MSAA dropdown index from a sample count.</summary>
        private static int GetMsaaIndex(int sampleCount)
        {
            return sampleCount switch
            {
                2 => 1,
                4 => 2,
                8 => 3,
                _ => 0,
            };
        }
    }
}
