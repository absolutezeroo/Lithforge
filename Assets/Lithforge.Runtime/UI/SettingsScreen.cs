using Lithforge.Runtime.Input;
using Lithforge.Runtime.Rendering;
using Lithforge.Voxel.Chunk;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// In-game settings screen built with UI Toolkit.
    /// Opened with Escape key (when cursor is locked) or via pause menu.
    /// All settings apply live as sliders are dragged.
    /// </summary>
    public sealed class SettingsScreen : MonoBehaviour
    {
        private UIDocument _document;
        private VisualElement _panel;
        private bool _isOpen;

        // References to systems we can tweak at runtime
        private ChunkManager _chunkManager;
        private CameraController _cameraController;
        private TimeOfDayController _timeOfDayController;
        private ChunkRenderManager _chunkRenderManager;
        private Camera _mainCamera;

        // Current values
        private int _renderDistance;
        private float _fov;
        private float _mouseSensitivity;
        private float _aoStrength;
        private float _dayLength;
        private float _audioVolume;

        private static readonly int _aoStrengthId = Shader.PropertyToID("_AOStrength");

        // PlayerPrefs keys for persistent settings
        private const string _prefRenderDistance = "LF_RenderDistance";
        private const string _prefFOV = "LF_FOV";
        private const string _prefMouseSensitivity = "LF_MouseSensitivity";
        private const string _prefAOStrength = "LF_AOStrength";

        public void Initialize(
            ChunkManager chunkManager,
            CameraController cameraController,
            TimeOfDayController timeOfDayController,
            ChunkRenderManager chunkRenderManager,
            PanelSettings panelSettings)
        {
            _chunkManager = chunkManager;
            _cameraController = cameraController;
            _timeOfDayController = timeOfDayController;
            _chunkRenderManager = chunkRenderManager;
            _mainCamera = Camera.main;

            // Read current values as defaults
            _renderDistance = chunkManager.RenderDistance;
            _fov = _mainCamera != null ? _mainCamera.fieldOfView : 60f;
            _mouseSensitivity = cameraController != null ? cameraController.LookSensitivity : 0.1f;
            _aoStrength = 0.4f;
            _dayLength = timeOfDayController != null ? timeOfDayController.DayLengthSeconds : 600f;
            _audioVolume = 1.0f;

            // Override with persisted PlayerPrefs values and apply to systems
            LoadPersistedSettings();

            _document = gameObject.AddComponent<UIDocument>();
            _document.sortingOrder = 300;

            if (panelSettings != null)
            {
                _document.panelSettings = panelSettings;
            }

            BuildUI();

            // Start with panel hidden
            _panel.style.display = DisplayStyle.None;
        }

        private void BuildUI()
        {
            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            // Semi-transparent overlay background
            VisualElement overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.top = 0;
            overlay.style.bottom = 0;
            overlay.style.left = 0;
            overlay.style.right = 0;
            overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            root.Add(overlay);

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
            overlay.Add(_panel);

            // Title
            Label title = new Label("Settings");
            title.style.fontSize = 28;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Color.white;
            title.style.marginBottom = 20;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            _panel.Add(title);

            // Scrollable content
            ScrollView scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            _panel.Add(scrollView);

            // --- Graphics Section ---
            AddSectionHeader(scrollView, "Graphics");
            AddSliderInt(scrollView, "Render Distance", _renderDistance, 1, 32, (int value) =>
            {
                _renderDistance = value;

                if (_chunkManager != null)
                {
                    _chunkManager.SetRenderDistance(value);
                }

                PlayerPrefs.SetInt(_prefRenderDistance, value);
            });

            AddSliderFloat(scrollView, "Field of View", _fov, 60f, 120f, (float value) =>
            {
                _fov = value;

                if (_mainCamera != null)
                {
                    _mainCamera.fieldOfView = value;
                }

                PlayerPrefs.SetFloat(_prefFOV, value);
            });

            AddSliderFloat(scrollView, "AO Strength", _aoStrength, 0f, 1f, (float value) =>
            {
                _aoStrength = value;

                if (_chunkRenderManager != null)
                {
                    if (_chunkRenderManager.OpaqueMaterial != null)
                    {
                        _chunkRenderManager.OpaqueMaterial.SetFloat(_aoStrengthId, value);
                    }

                    if (_chunkRenderManager.CutoutMaterial != null)
                    {
                        _chunkRenderManager.CutoutMaterial.SetFloat(_aoStrengthId, value);
                    }

                    if (_chunkRenderManager.TranslucentMaterial != null)
                    {
                        _chunkRenderManager.TranslucentMaterial.SetFloat(_aoStrengthId, value);
                    }
                }

                PlayerPrefs.SetFloat(_prefAOStrength, value);
            });

            // --- Gameplay Section ---
            AddSectionHeader(scrollView, "Gameplay");
            AddSliderFloat(scrollView, "Mouse Sensitivity", _mouseSensitivity, 0.01f, 1.0f, (float value) =>
            {
                _mouseSensitivity = value;

                if (_cameraController != null)
                {
                    _cameraController.SetLookSensitivity(value);
                }

                PlayerPrefs.SetFloat(_prefMouseSensitivity, value);
            });

            AddSliderFloat(scrollView, "Day Length (seconds)", _dayLength, 60f, 3600f, (float value) =>
            {
                _dayLength = value;

                if (_timeOfDayController != null)
                {
                    _timeOfDayController.DayLengthSeconds = value;
                }
            });

            // --- Audio Section ---
            AddSectionHeader(scrollView, "Audio");
            AddSliderFloat(scrollView, "Master Volume", _audioVolume, 0f, 1f, (float value) =>
            {
                _audioVolume = value;
                AudioListener.volume = value;
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

            // Close button
            Button closeButton = new Button(() => { Close(); });
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
            Label header = new Label(text);
            header.style.fontSize = 20;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new Color(0.8f, 0.8f, 0.85f, 1f);
            header.style.marginTop = 16;
            header.style.marginBottom = 8;
            parent.Add(header);
        }

        private void AddSliderFloat(ScrollView parent, string label, float initialValue,
            float min, float max, System.Action<float> onChange)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.height = 30;

            Label nameLabel = new Label(label);
            nameLabel.style.width = new Length(40, LengthUnit.Percent);
            nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            nameLabel.style.fontSize = 14;
            row.Add(nameLabel);

            Slider slider = new Slider(min, max);
            slider.value = initialValue;
            slider.style.flexGrow = 1;
            row.Add(slider);

            Label valueLabel = new Label(initialValue.ToString("F2"));
            valueLabel.style.width = 60;
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 14;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
            {
                valueLabel.text = evt.newValue.ToString("F2");
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        private void AddSliderInt(ScrollView parent, string label, int initialValue,
            int min, int max, System.Action<int> onChange)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.height = 30;

            Label nameLabel = new Label(label);
            nameLabel.style.width = new Length(40, LengthUnit.Percent);
            nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            nameLabel.style.fontSize = 14;
            row.Add(nameLabel);

            SliderInt slider = new SliderInt(min, max);
            slider.value = initialValue;
            slider.style.flexGrow = 1;
            row.Add(slider);

            Label valueLabel = new Label(initialValue.ToString());
            valueLabel.style.width = 60;
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 14;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback((ChangeEvent<int> evt) =>
            {
                valueLabel.text = evt.newValue.ToString();
                onChange(evt.newValue);
            });

            parent.Add(row);
        }

        private void AddKeybindRow(ScrollView parent, string action, string key)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.style.height = 26;

            Label actionLabel = new Label(action);
            actionLabel.style.width = new Length(40, LengthUnit.Percent);
            actionLabel.style.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            actionLabel.style.fontSize = 14;
            row.Add(actionLabel);

            Label keyLabel = new Label("[" + key + "]");
            keyLabel.style.color = new Color(0.6f, 0.6f, 0.7f, 1f);
            keyLabel.style.fontSize = 14;
            row.Add(keyLabel);

            parent.Add(row);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    Close();
                }
                else if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Open();
                }
            }
        }

        public void Open()
        {
            _isOpen = true;
            _panel.style.display = DisplayStyle.Flex;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Close()
        {
            _isOpen = false;
            _panel.style.display = DisplayStyle.None;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            PlayerPrefs.Save();
        }

        public bool IsOpen
        {
            get { return _isOpen; }
        }

        /// <summary>
        /// Controls the root document visibility (used by HudVisibilityController).
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        /// <summary>
        /// Loads persisted settings from PlayerPrefs and applies them to the live systems.
        /// Called once during Initialize after reading default values.
        /// </summary>
        private void LoadPersistedSettings()
        {
            if (PlayerPrefs.HasKey(_prefRenderDistance))
            {
                _renderDistance = PlayerPrefs.GetInt(_prefRenderDistance);

                if (_chunkManager != null)
                {
                    _chunkManager.SetRenderDistance(_renderDistance);
                }
            }

            if (PlayerPrefs.HasKey(_prefFOV))
            {
                _fov = PlayerPrefs.GetFloat(_prefFOV);

                if (_mainCamera != null)
                {
                    _mainCamera.fieldOfView = _fov;
                }
            }

            if (PlayerPrefs.HasKey(_prefMouseSensitivity))
            {
                _mouseSensitivity = PlayerPrefs.GetFloat(_prefMouseSensitivity);

                if (_cameraController != null)
                {
                    _cameraController.SetLookSensitivity(_mouseSensitivity);
                }
            }

            if (PlayerPrefs.HasKey(_prefAOStrength))
            {
                _aoStrength = PlayerPrefs.GetFloat(_prefAOStrength);

                if (_chunkRenderManager != null)
                {
                    if (_chunkRenderManager.OpaqueMaterial != null)
                    {
                        _chunkRenderManager.OpaqueMaterial.SetFloat(_aoStrengthId, _aoStrength);
                    }

                    if (_chunkRenderManager.CutoutMaterial != null)
                    {
                        _chunkRenderManager.CutoutMaterial.SetFloat(_aoStrengthId, _aoStrength);
                    }

                    if (_chunkRenderManager.TranslucentMaterial != null)
                    {
                        _chunkRenderManager.TranslucentMaterial.SetFloat(_aoStrengthId, _aoStrength);
                    }
                }
            }
        }
    }
}
