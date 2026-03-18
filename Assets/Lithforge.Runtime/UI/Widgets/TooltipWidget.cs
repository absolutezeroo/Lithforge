using System.Collections.Generic;
using System.Text;

using Lithforge.Core.Data;
using Lithforge.Voxel.Item;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Widgets
{
    /// <summary>
    ///     Absolute-positioned tooltip with 3 display modes for tools:
    ///     Default (durability + traits + hints), Shift (detailed stats),
    ///     Ctrl (per-part material breakdown). Uses USS class "lf-tooltip".
    /// </summary>
    public sealed class TooltipWidget : VisualElement
    {
        private readonly Label _durabilityLabel;
        private readonly Label _hintLabel;
        private readonly Label _materialBreakdownLabel;
        private readonly Label _nameLabel;
        private readonly Label _statsLabel;
        private readonly Label _traitsLabel;
        private readonly Label _typeLabel;

        // Reusable collections to avoid per-frame allocation
        private readonly HashSet<string> _seenTraits = new();
        private readonly StringBuilder _sb = new(256);

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

            _traitsLabel = new Label("");
            _traitsLabel.AddToClassList("lf-tooltip__stat");
            _traitsLabel.pickingMode = PickingMode.Ignore;
            Add(_traitsLabel);

            _materialBreakdownLabel = new Label("");
            _materialBreakdownLabel.AddToClassList("lf-tooltip__stat");
            _materialBreakdownLabel.pickingMode = PickingMode.Ignore;
            Add(_materialBreakdownLabel);

            _hintLabel = new Label("");
            _hintLabel.AddToClassList("lf-tooltip__hint");
            _hintLabel.pickingMode = PickingMode.Ignore;
            Add(_hintLabel);
        }

        /// <summary>
        ///     Shows the tooltip with data from the given item stack.
        ///     Displays in one of three modes depending on modifier keys held.
        /// </summary>
        public void Show(
            ItemStack stack,
            ItemEntry entry,
            float posX,
            float posY,
            bool isShiftHeld,
            bool isCtrlHeld,
            ToolMaterialRegistry materialRegistry)
        {
            if (stack.IsEmpty)
            {
                Hide();
                return;
            }

            style.display = DisplayStyle.Flex;
            style.left = posX + 16;
            style.top = posY - 8;

            // Resolve tool/part data from Components
            ToolInstance tool = null;
            ToolPartData partData = default;
            bool isToolPart = false;

            if (stack.HasComponents)
            {
                ToolInstanceComponent toolComp = stack.Components.Get<ToolInstanceComponent>(
                    DataComponentTypes.ToolInstanceId);

                if (toolComp != null)
                {
                    tool = toolComp.Tool;
                }
                else
                {
                    ToolPartDataComponent partComp = stack.Components.Get<ToolPartDataComponent>(
                        DataComponentTypes.ToolPartDataId);

                    if (partComp != null)
                    {
                        partData = partComp.PartData;
                        isToolPart = true;
                    }
                }
            }

            // ---------- NAME ----------
            if (tool != null)
            {
                string matName = "";
                if (tool.Parts != null && tool.Parts.Length > 0)
                {
                    matName = FormatFullName(tool.Parts[0].MaterialId.Name) + " ";
                }

                _nameLabel.text = matName + tool.ToolType.ToString();
            }
            else if (isToolPart)
            {
                string matName = FormatFullName(partData.MaterialId.Name);
                string partName = partData.PartType == ToolPartType.RepairKit
                    ? "Repair Kit"
                    : partData.PartType.ToString();
                _nameLabel.text = matName + " " + partName;
            }
            else
            {
                _nameLabel.text = FormatFullName(stack.ItemId.Name);
            }

            // ---------- TYPE ----------
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
            else if (entry != null && entry.IsBlockItem)
            {
                _typeLabel.text = "Block";
                _typeLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _typeLabel.style.display = DisplayStyle.None;
            }

            // ---------- TOOL: 3-mode display ----------
            if (tool != null)
            {
                if (isCtrlHeld && materialRegistry != null)
                {
                    ShowCtrlMode(tool, materialRegistry);
                }
                else if (isShiftHeld)
                {
                    ShowShiftMode(tool);
                }
                else
                {
                    ShowDefaultMode(tool);
                }
            }
            // ---------- PART: show material stats ----------
            else if (isToolPart)
            {
                ShowPartTooltip(partData, materialRegistry);
            }
            // ---------- NORMAL ITEM ----------
            else
            {
                _durabilityLabel.style.display = DisplayStyle.None;
                _traitsLabel.style.display = DisplayStyle.None;
                _materialBreakdownLabel.style.display = DisplayStyle.None;

                if (stack.Count > 1)
                {
                    _statsLabel.text = "Count: " + stack.Count;
                    _statsLabel.style.display = DisplayStyle.Flex;
                }
                else
                {
                    _statsLabel.style.display = DisplayStyle.None;
                }

                _hintLabel.style.display = DisplayStyle.None;
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

        private void ShowDefaultMode(ToolInstance tool)
        {
            // Durability (colored by ratio)
            if (tool.IsBroken)
            {
                _durabilityLabel.text = "BROKEN";
                _durabilityLabel.style.color = ColorFromHex("#C80000");
                _durabilityLabel.style.display = DisplayStyle.Flex;
            }
            else if (tool.MaxDurability > 0)
            {
                float ratio = (float)tool.CurrentDurability / tool.MaxDurability;
                string color = ratio > 0.5f ? "#00C800" : ratio > 0.25f ? "#C8C800" : "#C80000";
                _durabilityLabel.text = "Durability: " + tool.CurrentDurability + " / " + tool.MaxDurability;
                _durabilityLabel.style.color = ColorFromHex(color);
                _durabilityLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _durabilityLabel.style.display = DisplayStyle.None;
            }

            // Traits list
            _sb.Clear();
            _seenTraits.Clear();

            if (tool.Parts != null)
            {
                for (int p = 0; p < tool.Parts.Length; p++)
                {
                    ResourceId[] traitIds = tool.Parts[p].TraitIds;

                    if (traitIds == null)
                    {
                        continue;
                    }

                    for (int t = 0; t < traitIds.Length; t++)
                    {
                        string id = traitIds[t].Name;

                        if (_seenTraits.Add(id))
                        {
                            if (_sb.Length > 0)
                            {
                                _sb.Append('\n');
                            }

                            _sb.Append(FormatFullName(id));
                        }
                    }
                }
            }

            // Modifiers
            if (tool.Slots != null)
            {
                for (int i = 0; i < tool.Slots.Length; i++)
                {
                    if (tool.Slots[i].IsOccupied)
                    {
                        if (_sb.Length > 0)
                        {
                            _sb.Append('\n');
                        }

                        _sb.Append(FormatFullName(tool.Slots[i].ModifierId.Name));

                        if (tool.Slots[i].Level > 1)
                        {
                            _sb.Append(' ');
                            _sb.Append(ToRoman(tool.Slots[i].Level));
                        }
                    }
                }
            }

            if (_sb.Length > 0)
            {
                _traitsLabel.text = _sb.ToString();
                _traitsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _traitsLabel.style.display = DisplayStyle.None;
            }

            _statsLabel.style.display = DisplayStyle.None;
            _materialBreakdownLabel.style.display = DisplayStyle.None;

            if (tool.IsBroken)
            {
                _hintLabel.text = "Repair at Tool Station or use Repair Kit";
            }
            else
            {
                _hintLabel.text = "Hold [Shift] for Stats\nHold [Ctrl] for Materials";
            }

            _hintLabel.style.display = DisplayStyle.Flex;
        }

        private void ShowShiftMode(ToolInstance tool)
        {
            _sb.Clear();

            if (tool.IsBroken)
            {
                _sb.Append("Durability: BROKEN\n");
            }
            else if (tool.MaxDurability > 0)
            {
                _sb.Append("Durability: ").Append(tool.CurrentDurability)
                    .Append(" / ").Append(tool.MaxDurability).Append('\n');
            }

            if (tool.BaseDamage > 0)
            {
                _sb.Append("Attack Damage: ").Append(tool.BaseDamage.ToString("F1")).Append('\n');
            }

            if (tool.BaseSpeed > 0)
            {
                _sb.Append("Mining Speed: ").Append(tool.BaseSpeed.ToString("F1")).Append('\n');
            }

            if (tool.EffectiveToolLevel > 0)
            {
                _sb.Append("Mining Level: ").Append(FormatToolLevel(tool.EffectiveToolLevel)).Append('\n');
            }

            int free = tool.GetAvailableSlots();
            int total = tool.Slots != null ? tool.Slots.Length : 0;

            if (total > 0)
            {
                _sb.Append("Modifier Slots: ").Append(free).Append(" / ").Append(total).Append('\n');
            }

            // Active modifier effects
            if (tool.Slots != null)
            {
                for (int i = 0; i < tool.Slots.Length; i++)
                {
                    if (tool.Slots[i].IsOccupied)
                    {
                        _sb.Append("  ").Append(FormatFullName(tool.Slots[i].ModifierId.Name));

                        if (tool.Slots[i].Level > 1)
                        {
                            _sb.Append(' ').Append(ToRoman(tool.Slots[i].Level));
                        }

                        _sb.Append('\n');
                    }
                }
            }

            if (_sb.Length > 0)
            {
                // Remove trailing newline
                if (_sb[_sb.Length - 1] == '\n')
                {
                    _sb.Length--;
                }

                _statsLabel.text = _sb.ToString();
                _statsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _statsLabel.style.display = DisplayStyle.None;
            }

            _durabilityLabel.style.display = DisplayStyle.None;
            _traitsLabel.style.display = DisplayStyle.None;
            _materialBreakdownLabel.style.display = DisplayStyle.None;
            _hintLabel.style.display = DisplayStyle.None;
        }

        private void ShowCtrlMode(ToolInstance tool, ToolMaterialRegistry materialRegistry)
        {
            _sb.Clear();

            if (tool.Parts != null)
            {
                for (int p = 0; p < tool.Parts.Length; p++)
                {
                    ToolPart part = tool.Parts[p];
                    string partName = part.PartType.ToString();
                    string matName = FormatFullName(part.MaterialId.Name);

                    if (p > 0)
                    {
                        _sb.Append('\n');
                    }

                    _sb.Append('[').Append(matName).Append(' ').Append(partName).Append("]\n");

                    ToolMaterialData mat = materialRegistry.Get(part.MaterialId);

                    if (mat == null)
                    {
                        _sb.Append("  Unknown material\n");
                        continue;
                    }

                    AppendPartMaterialStats(part.PartType, mat);
                    AppendTraitLines(mat.TraitIds, "  ");
                }
            }

            if (_sb.Length > 0)
            {
                // Remove trailing newline
                if (_sb[_sb.Length - 1] == '\n')
                {
                    _sb.Length--;
                }

                _materialBreakdownLabel.text = _sb.ToString();
                _materialBreakdownLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _materialBreakdownLabel.style.display = DisplayStyle.None;
            }

            _statsLabel.style.display = DisplayStyle.None;
            _durabilityLabel.style.display = DisplayStyle.None;
            _traitsLabel.style.display = DisplayStyle.None;
            _hintLabel.style.display = DisplayStyle.None;
        }

        private void ShowPartTooltip(ToolPartData partData, ToolMaterialRegistry materialRegistry)
        {
            _traitsLabel.style.display = DisplayStyle.None;
            _materialBreakdownLabel.style.display = DisplayStyle.None;
            _durabilityLabel.style.display = DisplayStyle.None;

            if (materialRegistry == null)
            {
                _statsLabel.style.display = DisplayStyle.None;
                _hintLabel.style.display = DisplayStyle.None;
                return;
            }

            ToolMaterialData mat = materialRegistry.Get(partData.MaterialId);

            if (mat == null)
            {
                _statsLabel.style.display = DisplayStyle.None;
                _hintLabel.style.display = DisplayStyle.None;
                return;
            }

            _sb.Clear();

            if (partData.PartType == ToolPartType.RepairKit)
            {
                _sb.Append("Repairs: ").Append(FormatFullName(partData.MaterialId.Name))
                    .Append(" tools\n");
            }

            AppendPartMaterialStats(partData.PartType, mat);
            AppendTraitLines(mat.TraitIds, "");

            if (_sb.Length > 0)
            {
                // Remove trailing newline
                if (_sb[_sb.Length - 1] == '\n')
                {
                    _sb.Length--;
                }

                _statsLabel.text = _sb.ToString();
                _statsLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _statsLabel.style.display = DisplayStyle.None;
            }

            if (partData.PartType == ToolPartType.RepairKit)
            {
                _hintLabel.text = "Right-click on tool to use";
                _hintLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _hintLabel.style.display = DisplayStyle.None;
            }
        }

        private void AppendPartMaterialStats(ToolPartType partType, ToolMaterialData mat)
        {
            switch (partType)
            {
                case ToolPartType.Head:
                case ToolPartType.Blade:
                case ToolPartType.Point:
                    _sb.Append("  Durability: ").Append(mat.HeadDurability).Append('\n');
                    _sb.Append("  Mining Speed: ").Append(mat.HeadMiningSpeed.ToString("F1")).Append('\n');
                    _sb.Append("  Attack Damage: ").Append(mat.HeadAttackDamage.ToString("F1")).Append('\n');
                    _sb.Append("  Mining Level: ").Append(FormatToolLevel(mat.ToolLevel)).Append('\n');
                    break;

                case ToolPartType.Handle:
                case ToolPartType.Shaft:
                case ToolPartType.Grip:
                case ToolPartType.Stock:
                    _sb.Append("  Durability: x").Append(mat.HandleDurabilityMultiplier.ToString("F2")).Append('\n');
                    _sb.Append("  Mining Speed: x").Append(mat.HandleSpeedMultiplier.ToString("F2")).Append('\n');
                    break;

                case ToolPartType.Binding:
                case ToolPartType.Guard:
                    _sb.Append("  Durability Bonus: +").Append(mat.BindingDurabilityBonus).Append('\n');
                    break;

                case ToolPartType.RepairKit:
                {
                    int repair = (int)(mat.HeadDurability *
                        RepairKitHelper.RepairKitValue / RepairKitHelper.UnitsPerRepair);
                    _sb.Append("  Restores: ").Append(repair).Append(" durability\n");
                    break;
                }

                default:
                    // Generic fallback for uncovered part types
                    _sb.Append("  Durability: ").Append(mat.HeadDurability).Append('\n');

                    if (mat.HeadMiningSpeed > 0)
                    {
                        _sb.Append("  Mining Speed: ").Append(mat.HeadMiningSpeed.ToString("F1")).Append('\n');
                    }

                    if (mat.HeadAttackDamage > 0)
                    {
                        _sb.Append("  Attack Damage: ").Append(mat.HeadAttackDamage.ToString("F1")).Append('\n');
                    }

                    break;
            }
        }

        private void AppendTraitLines(string[] traitIds, string indent)
        {
            if (traitIds == null || traitIds.Length == 0)
            {
                return;
            }

            for (int t = 0; t < traitIds.Length; t++)
            {
                string traitName = traitIds[t];

                if (ResourceId.TryParse(traitName, out ResourceId rid))
                {
                    traitName = rid.Name;
                }

                _sb.Append(indent).Append("Trait: ").Append(FormatFullName(traitName)).Append('\n');
            }
        }

        private static string FormatToolLevel(int level)
        {
            string[] tierNames = { "Wood", "Stone", "Iron", "Diamond", "Netherite" };

            if (level >= 0 && level < tierNames.Length)
            {
                return tierNames[level];
            }

            return "Level " + level;
        }

        private static string ToRoman(int number)
        {
            if (number <= 0 || number > 10)
            {
                return number.ToString();
            }

            string[] roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return roman[number - 1];
        }

        private static Color ColorFromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                return c;
            }

            return Color.white;
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
