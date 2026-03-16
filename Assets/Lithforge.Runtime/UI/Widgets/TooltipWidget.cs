using Lithforge.Core.Data;
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

            // Resolve tool data from CustomData
            ToolInstance tool = null;
            bool isToolPart = false;

            if (stack.HasCustomData)
            {
                tool = ToolInstanceSerializer.Deserialize(stack.CustomData);

                // Check for generic tool part (if not a tool instance)
                if (tool == null &&
                    ToolPartDataSerializer.TryDeserialize(stack.CustomData, out ToolPartData partData))
                {
                    isToolPart = true;
                    string matName = FormatFullName(partData.MaterialId.Name);
                    string partName = partData.PartType.ToString();
                    _nameLabel.text = matName + " " + partName;
                }
            }

            // Type (tool type or part type)
            if (tool != null)
            {
                _typeLabel.text = tool.ToolType.ToString();
                _typeLabel.style.display = DisplayStyle.Flex;
            }
            else if (isToolPart)
            {
                _typeLabel.text = "Tool Part";
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

            if (tool != null)
            {
                if (tool.BaseDamage > 0)
                {
                    stats += $"Attack: {tool.BaseDamage:F1}\n";
                }

                if (tool.BaseSpeed > 1f)
                {
                    stats += $"Mining Speed: {tool.BaseSpeed:F1}\n";
                }

                if (tool.EffectiveToolLevel > 0)
                {
                    stats += $"Mining Level: {tool.EffectiveToolLevel}\n";
                }
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
            if (tool != null && tool.MaxDurability > 0)
            {
                _durabilityLabel.text = $"Durability: {tool.CurrentDurability} / {tool.MaxDurability}";
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
