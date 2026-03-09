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

        private UIDocument _document;
        private Inventory _inventory;
        private VisualElement[] _slotElements;
        private Label[] _countLabels;
        private Label[] _nameLabels;
        private int _lastSelectedSlot = -1;

        private static readonly Color _slotBackground = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color _slotBorder = new Color(0.4f, 0.4f, 0.4f, 0.9f);
        private static readonly Color _selectedBorder = new Color(1f, 1f, 1f, 1f);

        public void Initialize(Inventory inventory, PanelSettings panelSettings)
        {
            _inventory = inventory;

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

            for (int i = 0; i < Inventory.HotbarSize; i++)
            {
                ItemStack stack = _inventory.GetSlot(i);

                // Update selection highlight
                if (selectedSlot != _lastSelectedSlot)
                {
                    Color borderColor = (i == selectedSlot) ? _selectedBorder : _slotBorder;
                    _slotElements[i].style.borderTopColor = borderColor;
                    _slotElements[i].style.borderBottomColor = borderColor;
                    _slotElements[i].style.borderLeftColor = borderColor;
                    _slotElements[i].style.borderRightColor = borderColor;
                }

                // Update item display
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

            _lastSelectedSlot = selectedSlot;
        }

    }
}
