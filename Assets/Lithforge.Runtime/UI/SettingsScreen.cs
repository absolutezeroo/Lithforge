using System;

using Lithforge.Runtime.Audio;
using Lithforge.Runtime.Content.Settings;
using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Voxel.Chunk;

using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     In-game settings screen built with UI Toolkit.
    ///     Opened via pause menu or main menu. All settings apply live as sliders are dragged.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour, IScreen
    {
        /// <summary>Cached shader property ID for the AO strength parameter.</summary>
        private static readonly int s_aoStrengthId = Shader.PropertyToID("_AOStrength");

        /// <summary>Current ambient volume level (0 to 1).</summary>
        private float _ambientVolume;

        /// <summary>Current ambient occlusion strength (0 to 1).</summary>
        private float _aoStrength;

        /// <summary>Audio mixer controller for routing volume changes through the mixer groups.</summary>
        private AudioMixerController _audioMixerController;

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

        /// <summary>The centered settings panel containing all sliders and controls.</summary>
        private VisualElement _panel;

        /// <summary>Persistent user preferences for saving and loading settings across sessions.</summary>
        private UserPreferences _preferences;

        /// <summary>Current render distance in chunks.</summary>
        private int _renderDistance;

        /// <summary>Current SFX volume level (0 to 1).</summary>
        private float _sfxVolume;

        /// <summary>Time-of-day controller for applying day length changes.</summary>
        private TimeOfDayController _timeOfDayController;

        /// <summary>True while the settings overlay is visible.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>
        ///     When true, the Close button will return to the pause menu
        ///     instead of re-locking the cursor directly.
        /// </summary>
        public bool OpenedFromPause { get; set; }

        /// <summary>Unique screen name identifier for the settings screen.</summary>
        public string ScreenName { get { return ScreenNames.Settings; } }

        /// <summary>Returns true because the settings screen blocks all input beneath it.</summary>
        public bool IsInputOpaque { get { return true; } }

        /// <summary>Returns true because the settings screen requires a visible mouse cursor.</summary>
        public bool RequiresCursor { get { return true; } }

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
            // Close settings — let ScreenManager pop this screen
            return false;
        }

        /// <summary>Initializes the settings screen with system references, builds the UI, and loads persisted settings.</summary>
        public void Initialize(
            ChunkManager chunkManager,
            CameraController cameraController,
            TimeOfDayController timeOfDayController,
            ChunkMeshStore chunkMeshStore,
            PanelSettings panelSettings,
            UserPreferences preferences,
            Action<int> onRenderDistanceChanged = null)
        {
            _chunkManager = chunkManager;
            _cameraController = cameraController;
            _timeOfDayController = timeOfDayController;
            _chunkMeshStore = chunkMeshStore;
            _mainCamera = Camera.main;
            _onRenderDistanceChanged = onRenderDistanceChanged;
            _preferences = preferences;

            // Read current values as defaults
            _renderDistance = chunkManager.RenderDistance;
            _fov = _mainCamera != null ? _mainCamera.fieldOfView : 60f;
            _mouseSensitivity = cameraController != null ? cameraController.LookSensitivity : 0.1f;
            _aoStrength = 0.4f;
            _dayLength = timeOfDayController != null ? timeOfDayController.DayLengthSeconds : 600f;
            _masterVolume = 1.0f;
            _sfxVolume = 1.0f;
            _musicVolume = 1.0f;
            _ambientVolume = 1.0f;

            // Override with persisted PlayerPrefs values and apply to systems
            LoadPersistedSettings();

            _document = gameObject.AddComponent<UIDocument>();
            _document.sortingOrder = 300;

            if (panelSettings != null)
            {
                _document.panelSettings = panelSettings;
            }

            BuildUI();

            // Start with overlay hidden (hides panel too since it's a child)
            _overlay.style.display = DisplayStyle.None;
        }

        /// <summary>Constructs the settings screen layout with graphics, gameplay, audio, and keybinds sections.</summary>
        private void BuildUI()
        {
            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Semi-transparent overlay background
            _overlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    top = 0,
                    bottom = 0,
                    left = 0,
                    right = 0,
                    backgroundColor = new Color(0f, 0f, 0f, 0.6f),
                },
            };
            root.Add(_overlay);

            // Panel container — centered
            _panel = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    top = new Length(10, LengthUnit.Percent),
                    bottom = new Length(10, LengthUnit.Percent),
                    left = new Length(20, LengthUnit.Percent),
                    right = new Length(20, LengthUnit.Percent),
                    backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.95f),
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 20,
                    paddingBottom = 20,
                    paddingLeft = 30,
                    paddingRight = 30,
                    overflow = Overflow.Hidden,
                },
            };
            _overlay.Add(_panel);

            // Title
            Label title = new("Settings")
            {
                style =
                {
                    fontSize = 28,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    marginBottom = 20,
                    unityTextAlign = TextAnchor.MiddleCenter,
                },
            };
            _panel.Add(title);

            // Scrollable content
            ScrollView scrollView = new(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                },
            };
            _panel.Add(scrollView);

            // --- Graphics Section ---
            AddSectionHeader(scrollView, "Graphics");
            AddSliderInt(scrollView, "Render Distance", _renderDistance, 1, 32, value =>
            {
                _renderDistance = value;

                if (_chunkManager != null)
                {
                    _chunkManager.SetRenderDistance(value);
                }

                _onRenderDistanceChanged?.Invoke(value);
                _preferences.RenderDistance = value;
            });

            AddSliderFloat(scrollView, "Field of View", _fov, 60f, 120f, value =>
            {
                _fov = value;

                if (_mainCamera != null)
                {
                    _mainCamera.fieldOfView = value;
                }

                _preferences.FieldOfView = value;
            });

            AddSliderFloat(scrollView, "AO Strength", _aoStrength, 0f, 1f, value =>
            {
                _aoStrength = value;

                if (_chunkMeshStore != null)
                {
                    if (_chunkMeshStore.OpaqueMaterial != null)
                    {
                        _chunkMeshStore.OpaqueMaterial.SetFloat(s_aoStrengthId, value);
                    }

                    if (_chunkMeshStore.CutoutMaterial != null)
                    {
                        _chunkMeshStore.CutoutMaterial.SetFloat(s_aoStrengthId, value);
                    }

                    if (_chunkMeshStore.TranslucentMaterial != null)
                    {
                        _chunkMeshStore.TranslucentMaterial.SetFloat(s_aoStrengthId, value);
                    }
                }

                _preferences.AOStrength = value;
            });

            // --- Gameplay Section ---
            AddSectionHeader(scrollView, "Gameplay");
            AddSliderFloat(scrollView, "Mouse Sensitivity", _mouseSensitivity, 0.01f, 1.0f, value =>
            {
                _mouseSensitivity = value;

                if (_cameraController != null)
                {
                    _cameraController.SetLookSensitivity(value);
                }

                _preferences.MouseSensitivity = value;
            });

            AddSliderFloat(scrollView, "Day Length (seconds)", _dayLength, 60f, 3600f, value =>
            {
                _dayLength = value;

                if (_timeOfDayController != null)
                {
                    _timeOfDayController.DayLengthSeconds = value;
                }
            });

            // --- Audio Section ---
            AddSectionHeader(scrollView, "Audio");
            AddSliderFloat(scrollView, "Master Volume", _masterVolume, 0f, 1f, value =>
            {
                _masterVolume = value;

                if (_audioMixerController != null)
                {
                    _audioMixerController.SetMasterVolume(value);
                }
                else
                {
                    AudioListener.volume = value;
                }

                _preferences.MasterVolume = value;
            });

            AddSliderFloat(scrollView, "SFX Volume", _sfxVolume, 0f, 1f, value =>
            {
                _sfxVolume = value;

                if (_audioMixerController != null)
                {
                    _audioMixerController.SetSfxVolume(value);
                }

                _preferences.SfxVolume = value;
            });

            AddSliderFloat(scrollView, "Music Volume", _musicVolume, 0f, 1f, value =>
            {
                _musicVolume = value;

                if (_audioMixerController != null)
                {
                    _audioMixerController.SetMusicVolume(value);
                }

                _preferences.MusicVolume = value;
            });

            AddSliderFloat(scrollView, "Ambient Volume", _ambientVolume, 0f, 1f, value =>
            {
                _ambientVolume = value;

                if (_audioMixerController != null)
                {
                    _audioMixerController.SetAmbientVolume(value);
                }

                _preferences.AmbientVolume = value;
            });

            // --- Keybinds Section ---
            AddSectionHeader(scrollView, "Keybinds");
            AddKeybindRow(scrollView, "Forward", "W");
            AddKeybindRow(scrollView, "Back", "S");
            AddKeybindRow(scrollView, "Left", "A");
            AddKeybindRow(scrollView, "Right", "D");
            AddKeybindRow(scrollView, "Jump", "Space");
            AddKeybindRow(scrollView, "Sprint", "Left Shift");
            AddKeybindRow(scrollView, "Inventory", "E");
            AddKeybindRow(scrollView, "Break Block", "Left Click");
            AddKeybindRow(scrollView, "Place Block", "Right Click");
            AddKeybindRow(scrollView, "Fly Mode", "F");
            AddKeybindRow(scrollView, "Noclip (while flying)", "N");

            // Close button — calls Close with returnToPause if opened from pause menu
            Button closeButton = new(() => { Close(OpenedFromPause); })
            {
                text = "Close",
                style =
                {
                    height = 40,
                    marginTop = 15,
                    fontSize = 16,
                    backgroundColor = new Color(0.3f, 0.3f, 0.35f, 1f),
                    color = Color.white,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                },
            };
            _panel.Add(closeButton);
        }

        /// <summary>Adds a bold section header label to the scroll view.</summary>
        private void AddSectionHeader(ScrollView parent, string text)
        {
            Label header = new(text)
            {
                style =
                {
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.8f, 0.8f, 0.85f, 1f),
                    marginTop = 16,
                    marginBottom = 8,
                },
            };
            parent.Add(header);
        }

        /// <summary>Adds a float slider row with label and live value display.</summary>
        private void AddSliderFloat(ScrollView parent, string label, float initialValue,
            float min, float max, Action<float> onChange)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6, height = 30,
                },
            };

            Label nameLabel = new(label)
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent), color = new Color(0.85f, 0.85f, 0.85f, 1f), fontSize = 14,
                },
            };
            row.Add(nameLabel);

            Slider slider = new(min, max)
            {
                value = initialValue,
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(slider);

            Label valueLabel = new(initialValue.ToString("F2"))
            {
                style =
                {
                    width = 60, color = Color.white, fontSize = 14, unityTextAlign = TextAnchor.MiddleRight,
                },
            };
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString("F2");
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        /// <summary>Adds an integer slider row with label and live value display.</summary>
        private void AddSliderInt(ScrollView parent, string label, int initialValue,
            int min, int max, Action<int> onChange)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6, height = 30,
                },
            };

            Label nameLabel = new(label)
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent), color = new Color(0.85f, 0.85f, 0.85f, 1f), fontSize = 14,
                },
            };
            row.Add(nameLabel);

            SliderInt slider = new(min, max)
            {
                value = initialValue,
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(slider);

            Label valueLabel = new(initialValue.ToString())
            {
                style =
                {
                    width = 60, color = Color.white, fontSize = 14, unityTextAlign = TextAnchor.MiddleRight,
                },
            };
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString();
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        /// <summary>Adds a read-only keybind display row showing the action name and its bound key.</summary>
        private void AddKeybindRow(ScrollView parent, string action, string key)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4, height = 26,
                },
            };

            Label actionLabel = new(action)
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent), color = new Color(0.85f, 0.85f, 0.85f, 1f), fontSize = 14,
                },
            };
            row.Add(actionLabel);

            Label keyLabel = new("[" + key + "]")
            {
                style =
                {
                    color = new Color(0.6f, 0.6f, 0.7f, 1f), fontSize = 14,
                },
            };
            row.Add(keyLabel);

            parent.Add(row);
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

        /// <summary>
        ///     Sets a callback invoked when the settings panel closes back to pause menu.
        /// </summary>
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
            IsOpen = false;
            OpenedFromPause = false;
            _overlay.style.display = DisplayStyle.None;

            if (!returnToPause)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _preferences.Save();

            if (returnToPause && _onCloseCallback != null)
            {
                _onCloseCallback();
            }
        }

        /// <summary>
        ///     Sets the audio mixer controller for volume persistence through the mixer.
        ///     Call after Initialize.
        /// </summary>
        public void SetAudioMixerController(AudioMixerController controller)
        {
            _audioMixerController = controller;
        }

        /// <summary>
        ///     Controls the root document visibility (used by HudVisibilityController).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        ///     Loads persisted settings from PlayerPrefs and applies them to the live systems.
        ///     Called once during Initialize after reading default values.
        /// </summary>
        private void LoadPersistedSettings()
        {
            if (_preferences.HasRenderDistance)
            {
                _renderDistance = _preferences.RenderDistance;

                if (_chunkManager != null)
                {
                    _chunkManager.SetRenderDistance(_renderDistance);
                }

                _onRenderDistanceChanged?.Invoke(_renderDistance);
            }

            if (_preferences.HasFieldOfView)
            {
                _fov = _preferences.FieldOfView;

                if (_mainCamera != null)
                {
                    _mainCamera.fieldOfView = _fov;
                }
            }

            if (_preferences.HasMouseSensitivity)
            {
                _mouseSensitivity = _preferences.MouseSensitivity;

                if (_cameraController != null)
                {
                    _cameraController.SetLookSensitivity(_mouseSensitivity);
                }
            }

            if (_preferences.HasAOStrength)
            {
                _aoStrength = _preferences.AOStrength;

                if (_chunkMeshStore != null)
                {
                    if (_chunkMeshStore.OpaqueMaterial != null)
                    {
                        _chunkMeshStore.OpaqueMaterial.SetFloat(s_aoStrengthId, _aoStrength);
                    }

                    if (_chunkMeshStore.CutoutMaterial != null)
                    {
                        _chunkMeshStore.CutoutMaterial.SetFloat(s_aoStrengthId, _aoStrength);
                    }

                    if (_chunkMeshStore.TranslucentMaterial != null)
                    {
                        _chunkMeshStore.TranslucentMaterial.SetFloat(s_aoStrengthId, _aoStrength);
                    }
                }
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
        }

        /// <summary>
        ///     Applies persisted volume values to the mixer. Must be called after
        ///     SetAudioMixerController so the mixer reference is available.
        /// </summary>
        public void ApplyPersistedVolumes()
        {
            if (_audioMixerController != null)
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
    }
}
