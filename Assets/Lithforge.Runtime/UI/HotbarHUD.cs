using Lithforge.Voxel.Item;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Displays the 9-slot hotbar at the bottom center of the screen.
    /// Shows item names and counts, highlights the selected slot.
    /// Uses UI Toolkit with code-driven VisualElement construction.
    /// </summary>
    public sealed class HotbarHUD : MonoBehaviour
    {
        private const int _slotSize = 60;
        private const int _slotMargin = 3;
        private const int _bottomOffset = 12;
        private const int _borderWidth = 2;

        private const float _itemNameDuration = 2.0f;

        private UIDocument _document;
        private Inventory _inventory;
        private ItemRegistry _itemRegistry;
        private VisualElement[] _slotElements;
        private Label[] _countLabels;
        private Label[] _nameLabels;
        private Label _itemNameLabel;
        private float _itemNameTimer;
        private int _lastSelectedSlot = -1;

        private static readonly Color _slotBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color _slotBorder = new Color(0.4f, 0.4f, 0.4f, 0.9f);
        private static readonly Color _selectedBorder = new Color(1f, 1f, 1f, 1f);

        /// <summary>
        /// Shows or hides the hotbar by toggling the root document visibility.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Initialize(Inventory inventory, PanelSettings panelSettings, ItemRegistry itemRegistry = null)
        {
            _inventory = inventory;
            _itemRegistry = itemRegistry;

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 50;

            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            BuildHotbar(root);
        }

        private void BuildHotbar(VisualElement root)
        {
            _slotElements = new VisualElement[Inventory.HotbarSize];
            _countLabels = new Label[Inventory.HotbarSize];
            _nameLabels = new Label[Inventory.HotbarSize];

            // Hotbar container at bottom center
            VisualElement hotbar = new VisualElement();
            hotbar.name = "hotbar";
            hotbar.pickingMode = PickingMode.Ignore;
            hotbar.style.position = Position.Absolute;
            hotbar.style.bottom = _bottomOffset;
            hotbar.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            int totalWidth = Inventory.HotbarSize * (_slotSize + _slotMargin * 2);
            hotbar.style.marginLeft = -(totalWidth / 2);
            hotbar.style.flexDirection = FlexDirection.Row;
            root.Add(hotbar);

            // Item name tooltip above hotbar
            _itemNameLabel = new Label("");
            _itemNameLabel.name = "item-name-tooltip";
            _itemNameLabel.pickingMode = PickingMode.Ignore;
            _itemNameLabel.style.position = Position.Absolute;
            _itemNameLabel.style.bottom = _bottomOffset + _slotSize + _slotMargin * 2 + 8;
            _itemNameLabel.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            _itemNameLabel.style.marginLeft = -150;
            _itemNameLabel.style.width = 300;
            _itemNameLabel.style.fontSize = 16;
            _itemNameLabel.style.color = Color.white;
            _itemNameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _itemNameLabel.style.opacity = 0f;
            root.Add(_itemNameLabel);

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                VisualElement slot = new VisualElement();
                slot.name = $"slot-{i}";
                slot.pickingMode = PickingMode.Ignore;
                slot.style.width = _slotSize;
                slot.style.height = _slotSize;
                slot.style.marginLeft = _slotMargin;
                slot.style.marginRight = _slotMargin;
                slot.style.backgroundColor = _slotBackground;
                slot.style.borderTopWidth = _borderWidth;
                slot.style.borderBottomWidth = _borderWidth;
                slot.style.borderLeftWidth = _borderWidth;
                slot.style.borderRightWidth = _borderWidth;
                slot.style.borderTopColor = _slotBorder;
                slot.style.borderBottomColor = _slotBorder;
                slot.style.borderLeftColor = _slotBorder;
                slot.style.borderRightColor = _slotBorder;
                slot.style.justifyContent = Justify.Center;
                slot.style.alignItems = Align.Center;

                // Item name label (short)
                Label nameLabel = new Label("");
                nameLabel.name = $"name-{i}";
                nameLabel.pickingMode = PickingMode.Ignore;
                nameLabel.style.fontSize = 12;
                nameLabel.style.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                nameLabel.style.overflow = Overflow.Hidden;
                nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
                nameLabel.style.maxWidth = _slotSize - 4;
                slot.Add(nameLabel);

                // Count label (bottom right)
                Label countLabel = new Label("");
                countLabel.name = $"count-{i}";
                countLabel.pickingMode = PickingMode.Ignore;
                countLabel.style.position = Position.Absolute;
                countLabel.style.bottom = 2;
                countLabel.style.right = 4;
                countLabel.style.fontSize = 13;
                countLabel.style.color = new Color(1f, 1f, 1f, 1f);
                countLabel.style.unityTextAlign = TextAnchor.LowerRight;
                slot.Add(countLabel);

                hotbar.Add(slot);
                _slotElements[i] = slot;
                _countLabels[i] = countLabel;
                _nameLabels[i] = nameLabel;
            }
        }

        private void Update()
        {
            if (_inventory == null)
            {
                return;
            }

            int selectedSlot = _inventory.SelectedSlot;

            // Show item name tooltip when selected slot changes
            if (selectedSlot != _lastSelectedSlot)
            {
                for (int i = 0; i < Inventory.HotbarSize; i++)
                {
                    Color borderColor = (i == selectedSlot) ? _selectedBorder : _slotBorder;
                    _slotElements[i].style.borderTopColor = borderColor;
                    _slotElements[i].style.borderBottomColor = borderColor;
                    _slotElements[i].style.borderLeftColor = borderColor;
                    _slotElements[i].style.borderRightColor = borderColor;
                }

                ItemStack selectedStack = _inventory.GetSlot(selectedSlot);

                if (!selectedStack.IsEmpty)
                {
                    _itemNameLabel.text = FormatFullItemName(selectedStack.ItemId.Name);
                    _itemNameLabel.style.opacity = 1f;
                    _itemNameTimer = _itemNameDuration;
                }
                else
                {
                    _itemNameLabel.text = "";
                    _itemNameLabel.style.opacity = 0f;
                    _itemNameTimer = 0f;
                }

                _lastSelectedSlot = selectedSlot;
            }

            // Fade out item name tooltip
            if (_itemNameTimer > 0f)
            {
                _itemNameTimer -= Time.deltaTime;

                if (_itemNameTimer <= 0f)
                {
                    _itemNameLabel.style.opacity = 0f;
                }
            }

            // Update item display
            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                ItemStack stack = _inventory.GetSlot(i);

                if (stack.IsEmpty)
                {
                    _nameLabels[i].text = "";
                    _countLabels[i].text = "";
                }
                else
                {
                    _nameLabels[i].text = ItemDisplayFormatter.FormatItemName(stack.ItemId.Name);
                    _countLabels[i].text = stack.Count > 1 ? stack.Count.ToString() : "";
                }
            }
        }

        private static string FormatFullItemName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "";
            }

            string[] parts = name.Split('_');

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0)
                {
                    parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1);
                }
            }

            return string.Join(" ", parts);
        }
    }
}
