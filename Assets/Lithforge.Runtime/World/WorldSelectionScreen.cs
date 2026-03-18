using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.UIElements;

using Cursor = UnityEngine.Cursor;

namespace Lithforge.Runtime.World
{
    /// <summary>
    ///     Full-screen UI Toolkit overlay that lets the player browse, create, and delete
    ///     world saves before entering a game session. Scans the worlds directory on a
    ///     background thread, then populates a scrollable list on the main thread.
    ///     Invokes a callback with a <see cref="WorldSession" /> and destroys itself once
    ///     the player picks a world.
    /// </summary>
    public sealed class WorldSelectionScreen : MonoBehaviour
    {
        private static readonly Color s_backgroundColor = new(0.08f, 0.08f, 0.10f, 1.0f);
        private static readonly Color s_panelColor = new(0.12f, 0.12f, 0.15f, 0.95f);
        private static readonly Color s_entryColor = new(0.16f, 0.16f, 0.20f, 1.0f);
        private static readonly Color s_entryHoverColor = new(0.22f, 0.22f, 0.28f, 1.0f);
        private static readonly Color s_entrySelectedColor = new(0.25f, 0.30f, 0.45f, 1.0f);
        private static readonly Color s_buttonColor = new(0.20f, 0.45f, 0.25f, 1.0f);
        private static readonly Color s_buttonHoverColor = new(0.25f, 0.55f, 0.30f, 1.0f);
        private static readonly Color s_deleteButtonColor = new(0.55f, 0.20f, 0.20f, 1.0f);
        private static readonly Color s_deleteButtonHoverColor = new(0.70f, 0.25f, 0.25f, 1.0f);
        private static readonly Color s_disabledButtonColor = new(0.25f, 0.25f, 0.25f, 1.0f);
        private static readonly Color s_textColor = new(0.90f, 0.90f, 0.88f, 1.0f);
        private static readonly Color s_dimTextColor = new(0.55f, 0.55f, 0.50f, 1.0f);
        private static readonly Color s_logoColor = new(1.0f, 0.95f, 0.80f, 1.0f);
        private static readonly Color s_lockedColor = new(0.80f, 0.40f, 0.40f, 1.0f);
        private static readonly Color s_survivalDotColor = new(0.40f, 0.75f, 0.40f, 1.0f);
        private static readonly Color s_creativeDotColor = new(0.40f, 0.55f, 0.85f, 1.0f);
        private static readonly Color s_modalOverlayColor = new(0f, 0f, 0f, 0.7f);
        private readonly List<VisualElement> _entryElements = new();
        private VisualElement _background;
        private Button _deleteButton;
        private Label _detailInfo;
        private Label _detailName;

        private UIDocument _document;
        private VisualElement _modalOverlay;

        private Action<WorldSession> _onWorldSelected;
        private Button _playButton;
        private volatile bool _scanComplete;
        private List<WorldScanEntry> _scanResults;
        private volatile bool _scanRunning;
        private int _selectedIndex = -1;
        private bool _uiPopulated;
        private ScrollView _worldListScroll;
        private string _worldsRoot;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            if (_scanComplete && !_uiPopulated)
            {
                _uiPopulated = true;
                PopulateWorldList();
            }
        }

        /// <summary>
        ///     Builds the UI hierarchy and kicks off an asynchronous world-directory scan.
        ///     Must be called exactly once after the component is added.
        /// </summary>
        /// <param name="panelSettings">Shared panel settings for the UIDocument (sortingOrder 600).</param>
        public void Initialize(PanelSettings panelSettings, Action<WorldSession> onWorldSelected)
        {
            _onWorldSelected = onWorldSelected;
            _worldsRoot = Path.Combine(Application.persistentDataPath, "worlds");

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
            Thread scanThread = new(ScanWorker)
            {
                IsBackground = true,
            };
            scanThread.Start();
        }

