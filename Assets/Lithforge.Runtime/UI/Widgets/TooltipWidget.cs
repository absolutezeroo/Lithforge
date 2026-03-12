using Lithforge.Voxel.Item;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Widgets
{
    /// <summary>
    /// Absolute-positioned tooltip that shows item name, type, stats, and durability.
    /// Uses USS class "lf-tooltip".
    /// </summary>
    public sealed class TooltipWidget : VisualElement
    {
        private readonly Label _nameLabel;
        private readonly Label _typeLabel;
        private readonly Label _statsLabel;
        private readonly Label _durabilityLabel;

        public TooltipWidget()
        {
            AddToClassList("lf-tooltip");
            pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;

            _nameLabel = new Label("");
            _nameLabel.AddToClassList("lf-tooltip__name");
            _nameLabel.pickingMode = PickingMode.Ignore;
            Add(_nameLabel);

            _typeLabel = new Label("");
            _typeLabel.AddToClassList("lf-tooltip__type");
            _typeLabel.pickingMode = PickingMode.Ignore;
            Add(_typeLabel);

            _statsLabel = new Label("");
            _statsLabel.AddToClassList("lf-tooltip__stat");
            _statsLabel.pickingMode = PickingMode.Ignore;
            Add(_statsLabel);

            _durabilityLabel = new Label("");
            _durabilityLabel.AddToClassList("lf-tooltip__durability");
            _durabilityLabel.pickingMode = PickingMode.Ignore;
            Add(_durabilityLabel);
        }

        /// <summary>
        /// Shows the tooltip with data from the given item stack.
        /// </summary>
        public void Show(ItemStack stack, ItemEntry entry, float posX, float posY)
        {
            if (stack.IsEmpty || entry == null)
            {
                Hide();
                return;
            }

            style.display = DisplayStyle.Flex;
            style.left = posX + 16;
            style.top = posY - 8;

            // Name
            _nameLabel.text = FormatFullName(stack.ItemId.Name);

            // Type (tool type)
            if (entry.ToolType != ToolType.None)
            {
                _typeLabel.text = entry.ToolType.ToString();
                _typeLabel.style.display = DisplayStyle.Flex;
            }
            else if (entry.IsBlockItem)
            {
                _typeLabel.text = "Block";
                _typeLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _typeLabel.style.display = DisplayStyle.None;
            }

            // Stats
            string stats = "";

            if (entry.AttackDamage > 0 && entry.ToolType != ToolType.None)
            {
                stats += $"Attack: {entry.AttackDamage:F1}\n";
            }

            if (entry.MiningSpeed > 1f)
            {
                stats += $"Mining Speed: {entry.MiningSpeed:F1}\n";
            }

            if (entry.ToolLevel > 0)
            {
                stats += $"Mining Level: {entry.ToolLevel}\n";
            }

            if (stats.Length > 0)
            {
                _statsLabel.text = stats.TrimEnd('\n');
                _statsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _statsLabel.style.display = DisplayStyle.None;
            }

            // Durability
            if (stack.Durability > 0 && entry.Durability > 0)
            {
                _durabilityLabel.text = $"Durability: {stack.Durability} / {entry.Durability}";
                _durabilityLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _durabilityLabel.style.display = DisplayStyle.None;
            }
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        public void UpdatePosition(float posX, float posY)
        {
            style.left = posX + 16;
            style.top = posY - 8;
        }

        private static string FormatFullName(string name)
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
