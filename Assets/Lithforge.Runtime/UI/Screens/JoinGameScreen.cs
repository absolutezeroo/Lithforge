using System;
using System.Collections.Generic;

using Lithforge.Runtime.Network;
using Lithforge.Runtime.UI.Navigation;
using Lithforge.Runtime.World;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Join Game screen with three sections: Direct Connect (IP + port fields),
    ///     Saved/Recent Servers (persisted list), and LAN Servers (auto-discovered).
    ///     Selecting a server or filling in direct connect fields and clicking Connect
    ///     pushes the <see cref="ConnectionProgressScreen" />.
    /// </summary>
    public sealed class JoinGameScreen : MonoBehaviour, IScreen
    {
        /// <summary>Debounce interval for LAN discovery UI updates.</summary>
        private const float LanRefreshInterval = 0.5f;
        /// <summary>Background color for the full-screen overlay.</summary>
        private static readonly Color s_backgroundColor = new(0.06f, 0.06f, 0.08f, 1.0f);

        /// <summary>Background color for the main content panel.</summary>
        private static readonly Color s_panelColor = new(0.10f, 0.10f, 0.13f, 0.95f);

        /// <summary>Background color for section containers (direct connect, server lists).</summary>
        private static readonly Color s_sectionColor = new(0.08f, 0.08f, 0.10f, 1.0f);

        /// <summary>Normal color for the primary Connect button.</summary>
        private static readonly Color s_buttonColor = new(0.18f, 0.40f, 0.22f, 1.0f);

        /// <summary>Hover color for the primary Connect button.</summary>
        private static readonly Color s_buttonHoverColor = new(0.22f, 0.50f, 0.28f, 1.0f);

        /// <summary>Normal color for the delete server button.</summary>
        private static readonly Color s_dangerButtonColor = new(0.45f, 0.18f, 0.18f, 1.0f);

        /// <summary>Hover color for the delete server button.</summary>
        private static readonly Color s_dangerButtonHoverColor = new(0.58f, 0.22f, 0.22f, 1.0f);

        /// <summary>Normal color for the Back button.</summary>
        private static readonly Color s_secondaryButtonColor = new(0.22f, 0.22f, 0.28f, 1.0f);

        /// <summary>Hover color for the Back button.</summary>
        private static readonly Color s_secondaryButtonHoverColor = new(0.30f, 0.30f, 0.38f, 1.0f);

        /// <summary>Standard text color for labels and button text.</summary>
        private static readonly Color s_textColor = new(0.92f, 0.92f, 0.90f, 1.0f);

        /// <summary>Dimmed text color for section headers and status messages.</summary>
        private static readonly Color s_dimTextColor = new(0.50f, 0.50f, 0.48f, 1.0f);

        /// <summary>Background color for text input fields.</summary>
        private static readonly Color s_fieldBgColor = new(0.05f, 0.05f, 0.07f, 1.0f);

        /// <summary>Border color for text input fields.</summary>
        private static readonly Color s_fieldBorderColor = new(0.25f, 0.25f, 0.30f, 1.0f);

        /// <summary>Background color for the selected server row highlight.</summary>
        private static readonly Color s_selectedColor = new(0.18f, 0.40f, 0.22f, 0.3f);

        /// <summary>Background color for server rows on hover.</summary>
        private static readonly Color s_rowHoverColor = new(0.14f, 0.14f, 0.18f, 1.0f);

        /// <summary>Default background color for server rows.</summary>
        private static readonly Color s_rowColor = new(0.08f, 0.08f, 0.10f, 1.0f);

        /// <summary>List of LAN servers discovered by the broadcast listener.</summary>
        private readonly List<LanServerEntry> _lanResults = new();

        /// <summary>Text field for the server address (IP or hostname).</summary>
        private TextField _addressField;

        /// <summary>Button that initiates the connection to the entered server.</summary>
        private Button _connectButton;

        /// <summary>UI Toolkit document hosting the join game panel.</summary>
        private UIDocument _document;

        /// <summary>True when this screen is currently visible and polling for LAN servers.</summary>
        private bool _isVisible;

        /// <summary>Visual container holding the LAN server discovery result rows.</summary>
        private VisualElement _lanListContainer;

        /// <summary>Background listener that discovers LAN servers via UDP broadcast.</summary>
        private LanDiscoveryListener _lanListener;

        /// <summary>Accumulated time since the last LAN server list UI refresh.</summary>
        private float _lanRefreshTimer;

        /// <summary>Status label shown when no LAN servers have been discovered yet.</summary>
        private Label _lanStatusLabel;

        /// <summary>Callback invoked with the client session config when the user clicks Connect.</summary>
        private Action<SessionConfig.Client> _onConnect;

        /// <summary>Text field for the player display name.</summary>
        private TextField _playerNameField;

        /// <summary>Text field for the server port number.</summary>
        private TextField _portField;

        /// <summary>Visual container holding the saved/recent server list rows.</summary>
        private VisualElement _savedListContainer;

        /// <summary>Persisted list of saved server entries for the recent servers section.</summary>
        private SavedServerList _savedServerList;

        /// <summary>Screen manager for navigating back to the main menu.</summary>
        private ScreenManager _screenManager;

        /// <summary>Polls the LAN discovery listener at a throttled interval and refreshes the server list UI.</summary>
        private void Update()
        {
            if (!_isVisible || _lanListener == null)
            {
                return;
            }

            _lanRefreshTimer += Time.deltaTime;

            if (_lanRefreshTimer >= LanRefreshInterval)
            {
                _lanRefreshTimer = 0f;
                _lanListener.DrainDiscovered(_lanResults);
                RefreshLanServerList();
            }
        }

        /// <summary>Disposes the LAN discovery listener when the screen is destroyed.</summary>
        private void OnDestroy()
        {
            _lanListener?.Dispose();
        }

        /// <summary>Returns the screen name identifier for the join game screen.</summary>
        public string ScreenName { get { return ScreenNames.JoinGame; } }

        /// <summary>Returns true because the join game screen consumes all input.</summary>
        public bool IsInputOpaque { get { return true; } }

        /// <summary>Returns true because the join game screen requires a visible cursor.</summary>
        public bool RequiresCursor { get { return true; } }

        /// <summary>Shows the screen, starts LAN discovery, refreshes saved servers, and pre-fills the most recent address.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.Flex;
            }

            _isVisible = true;

            // Start LAN discovery when screen is shown
            if (_lanListener != null)
            {
                _lanListener.Start();
            }

            // Refresh saved server list
            RefreshSavedServerList();

            // Pre-fill address from most recent server
            if (!args.IsReturning)
            {
                SavedServerEntry recent = _savedServerList.GetMostRecent();

                if (recent != null)
                {
                    _addressField.value = recent.address;
                    _portField.value = recent.port.ToString();
                    _playerNameField.value = recent.playerName ?? "";
                }
            }
        }

        /// <summary>Hides the screen, stops LAN discovery, and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display = DisplayStyle.None;
            }

            _isVisible = false;

            // Stop LAN discovery without blocking the main thread
            _lanListener?.Stop();

            onComplete();
        }

        /// <summary>Returns false to allow the screen manager to pop back to the main menu.</summary>
        public bool HandleEscape()
        {
            // Let ScreenManager pop us back to MainMenu
            return false;
        }

        /// <summary>
        ///     Initializes the Join Game screen with its UIDocument and dependencies.
        /// </summary>
        public void Initialize(
            PanelSettings panelSettings,
            ScreenManager screenManager,
            SavedServerList savedServerList,
            Action<SessionConfig.Client> onConnect)
        {
            _screenManager = screenManager;
            _savedServerList = savedServerList;
            _onConnect = onConnect;
            _lanListener = new LanDiscoveryListener();

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 700;

            BuildUI(_document.rootVisualElement);

            // Start hidden
            _document.rootVisualElement.style.display = DisplayStyle.None;
        }

        /// <summary>Constructs the full layout with direct connect fields, saved/LAN server lists, and action buttons.</summary>
        private void BuildUI(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;

            VisualElement background = new()
            {
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

            // Title
            Label title = new("Join Game")
            {
                style =
                {
                    fontSize = 32,
                    color = s_textColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 24,
                },
            };
            background.Add(title);

            // Main panel
            VisualElement panel = new()
            {
                style =
                {
                    width = 520,
                    maxHeight = new Length(80, LengthUnit.Percent),
                    backgroundColor = s_panelColor,
                    borderTopLeftRadius = 8,
                    borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8,
                    borderBottomRightRadius = 8,
                    paddingTop = 20,
                    paddingBottom = 20,
                    paddingLeft = 24,
                    paddingRight = 24,
                    flexDirection = FlexDirection.Column,
                },
            };
            background.Add(panel);

            // -- Direct Connect section --
            BuildSectionHeader(panel, "Direct Connect");
            BuildDirectConnectSection(panel);

            // -- Saved Servers section --
            BuildSectionHeader(panel, "Saved Servers");
            _savedListContainer = BuildServerListContainer(panel);

            // -- LAN Servers section --
            BuildSectionHeader(panel, "LAN Servers");
            _lanListContainer = BuildServerListContainer(panel);

            _lanStatusLabel = new Label("Searching for LAN servers...")
            {
                style =
                {
                    fontSize = 12,
                    color = s_dimTextColor,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginTop = 4,
                    marginBottom = 4,
                },
            };
            _lanListContainer.Add(_lanStatusLabel);

            // -- Button bar --
            VisualElement buttonBar = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginTop = 16,
                },
            };
            panel.Add(buttonBar);

            Button backBtn = BuildButton("Back", s_secondaryButtonColor, s_secondaryButtonHoverColor, 120);
            backBtn.clicked += OnBackClicked;
            buttonBar.Add(backBtn);

            _connectButton = BuildButton("Connect", s_buttonColor, s_buttonHoverColor, 160);
            _connectButton.clicked += OnConnectClicked;
            buttonBar.Add(_connectButton);
        }

        /// <summary>Builds the direct connect section with address, port, and player name text fields.</summary>
        private void BuildDirectConnectSection(VisualElement parent)
        {
            VisualElement section = new()
            {
                style =
                {
                    backgroundColor = s_sectionColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingTop = 12,
                    paddingBottom = 12,
                    paddingLeft = 16,
                    paddingRight = 16,
                    marginBottom = 16,
                },
            };
            parent.Add(section);

            // Address + Port row
            VisualElement addrRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row, marginBottom = 8,
                },
            };
            section.Add(addrRow);

            // Address field
            VisualElement addrGroup = new()
            {
                style =
                {
                    flexGrow = 1, marginRight = 8,
                },
            };
            addrRow.Add(addrGroup);

            Label addrLabel = new("Address")
            {
                style =
                {
                    fontSize = 12, color = s_dimTextColor, marginBottom = 4,
                },
            };
            addrGroup.Add(addrLabel);

            _addressField = new TextField
            {
                value = "localhost",
            };
            ApplyFieldStyle(_addressField);
            addrGroup.Add(_addressField);

            // Port field
            VisualElement portGroup = new()
            {
                style =
                {
                    width = 80,
                },
            };
            addrRow.Add(portGroup);

            Label portLabel = new("Port")
            {
                style =
                {
                    fontSize = 12, color = s_dimTextColor, marginBottom = 4,
                },
            };
            portGroup.Add(portLabel);

            _portField = new TextField
            {
                value = "7777",
            };
            ApplyFieldStyle(_portField);
            portGroup.Add(_portField);

            // Player name row
            Label nameLabel = new("Player Name")
            {
                style =
                {
                    fontSize = 12, color = s_dimTextColor, marginBottom = 4,
                },
            };
            section.Add(nameLabel);

            _playerNameField = new TextField
            {
                value = "Player",
            };
            ApplyFieldStyle(_playerNameField);
            section.Add(_playerNameField);
        }

        /// <summary>Creates a scrollable container for server list rows with rounded corners and max height.</summary>
        private VisualElement BuildServerListContainer(VisualElement parent)
        {
            VisualElement container = new()
            {
                style =
                {
                    backgroundColor = s_sectionColor,
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 4,
                    paddingRight = 4,
                    marginBottom = 16,
                    maxHeight = 140,
                    overflow = Overflow.Hidden,
                },
            };
            parent.Add(container);

            return container;
        }

        /// <summary>Clears and rebuilds the saved server list UI from the persisted entries.</summary>
        private void RefreshSavedServerList()
        {
            _savedListContainer.Clear();

            IReadOnlyList<SavedServerEntry> entries = _savedServerList.Entries;

            if (entries.Count == 0)
            {
                Label empty = new("No saved servers")
                {
                    style =
                    {
                        fontSize = 12,
                        color = s_dimTextColor,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 8,
                        marginBottom = 8,
                    },
                };
                _savedListContainer.Add(empty);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                SavedServerEntry entry = entries[i];
                VisualElement row = BuildSavedServerRow(entry);
                _savedListContainer.Add(row);
            }
        }

        /// <summary>Creates a clickable row for a saved server entry with name, address, and delete button.</summary>
        private VisualElement BuildSavedServerRow(SavedServerEntry entry)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 8,
                    paddingRight = 8,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    backgroundColor = s_rowColor,
                    marginBottom = 2,
                },
            };

            // Hover effect
            row.RegisterCallback((PointerEnterEvent evt) =>
            {
                row.style.backgroundColor = s_rowHoverColor;
            });
            row.RegisterCallback((PointerLeaveEvent evt) =>
            {
                row.style.backgroundColor = s_rowColor;
            });

            // Click to fill fields
            string address = entry.address;
            ushort port = entry.port;
            string playerName = entry.playerName;
            row.RegisterCallback((ClickEvent evt) =>
            {
                _addressField.value = address;
                _portField.value = port.ToString();
                OnConnectClicked();

                if (!string.IsNullOrEmpty(playerName))
                {
                    _playerNameField.value = playerName;
                }
            });

            // Server info
            VisualElement info = new()
            {
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(info);

            string displayName = !string.IsNullOrEmpty(entry.name) ? entry.name : $"{entry.address}:{entry.port}";
            Label nameLabel = new(displayName)
            {
                style =
                {
                    fontSize = 13, color = s_textColor, unityFontStyleAndWeight = FontStyle.Bold,
                },
            };
            info.Add(nameLabel);

            Label addrLabel = new($"{entry.address}:{entry.port}")
            {
                style =
                {
                    fontSize = 11, color = s_dimTextColor,
                },
            };
            info.Add(addrLabel);

            // Delete button
            Button deleteBtn = new()
            {
                text = "X",
                style =
                {
                    width = 24,
                    height = 24,
                    fontSize = 12,
                    color = s_textColor,
                    backgroundColor = s_dangerButtonColor,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    unityTextAlign = TextAnchor.MiddleCenter,
                },
            };
            deleteBtn.RegisterCallback((PointerEnterEvent evt) =>
            {
                deleteBtn.style.backgroundColor = s_dangerButtonHoverColor;
            });
            deleteBtn.RegisterCallback((PointerLeaveEvent evt) =>
            {
                deleteBtn.style.backgroundColor = s_dangerButtonColor;
            });
            deleteBtn.clicked += () =>
            {
                _savedServerList.Remove(entry.address, entry.port);
                RefreshSavedServerList();
            };
            row.Add(deleteBtn);

            return row;
        }

        /// <summary>Clears and rebuilds the LAN server list UI from the latest discovery results.</summary>
        private void RefreshLanServerList()
        {
            _lanListContainer.Clear();

            if (_lanResults.Count == 0)
            {
                _lanStatusLabel = new Label("Searching for LAN servers...")
                {
                    style =
                    {
                        fontSize = 12,
                        color = s_dimTextColor,
                        unityTextAlign = TextAnchor.MiddleCenter,
                        marginTop = 8,
                        marginBottom = 8,
                    },
                };
                _lanListContainer.Add(_lanStatusLabel);
                return;
            }

            for (int i = 0; i < _lanResults.Count; i++)
            {
                LanServerEntry entry = _lanResults[i];
                VisualElement row = BuildLanServerRow(entry);
                _lanListContainer.Add(row);
            }
        }

        /// <summary>Creates a clickable row for a discovered LAN server with name, address, player count, and game mode.</summary>
        private VisualElement BuildLanServerRow(LanServerEntry entry)
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 8,
                    paddingRight = 8,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    backgroundColor = s_rowColor,
                    marginBottom = 2,
                },
            };

            // Hover
            row.RegisterCallback((PointerEnterEvent evt) =>
            {
                row.style.backgroundColor = s_rowHoverColor;
            });
            row.RegisterCallback((PointerLeaveEvent evt) =>
            {
                row.style.backgroundColor = s_rowColor;
            });

            // Click to fill fields
            string address = entry.Address;
            ushort gamePort = entry.Info.gamePort;

            row.RegisterCallback((ClickEvent evt) =>
            {
                _addressField.value = address;
                _portField.value = gamePort.ToString();

                OnConnectClicked();
            });

            // Server info
            VisualElement info = new()
            {
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(info);

            string serverName = !string.IsNullOrEmpty(entry.Info.serverName)
                ? entry.Info.serverName
                : entry.Address;
            Label nameLabel = new(serverName)
            {
                style =
                {
                    fontSize = 13, color = s_textColor, unityFontStyleAndWeight = FontStyle.Bold,
                },
            };
            info.Add(nameLabel);

            string details = $"{entry.Address}:{entry.Info.gamePort}  •  " +
                             $"{entry.Info.playerCount}/{entry.Info.maxPlayers}  •  " +
                             $"{entry.Info.gameMode}";
            Label detailLabel = new(details)
            {
                style =
                {
                    fontSize = 11, color = s_dimTextColor,
                },
            };
            info.Add(detailLabel);

            return row;
        }

        /// <summary>Validates address and port fields, saves the server entry, and invokes the connect callback.</summary>
        private void OnConnectClicked()
        {
            string address = _addressField.value?.Trim();
            string portText = _portField.value?.Trim();
            string playerName = _playerNameField.value?.Trim();

            if (string.IsNullOrEmpty(address))
            {
                UnityEngine.Debug.LogWarning("[JoinGameScreen] Address is empty.");
                return;
            }

            if (!ushort.TryParse(portText, out ushort port) || port == 0)
            {
                UnityEngine.Debug.LogWarning("[JoinGameScreen] Invalid port.");
                return;
            }

            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Player";
            }

            // Save to recent servers (preserve existing display name if present)
            string existingName = null;
            IReadOnlyList<SavedServerEntry> existing = _savedServerList.Entries;

            for (int i = 0; i < existing.Count; i++)
            {
                if (string.Equals(existing[i].address, address, StringComparison.OrdinalIgnoreCase) &&
                    existing[i].port == port &&
                    !string.IsNullOrEmpty(existing[i].name))
                {
                    existingName = existing[i].name;
                    break;
                }
            }

            SavedServerEntry savedEntry = new()
            {
                name = existingName ?? address,
                address = address,
                port = port,
                playerName = playerName,
                lastConnected = DateTime.UtcNow.ToString("o"),
            };
            _savedServerList.AddOrUpdate(savedEntry);

            // Create client session config and invoke callback
            SessionConfig.Client clientConfig = new(address, port, playerName);
            _onConnect?.Invoke(clientConfig);
        }

        /// <summary>Pops the screen manager stack to return to the main menu.</summary>
        private void OnBackClicked()
        {
            _screenManager.Pop();
        }

        /// <summary>Adds a bold section header label to the parent container.</summary>
        private void BuildSectionHeader(VisualElement parent, string text)
        {
            Label header = new(text)
            {
                style =
                {
                    fontSize = 14, color = s_textColor, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8,
                },
            };
            parent.Add(header);
        }

        /// <summary>Applies consistent dark-theme styling to a text input field including inner text element.</summary>
        private void ApplyFieldStyle(TextField field)
        {
            field.style.fontSize = 14;
            field.style.color = s_textColor;
            field.style.backgroundColor = s_fieldBgColor;
            field.style.borderTopWidth = 1;
            field.style.borderBottomWidth = 1;
            field.style.borderLeftWidth = 1;
            field.style.borderRightWidth = 1;
            field.style.borderTopColor = s_fieldBorderColor;
            field.style.borderBottomColor = s_fieldBorderColor;
            field.style.borderLeftColor = s_fieldBorderColor;
            field.style.borderRightColor = s_fieldBorderColor;
            field.style.borderTopLeftRadius = 4;
            field.style.borderTopRightRadius = 4;
            field.style.borderBottomLeftRadius = 4;
            field.style.borderBottomRightRadius = 4;
            field.style.paddingTop = 6;
            field.style.paddingBottom = 6;
            field.style.paddingLeft = 8;
            field.style.paddingRight = 8;

            // Style the inner text input element (UI Toolkit nests it under #unity-text-input)
            VisualElement textInput = field.Q("unity-text-input");
            if (textInput != null)
            {
                textInput.style.color = s_textColor;
                textInput.style.backgroundColor = s_fieldBgColor;
                textInput.style.borderTopWidth = 0;
                textInput.style.borderBottomWidth = 0;
                textInput.style.borderLeftWidth = 0;
                textInput.style.borderRightWidth = 0;
            }
        }

        /// <summary>Creates a styled button with specified dimensions and hover color transition.</summary>
        private Button BuildButton(string text, Color normalColor, Color hoverColor, int width)
        {
            Button btn = new()
            {
                text = text,
                style =
                {
                    width = width,
                    height = 40,
                    fontSize = 14,
                    color = s_textColor,
                    backgroundColor = normalColor,
                    borderTopWidth = 0,
                    borderBottomWidth = 0,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
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

            return btn;
        }
    }
}
