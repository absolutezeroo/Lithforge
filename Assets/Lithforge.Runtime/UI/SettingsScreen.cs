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
        private static readonly int s_aoStrengthId = Shader.PropertyToID("_AOStrength");
        private float _ambientVolume;
        private float _aoStrength;

        // Audio mixer
        private AudioMixerController _audioMixerController;
        private CameraController _cameraController;

        // References to systems we can tweak at runtime
        private ChunkManager _chunkManager;
        private ChunkMeshStore _chunkMeshStore;
        private float _dayLength;
        private UIDocument _document;
        private float _fov;
        private Camera _mainCamera;
        private float _masterVolume;
        private float _mouseSensitivity;
        private float _musicVolume;
        private Action _onCloseCallback;
        private Action<int> _onRenderDistanceChanged;
        private VisualElement _overlay;
        private VisualElement _panel;

        private UserPreferences _preferences;

        // Current values
        private int _renderDistance;
        private float _sfxVolume;
        private TimeOfDayController _timeOfDayController;

        public bool IsOpen { get; private set; }

        /// <summary>
        ///     When true, the Close button will return to the pause menu
        ///     instead of re-locking the cursor directly.
        /// </summary>
        public bool OpenedFromPause { get; set; }

        public string ScreenName { get { return ScreenNames.Settings; } }
        public bool IsInputOpaque { get { return true; } }
        public bool RequiresCursor { get { return true; } }

        public void OnShow(ScreenShowArgs args)
        {
            Open();
        }

        public void OnHide(Action onComplete)
        {
            if (IsOpen)
            {
                Close();
            }

            onComplete();
        }

        public bool HandleEscape()
        {
            // Close settings — let ScreenManager pop this screen
            return false;
        }

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

        private void BuildUI()
        {
            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Semi-transparent overlay background
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            root.Add(_overlay);

            // Panel container — centered
            _panel = new VisualElement();
            _panel.style.position = Position.Absolute;
            _panel.style.top = new Length(10, LengthUnit.Percent);
            _panel.style.bottom = new Length(10, LengthUnit.Percent);
            _panel.style.left = new Length(20, LengthUnit.Percent);
            _panel.style.right = new Length(20, LengthUnit.Percent);
            _panel.style.backgroundColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            _panel.style.borderTopLeftRadius = 8;
            _panel.style.borderTopRightRadius = 8;
            _panel.style.borderBottomLeftRadius = 8;
            _panel.style.borderBottomRightRadius = 8;
            _panel.style.paddingTop = 20;
            _panel.style.paddingBottom = 20;
            _panel.style.paddingLeft = 30;
            _panel.style.paddingRight = 30;
            _panel.style.overflow = Overflow.Hidden;
            _overlay.Add(_panel);

            // Title
            Label title = new("Settings");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 20;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _panel.Add(title);

            // Scrollable content
            ScrollView scrollView = new(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
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
            Button closeButton = new(() => { Close(OpenedFromPause); });
            closeButton.text = "Close";
            closeButton.style.height = 40;
            closeButton.style.marginTop = 15;
            closeButton.style.fontSize = 16;
            closeButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.35f, 1f);
            closeButton.style.color = Color.white;
            closeButton.style.borderTopLeftRadius = 4;
            closeButton.style.borderTopRightRadius = 4;
            closeButton.style.borderBottomLeftRadius = 4;
            closeButton.style.borderBottomRightRadius = 4;
            _panel.Add(closeButton);
        }

        private void AddSectionHeader(ScrollView parent, string text)
        {
            Label header = new(text);
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.8f, 0.8f, 0.85f, 1f);
            header.style.marginTop = 16;
            header.style.marginBottom = 8;
            parent.Add(header);
        }

        private void AddSliderFloat(ScrollView parent, string label, float initialValue,
            float min, float max, Action<float> onChange)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.height = 30;

            Label nameLabel = new(label);
            nameLabel.style.width = new Length(40, LengthUnit.Percent);
            nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            nameLabel.style.fontSize = 14;
            row.Add(nameLabel);

            Slider slider = new(min, max);
            slider.value = initialValue;
            slider.style.flexGrow = 1;
            row.Add(slider);

            Label valueLabel = new(initialValue.ToString("F2"));
            valueLabel.style.width = 60;
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 14;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString("F2");
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        private void AddSliderInt(ScrollView parent, string label, int initialValue,
            int min, int max, Action<int> onChange)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.height = 30;

            Label nameLabel = new(label);
            nameLabel.style.width = new Length(40, LengthUnit.Percent);
            nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            nameLabel.style.fontSize = 14;
            row.Add(nameLabel);

            SliderInt slider = new(min, max);
            slider.value = initialValue;
            slider.style.flexGrow = 1;
            row.Add(slider);

            Label valueLabel = new(initialValue.ToString());
            valueLabel.style.width = 60;
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 14;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString();
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        private void AddKeybindRow(ScrollView parent, string action, string key)
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.height = 26;

            Label actionLabel = new(action);
            actionLabel.style.width = new Length(40, LengthUnit.Percent);
            actionLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            actionLabel.style.fontSize = 14;
            row.Add(actionLabel);

            Label keyLabel = new("[" + key + "]");
            keyLabel.style.color = new Color(0.6f, 0.6f, 0.7f, 1f);
            keyLabel.style.fontSize = 14;
            row.Add(keyLabel);

            parent.Add(row);
        }

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