        private void ScanWorker()
        {
            _scanResults = WorldDirectoryScanner.ScanWorlds(_worldsRoot);
            _scanComplete = true;
            _scanRunning = false;
        }

        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            _background = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    backgroundColor = s_backgroundColor,
                    flexDirection = FlexDirection.Column,
                    alignItems = Align.Center,
                    paddingTop = 30,
                    paddingBottom = 30,
                },
            };
            root.Add(_background);

            // Logo
            Label logo = new("LITHFORGE")
            {
                style =
                {
                    fontSize = 48,
                    color = s_logoColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    letterSpacing = 4,
                    marginBottom = 4,
                },
            };
            _background.Add(logo);

            Label subtitle = new("Select World")
            {
                style =
                {
                    fontSize = 18, color = s_dimTextColor, unityTextAlign = TextAnchor.MiddleCenter, marginBottom = 20,
                },
            };
            _background.Add(subtitle);

            // Content area: list on left, detail on right
            VisualElement contentArea = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, flexGrow = 1, width = new Length(80, LengthUnit.Percent), maxWidth = 900,
                },
            };
            _background.Add(contentArea);

            // Left panel — world list
            VisualElement listPanel = new()
            {
                style =
                {
                    flexGrow = 1, flexBasis = new Length(60, LengthUnit.Percent), marginRight = 12,
                },
            };
            contentArea.Add(listPanel);

            _worldListScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 8,
                    paddingRight = 8,
                },
            };
            listPanel.Add(_worldListScroll);

            // Loading placeholder
            Label loadingLabel = new("Scanning worlds...")
            {
                name = "loading-label",
                style =
                {
                    fontSize = 14, color = s_dimTextColor, unityTextAlign = TextAnchor.MiddleCenter, marginTop = 20,
                },
            };
            _worldListScroll.Add(loadingLabel);

            // Right panel — detail / info
            VisualElement detailPanel = new()
            {
                style =
                {
                    flexBasis = new Length(40, LengthUnit.Percent),
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingTop = 20,
                    paddingBottom = 20,
                    paddingLeft = 20,
                    paddingRight = 20,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                },
            };
            contentArea.Add(detailPanel);

            _detailName = new Label("No world selected")
            {
                style =
                {
                    fontSize = 20,
                    color = s_textColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 12,
                },
            };
            detailPanel.Add(_detailName);

            _detailInfo = new Label("")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, unityTextAlign = TextAnchor.MiddleCenter, whiteSpace = WhiteSpace.Normal,
                },
            };
            detailPanel.Add(_detailInfo);

            // Bottom button row
            VisualElement buttonRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 16,
                    justifyContent = Justify.Center,
                    width = new Length(80, LengthUnit.Percent),
                    maxWidth = 900,
                },
            };
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
            _modalOverlay = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    left = 0,
                    top = 0,
                    right = 0,
                    bottom = 0,
                    backgroundColor = s_modalOverlayColor,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    display = DisplayStyle.None,
                },
            };
            root.Add(_modalOverlay);
        }

        private void PopulateWorldList()
        {
            _worldListScroll.Clear();
            _entryElements.Clear();

            if (_scanResults == null || _scanResults.Count == 0)
            {
                Label empty = new("No worlds found. Create a new world to begin.")
                {
                    style =
                    {
                        fontSize = 14,
                        color = s_dimTextColor,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 20,
                        whiteSpace = WhiteSpace.Normal,
                    },
                };
                _worldListScroll.Add(empty);
                return;
            }

            for (int i = 0; i < _scanResults.Count; i++)
            {
                WorldScanEntry entry = _scanResults[i];
                int index = i;

                VisualElement row = new()
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        backgroundColor = s_entryColor,
                        borderTopLeftRadius = 4,
                        borderTopRightRadius = 4,
                        borderBottomLeftRadius = 4,
                        borderBottomRightRadius = 4,
                        paddingTop = 10,
                        paddingBottom = 10,
                        paddingLeft = 12,
                        paddingRight = 12,
                        marginBottom = 4,
                    },
                };

                // Game mode dot
                VisualElement dot = new()
                {
                    style =
                    {
                        width = 12,
                        height = 12,
                        borderTopLeftRadius = 6,
                        borderTopRightRadius = 6,
                        borderBottomLeftRadius = 6,
                        borderBottomRightRadius = 6,
                        marginRight = 10,
                    },
                };

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
                Label nameLabel = new(displayName)
                {
                    style =
                    {
                        fontSize = 15, color = entry.IsLocked ? s_lockedColor : s_textColor, flexGrow = 1,
                    },
                    pickingMode = PickingMode.Ignore,
                };
                row.Add(nameLabel);

                // Locked indicator
                if (entry.IsLocked)
                {
                    Label lockedLabel = new("(in use)")
                    {
                        style =
                        {
                            fontSize = 12, color = s_lockedColor, marginRight = 10,
                        },
                        pickingMode = PickingMode.Ignore,
                    };
                    row.Add(lockedLabel);
                }

                // Last played
                if (entry.Metadata != null)
                {
                    string dateStr = entry.Metadata.LastPlayed.ToLocalTime().ToString("g");
                    Label dateLabel = new(dateStr)
                    {
                        style =
                        {
                            fontSize = 12, color = s_dimTextColor,
                        },
                        pickingMode = PickingMode.Ignore,
                    };
                    row.Add(dateLabel);
                }

                // Hover + click
                row.RegisterCallback((PointerEnterEvent evt) =>
                {
                    if (index != _selectedIndex)
                    {
                        row.style.backgroundColor = s_entryHoverColor;
                    }
                });

                row.RegisterCallback((PointerLeaveEvent evt) =>
                {
                    if (index != _selectedIndex)
                    {
                        row.style.backgroundColor = s_entryColor;
                    }
                });

                row.RegisterCallback((PointerDownEvent evt) =>
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

            WorldSession session = new(
                entry.DirectoryPath,
                entry.Metadata.DisplayName,
                entry.Metadata.Seed,
                entry.Metadata.GameMode,
                false);
            _onWorldSelected?.Invoke(session);

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

            Label title = new("Create New World")
            {
                style =
                {
                    fontSize = 22,
                    color = s_textColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 20,
                },
            };
            panel.Add(title);

            // World name field
            Label nameLabel = new("World Name")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, marginBottom = 4,
                },
            };
            panel.Add(nameLabel);

            TextField nameField = new()
            {
                value = "New World",
                style =
                {
                    marginBottom = 12,
                },
            };
            StyleTextField(nameField);
            panel.Add(nameField);

            // Seed field
            Label seedLabel = new("Seed (leave empty for random)")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, marginBottom = 4,
                },
            };
            panel.Add(seedLabel);

            TextField seedField = new()
            {
                value = "",
                style =
                {
                    marginBottom = 12,
                },
            };
            StyleTextField(seedField);
            panel.Add(seedField);

            // Game mode
            Label modeLabel = new("Game Mode")
            {
                style =
                {
                    fontSize = 13, color = s_dimTextColor, marginBottom = 4,
                },
            };
            panel.Add(modeLabel);

            DropdownField modeField = new(
                new List<string>
                {
                    "Survival", "Creative",
                }, 0)
            {
                style =
                {
                    marginBottom = 20,
                },
            };
            StyleDropdown(modeField);
            panel.Add(modeField);

            // Buttons
            VisualElement btnRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.Center,
                },
            };
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

                WorldSession session = new(worldDir, worldName, seed, mode, true);
                _onWorldSelected?.Invoke(session);
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

            Label title = new("Delete World")
            {
                style =
                {
                    fontSize = 22,
                    color = s_textColor,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginBottom = 16,
                },
            };
            panel.Add(title);

            string displayName = entry.Metadata?.DisplayName ?? entry.DirectoryName;
            Label warning = new($"'{displayName}' will be lost forever! (A long time!)")
            {
                style =
                {
                    fontSize = 14,
                    color = s_lockedColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    whiteSpace = WhiteSpace.Normal,
                    marginBottom = 20,
                },
            };
            panel.Add(warning);

            VisualElement btnRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.Center,
                },
            };
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
            VisualElement panel = new()
            {
                style =
                {
                    width = width,
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 24,
                    paddingBottom = 24,
                    paddingLeft = 24,
                    paddingRight = 24,
                },
            };
            return panel;
        }

        private Button CreateButton(string text, Color normalColor, Color hoverColor, Action onClick)
        {
            Button btn = new()
            {
                text = text,
                style =
                {
                    height = 36,
                    fontSize = 14,
                    color = s_textColor,
                    backgroundColor = normalColor,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    unityFontStyleAndWeight = FontStyle.Bold,
                },
            };

            btn.RegisterCallback((PointerEnterEvent evt) =>
            {
                btn.style.backgroundColor = hoverColor;
            });

            btn.RegisterCallback((PointerLeaveEvent evt) =>
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
