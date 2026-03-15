using System;
using System.Collections.Generic;
using System.Threading;
using Lithforge.Voxel.Storage;
using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.World
{
    public sealed class WorldSelectionScreen : MonoBehaviour
    {
        private static readonly Color s_backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1.0f);
        private static readonly Color s_panelColor = new Color(0.12f, 0.12f, 0.15f, 0.95f);
        private static readonly Color s_entryColor = new Color(0.16f, 0.16f, 0.20f, 1.0f);
        private static readonly Color s_entryHoverColor = new Color(0.22f, 0.22f, 0.28f, 1.0f);
        private static readonly Color s_entrySelectedColor = new Color(0.25f, 0.30f, 0.45f, 1.0f);
        private static readonly Color s_buttonColor = new Color(0.20f, 0.45f, 0.25f, 1.0f);
        private static readonly Color s_buttonHoverColor = new Color(0.25f, 0.55f, 0.30f, 1.0f);
        private static readonly Color s_deleteButtonColor = new Color(0.55f, 0.20f, 0.20f, 1.0f);
        private static readonly Color s_deleteButtonHoverColor = new Color(0.70f, 0.25f, 0.25f, 1.0f);
        private static readonly Color s_disabledButtonColor = new Color(0.25f, 0.25f, 0.25f, 1.0f);
        private static readonly Color s_textColor = new Color(0.90f, 0.90f, 0.88f, 1.0f);
        private static readonly Color s_dimTextColor = new Color(0.55f, 0.55f, 0.50f, 1.0f);
        private static readonly Color s_logoColor = new Color(1.0f, 0.95f, 0.80f, 1.0f);
        private static readonly Color s_lockedColor = new Color(0.80f, 0.40f, 0.40f, 1.0f);
        private static readonly Color s_survivalDotColor = new Color(0.40f, 0.75f, 0.40f, 1.0f);
        private static readonly Color s_creativeDotColor = new Color(0.40f, 0.55f, 0.85f, 1.0f);
        private static readonly Color s_modalOverlayColor = new Color(0f, 0f, 0f, 0.7f);

        private UIDocument _document;
        private VisualElement _background;
        private ScrollView _worldListScroll;
        private Button _playButton;
        private Button _deleteButton;
        private Label _detailName;
        private Label _detailInfo;
        private VisualElement _modalOverlay;

        private string _worldsRoot;
        private List<WorldScanEntry> _scanResults;
        private volatile bool _scanComplete;
        private volatile bool _scanRunning;
        private bool _uiPopulated;
        private int _selectedIndex = -1;
        private List<VisualElement> _entryElements = new List<VisualElement>();

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Initialize(PanelSettings panelSettings)
        {
            _worldsRoot = System.IO.Path.Combine(Application.persistentDataPath, "worlds");

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 600;

            BuildUI(_document.rootVisualElement);

            // Start async scan
            StartScan();
        }

        private void StartScan()
        {
            if (_scanRunning)
            {
                return;
            }

            _scanRunning = true;
            Thread scanThread = new Thread(ScanWorker);
            scanThread.IsBackground = true;
            scanThread.Start();
        }

        private void ScanWorker()
        {
            _scanResults = WorldDirectoryScanner.ScanWorlds(_worldsRoot);
            _scanComplete = true;
            _scanRunning = false;
        }

        private void Update()
        {
            if (_scanComplete && !_uiPopulated)
            {
                _uiPopulated = true;
                PopulateWorldList();
            }
        }

        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            _background = new VisualElement();
            _background.style.position = Position.Absolute;
            _background.style.left = 0;
            _background.style.top = 0;
            _background.style.right = 0;
            _background.style.bottom = 0;
            _background.style.backgroundColor = s_backgroundColor;
            _background.style.flexDirection = FlexDirection.Column;
            _background.style.alignItems = Align.Center;
            _background.style.paddingTop = 30;
            _background.style.paddingBottom = 30;
            root.Add(_background);

            // Logo
            Label logo = new Label("LITHFORGE");
            logo.style.fontSize = 48;
            logo.style.color = s_logoColor;
            logo.style.unityTextAlign = TextAnchor.MiddleCenter;
            logo.style.unityFontStyleAndWeight = FontStyle.Bold;
            logo.style.letterSpacing = 4;
            logo.style.marginBottom = 4;
            _background.Add(logo);

            Label subtitle = new Label("Select World");
            subtitle.style.fontSize = 18;
            subtitle.style.color = s_dimTextColor;
            subtitle.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitle.style.marginBottom = 20;
            _background.Add(subtitle);

            // Content area: list on left, detail on right
            VisualElement contentArea = new VisualElement();
            contentArea.style.flexDirection = FlexDirection.Row;
            contentArea.style.flexGrow = 1;
            contentArea.style.width = new Length(80, LengthUnit.Percent);
            contentArea.style.maxWidth = 900;
            _background.Add(contentArea);

            // Left panel — world list
            VisualElement listPanel = new VisualElement();
            listPanel.style.flexGrow = 1;
            listPanel.style.flexBasis = new Length(60, LengthUnit.Percent);
            listPanel.style.marginRight = 12;
            contentArea.Add(listPanel);

            _worldListScroll = new ScrollView(ScrollViewMode.Vertical);
            _worldListScroll.style.flexGrow = 1;
            _worldListScroll.style.backgroundColor = s_panelColor;
            _worldListScroll.style.borderTopLeftRadius = 6;
            _worldListScroll.style.borderTopRightRadius = 6;
            _worldListScroll.style.borderBottomLeftRadius = 6;
            _worldListScroll.style.borderBottomRightRadius = 6;
            _worldListScroll.style.paddingTop = 8;
            _worldListScroll.style.paddingBottom = 8;
            _worldListScroll.style.paddingLeft = 8;
            _worldListScroll.style.paddingRight = 8;
            listPanel.Add(_worldListScroll);

            // Loading placeholder
            Label loadingLabel = new Label("Scanning worlds...");
            loadingLabel.name = "loading-label";
            loadingLabel.style.fontSize = 14;
            loadingLabel.style.color = s_dimTextColor;
            loadingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            loadingLabel.style.marginTop = 20;
            _worldListScroll.Add(loadingLabel);

            // Right panel — detail / info
            VisualElement detailPanel = new VisualElement();
            detailPanel.style.flexBasis = new Length(40, LengthUnit.Percent);
            detailPanel.style.backgroundColor = s_panelColor;
            detailPanel.style.borderTopLeftRadius = 6;
            detailPanel.style.borderTopRightRadius = 6;
            detailPanel.style.borderBottomLeftRadius = 6;
            detailPanel.style.borderBottomRightRadius = 6;
            detailPanel.style.paddingTop = 20;
            detailPanel.style.paddingBottom = 20;
            detailPanel.style.paddingLeft = 20;
            detailPanel.style.paddingRight = 20;
            detailPanel.style.alignItems = Align.Center;
            detailPanel.style.justifyContent = Justify.Center;
            contentArea.Add(detailPanel);

            _detailName = new Label("No world selected");
            _detailName.style.fontSize = 20;
            _detailName.style.color = s_textColor;
            _detailName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _detailName.style.unityTextAlign = TextAnchor.MiddleCenter;
            _detailName.style.marginBottom = 12;
            detailPanel.Add(_detailName);

            _detailInfo = new Label("");
            _detailInfo.style.fontSize = 13;
            _detailInfo.style.color = s_dimTextColor;
            _detailInfo.style.unityTextAlign = TextAnchor.MiddleCenter;
            _detailInfo.style.whiteSpace = WhiteSpace.Normal;
            detailPanel.Add(_detailInfo);

            // Bottom button row
            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 16;
            buttonRow.style.justifyContent = Justify.Center;
            buttonRow.style.width = new Length(80, LengthUnit.Percent);
            buttonRow.style.maxWidth = 900;
            _background.Add(buttonRow);

            _playButton = CreateButton("Play Selected", s_buttonColor, s_buttonHoverColor, OnPlayClicked);
            _playButton.style.flexGrow = 1;
            _playButton.style.marginRight = 8;
            _playButton.SetEnabled(false);
            buttonRow.Add(_playButton);

            Button newButton = CreateButton("Create New World", s_buttonColor, s_buttonHoverColor, OnCreateNewClicked);
            newButton.style.flexGrow = 1;
            newButton.style.marginRight = 8;
            buttonRow.Add(newButton);

            _deleteButton = CreateButton("Delete", s_deleteButtonColor, s_deleteButtonHoverColor, OnDeleteClicked);
            _deleteButton.style.width = 100;
            _deleteButton.SetEnabled(false);
            buttonRow.Add(_deleteButton);

            // Modal overlay (hidden by default)
            _modalOverlay = new VisualElement();
            _modalOverlay.style.position = Position.Absolute;
            _modalOverlay.style.left = 0;
            _modalOverlay.style.top = 0;
            _modalOverlay.style.right = 0;
            _modalOverlay.style.bottom = 0;
            _modalOverlay.style.backgroundColor = s_modalOverlayColor;
            _modalOverlay.style.alignItems = Align.Center;
            _modalOverlay.style.justifyContent = Justify.Center;
            _modalOverlay.style.display = DisplayStyle.None;
            root.Add(_modalOverlay);
        }

        private void PopulateWorldList()
        {
            _worldListScroll.Clear();
            _entryElements.Clear();

            if (_scanResults == null || _scanResults.Count == 0)
            {
                Label empty = new Label("No worlds found. Create a new world to begin.");
                empty.style.fontSize = 14;
                empty.style.color = s_dimTextColor;
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.marginTop = 20;
                empty.style.whiteSpace = WhiteSpace.Normal;
                _worldListScroll.Add(empty);
                return;
            }

            for (int i = 0; i < _scanResults.Count; i++)
            {
                WorldScanEntry entry = _scanResults[i];
                int index = i;

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = s_entryColor;
                row.style.borderTopLeftRadius = 4;
                row.style.borderTopRightRadius = 4;
                row.style.borderBottomLeftRadius = 4;
                row.style.borderBottomRightRadius = 4;
                row.style.paddingTop = 10;
                row.style.paddingBottom = 10;
                row.style.paddingLeft = 12;
                row.style.paddingRight = 12;
                row.style.marginBottom = 4;

                // Game mode dot
                VisualElement dot = new VisualElement();
                dot.style.width = 12;
                dot.style.height = 12;
                dot.style.borderTopLeftRadius = 6;
                dot.style.borderTopRightRadius = 6;
                dot.style.borderBottomLeftRadius = 6;
                dot.style.borderBottomRightRadius = 6;
                dot.style.marginRight = 10;

                if (entry.Metadata != null)
                {
                    dot.style.backgroundColor = entry.Metadata.GameMode == GameMode.Creative
                        ? s_creativeDotColor
                        : s_survivalDotColor;
                }
                else
                {
                    dot.style.backgroundColor = s_dimTextColor;
                }

                row.Add(dot);

                // Name
                string displayName = entry.Metadata?.DisplayName ?? entry.DirectoryName;
                Label nameLabel = new Label(displayName);
                nameLabel.style.fontSize = 15;
                nameLabel.style.color = entry.IsLocked ? s_lockedColor : s_textColor;
                nameLabel.style.flexGrow = 1;
                nameLabel.pickingMode = PickingMode.Ignore;
                row.Add(nameLabel);

                // Locked indicator
                if (entry.IsLocked)
                {
                    Label lockedLabel = new Label("(in use)");
                    lockedLabel.style.fontSize = 12;
                    lockedLabel.style.color = s_lockedColor;
                    lockedLabel.style.marginRight = 10;
                    lockedLabel.pickingMode = PickingMode.Ignore;
                    row.Add(lockedLabel);
                }

                // Last played
                if (entry.Metadata != null)
                {
                    string dateStr = entry.Metadata.LastPlayed.ToLocalTime().ToString("g");
                    Label dateLabel = new Label(dateStr);
                    dateLabel.style.fontSize = 12;
                    dateLabel.style.color = s_dimTextColor;
                    dateLabel.pickingMode = PickingMode.Ignore;
                    row.Add(dateLabel);
                }

                // Hover + click
                row.RegisterCallback<PointerEnterEvent>((PointerEnterEvent evt) =>
                {
                    if (index != _selectedIndex)
                    {
                        row.style.backgroundColor = s_entryHoverColor;
                    }
                });

                row.RegisterCallback<PointerLeaveEvent>((PointerLeaveEvent evt) =>
                {
                    if (index != _selectedIndex)
                    {
                        row.style.backgroundColor = s_entryColor;
                    }
                });

                row.RegisterCallback<PointerDownEvent>((PointerDownEvent evt) =>
                {
                    SelectWorld(index);
                });

                _worldListScroll.Add(row);
                _entryElements.Add(row);
            }
        }

        private void SelectWorld(int index)
        {
            // Deselect previous
            if (_selectedIndex >= 0 && _selectedIndex < _entryElements.Count)
            {
                _entryElements[_selectedIndex].style.backgroundColor = s_entryColor;
            }

            _selectedIndex = index;

            if (index >= 0 && index < _entryElements.Count)
            {
                _entryElements[index].style.backgroundColor = s_entrySelectedColor;
            }

            WorldScanEntry entry = _scanResults[index];
            bool canPlay = entry.Metadata != null && !entry.IsLocked;
            bool canDelete = !entry.IsLocked;

            _playButton.SetEnabled(canPlay);
            _deleteButton.SetEnabled(canDelete);

            // Update detail panel
            if (entry.Metadata != null)
            {
                _detailName.text = entry.Metadata.DisplayName;

                string mode = entry.Metadata.GameMode == GameMode.Creative ? "Creative" : "Survival";
                string lastPlayed = entry.Metadata.LastPlayed.ToLocalTime().ToString("f");
                string created = entry.Metadata.CreationDate.ToLocalTime().ToString("d");
                string seed = entry.Metadata.Seed.ToString();

                _detailInfo.text = $"Mode: {mode}\nLast played: {lastPlayed}\nCreated: {created}\nSeed: {seed}\nFolder: {entry.DirectoryName}";
            }
            else
            {
                _detailName.text = entry.DirectoryName;
                _detailInfo.text = "Corrupt or missing world data";
            }
        }

        private void OnPlayClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _scanResults.Count)
            {
                return;
            }

            WorldScanEntry entry = _scanResults[_selectedIndex];

            if (entry.Metadata == null || entry.IsLocked)
            {
                return;
            }

            WorldLauncher.SetWorld(
                entry.DirectoryPath,
                entry.Metadata.DisplayName,
                entry.Metadata.Seed,
                entry.Metadata.GameMode,
                false);

            Destroy(gameObject);
        }

        private void OnCreateNewClicked()
        {
            ShowCreateModal();
        }

        private void OnDeleteClicked()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _scanResults.Count)
            {
                return;
            }

            WorldScanEntry entry = _scanResults[_selectedIndex];

            if (entry.IsLocked)
            {
                return;
            }

            ShowDeleteConfirmModal(entry);
        }

        private void ShowCreateModal()
        {
            _modalOverlay.Clear();
            _modalOverlay.style.display = DisplayStyle.Flex;

            VisualElement panel = CreateModalPanel(380);

            Label title = new Label("Create New World");
            title.style.fontSize = 22;
            title.style.color = s_textColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 20;
            panel.Add(title);

            // World name field
            Label nameLabel = new Label("World Name");
            nameLabel.style.fontSize = 13;
            nameLabel.style.color = s_dimTextColor;
            nameLabel.style.marginBottom = 4;
            panel.Add(nameLabel);

            TextField nameField = new TextField();
            nameField.value = "New World";
            nameField.style.marginBottom = 12;
            StyleTextField(nameField);
            panel.Add(nameField);

            // Seed field
            Label seedLabel = new Label("Seed (leave empty for random)");
            seedLabel.style.fontSize = 13;
            seedLabel.style.color = s_dimTextColor;
            seedLabel.style.marginBottom = 4;
            panel.Add(seedLabel);

            TextField seedField = new TextField();
            seedField.value = "";
            seedField.style.marginBottom = 12;
            StyleTextField(seedField);
            panel.Add(seedField);

            // Game mode
            Label modeLabel = new Label("Game Mode");
            modeLabel.style.fontSize = 13;
            modeLabel.style.color = s_dimTextColor;
            modeLabel.style.marginBottom = 4;
            panel.Add(modeLabel);

            DropdownField modeField = new DropdownField(
                new List<string> { "Survival", "Creative" }, 0);
            modeField.style.marginBottom = 20;
            StyleDropdown(modeField);
            panel.Add(modeField);

            // Buttons
            VisualElement btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;
            panel.Add(btnRow);

            Button createBtn = CreateButton("Create", s_buttonColor, s_buttonHoverColor, () =>
            {
                string worldName = nameField.value;

                if (string.IsNullOrWhiteSpace(worldName))
                {
                    worldName = "New World";
                }

                long seed;
                string seedText = seedField.value;

                if (string.IsNullOrWhiteSpace(seedText))
                {
                    seed = DateTime.UtcNow.Ticks;
                }
                else if (!long.TryParse(seedText, out seed))
                {
                    seed = seedText.GetHashCode();
                }

                GameMode mode = modeField.index == 1 ? GameMode.Creative : GameMode.Survival;

                string worldDir = WorldDirectoryScanner.CreateWorld(_worldsRoot, worldName, seed, mode);

                WorldLauncher.SetWorld(worldDir, worldName, seed, mode, true);
                _modalOverlay.style.display = DisplayStyle.None;
                Destroy(gameObject);
            });
            createBtn.style.marginRight = 8;
            createBtn.style.width = 120;
            btnRow.Add(createBtn);

            Button cancelBtn = CreateButton("Cancel", s_disabledButtonColor, s_entryHoverColor, () =>
            {
                _modalOverlay.style.display = DisplayStyle.None;
            });
            cancelBtn.style.width = 120;
            btnRow.Add(cancelBtn);

            _modalOverlay.Add(panel);
        }

        private void ShowDeleteConfirmModal(WorldScanEntry entry)
        {
            _modalOverlay.Clear();
            _modalOverlay.style.display = DisplayStyle.Flex;

            VisualElement panel = CreateModalPanel(360);

            Label title = new Label("Delete World");
            title.style.fontSize = 22;
            title.style.color = s_textColor;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.marginBottom = 16;
            panel.Add(title);

            string displayName = entry.Metadata?.DisplayName ?? entry.DirectoryName;
            Label warning = new Label($"'{displayName}' will be lost forever! (A long time!)");
            warning.style.fontSize = 14;
            warning.style.color = s_lockedColor;
            warning.style.unityTextAlign = TextAnchor.MiddleCenter;
            warning.style.whiteSpace = WhiteSpace.Normal;
            warning.style.marginBottom = 20;
            panel.Add(warning);

            VisualElement btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.Center;
            panel.Add(btnRow);

            Button deleteBtn = CreateButton("Delete", s_deleteButtonColor, s_deleteButtonHoverColor, () =>
            {
                WorldDirectoryScanner.DeleteWorld(entry.DirectoryPath);
                _modalOverlay.style.display = DisplayStyle.None;

                // Rescan
                _selectedIndex = -1;
                _playButton.SetEnabled(false);
                _deleteButton.SetEnabled(false);
                _detailName.text = "No world selected";
                _detailInfo.text = "";
                _uiPopulated = false;
                _scanComplete = false;

                StartScan();
            });
            deleteBtn.style.marginRight = 8;
            deleteBtn.style.width = 120;
            btnRow.Add(deleteBtn);

            Button cancelBtn = CreateButton("Cancel", s_disabledButtonColor, s_entryHoverColor, () =>
            {
                _modalOverlay.style.display = DisplayStyle.None;
            });
            cancelBtn.style.width = 120;
            btnRow.Add(cancelBtn);

            _modalOverlay.Add(panel);
        }

        private VisualElement CreateModalPanel(int width)
        {
            VisualElement panel = new VisualElement();
            panel.style.width = width;
            panel.style.backgroundColor = s_panelColor;
            panel.style.borderTopLeftRadius = 8;
            panel.style.borderTopRightRadius = 8;
            panel.style.borderBottomLeftRadius = 8;
            panel.style.borderBottomRightRadius = 8;
            panel.style.paddingTop = 24;
            panel.style.paddingBottom = 24;
            panel.style.paddingLeft = 24;
            panel.style.paddingRight = 24;
            return panel;
        }

        private Button CreateButton(string text, Color normalColor, Color hoverColor, Action onClick)
        {
            Button btn = new Button();
            btn.text = text;
            btn.style.height = 36;
            btn.style.fontSize = 14;
            btn.style.color = s_textColor;
            btn.style.backgroundColor = normalColor;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;

            btn.RegisterCallback<PointerEnterEvent>((PointerEnterEvent evt) =>
            {
                btn.style.backgroundColor = hoverColor;
            });

            btn.RegisterCallback<PointerLeaveEvent>((PointerLeaveEvent evt) =>
            {
                btn.style.backgroundColor = normalColor;
            });

            btn.clicked += onClick;

            return btn;
        }

        private void StyleTextField(TextField field)
        {
            field.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1.0f);
            field.style.color = s_textColor;
            field.style.borderTopLeftRadius = 4;
            field.style.borderTopRightRadius = 4;
            field.style.borderBottomLeftRadius = 4;
            field.style.borderBottomRightRadius = 4;
            field.style.paddingTop = 4;
            field.style.paddingBottom = 4;
            field.style.paddingLeft = 8;
            field.style.paddingRight = 8;
        }

        private void StyleDropdown(DropdownField field)
        {
            field.style.backgroundColor = new Color(0.08f, 0.08f, 0.10f, 1.0f);
            field.style.color = s_textColor;
            field.style.borderTopLeftRadius = 4;
            field.style.borderTopRightRadius = 4;
            field.style.borderBottomLeftRadius = 4;
            field.style.borderBottomRightRadius = 4;
        }
    }
}
