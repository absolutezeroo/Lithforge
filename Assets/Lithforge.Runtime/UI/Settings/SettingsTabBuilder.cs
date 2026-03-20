using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Settings
{
    /// <summary>
    ///     Static factory methods for building settings screen UI rows.
    ///     Each method creates a styled row and appends it to the given parent.
    /// </summary>
    internal static class SettingsTabBuilder
    {
        /// <summary>Label color for setting row names.</summary>
        private static readonly Color s_labelColor = new(0.85f, 0.85f, 0.85f, 1f);

        /// <summary>Color for section header labels.</summary>
        private static readonly Color s_headerColor = new(0.8f, 0.8f, 0.85f, 1f);

        /// <summary>Adds a bold section header label to the parent.</summary>
        public static Label AddSectionHeader(VisualElement parent, string text)
        {
            Label header = new(text)
            {
                style =
                {
                    fontSize = 20,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = s_headerColor,
                    marginTop = 16,
                    marginBottom = 8,
                },
            };
            header.AddToClassList("settings-section-header");
            parent.Add(header);

            return header;
        }

        /// <summary>Adds a float slider row with label and live value display.</summary>
        public static Slider AddSliderFloat(VisualElement parent, string label, float initialValue,
            float min, float max, Action<float> onChange)
        {
            VisualElement row = CreateRow();
            row.Add(CreateNameLabel(label));

            Slider slider = new(min, max)
            {
                value = initialValue,
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(slider);

            Label valueLabel = CreateValueLabel(initialValue.ToString("F2"));
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString("F2");
                onChange(evt.newValue);
            });

            parent.Add(row);

            return slider;
        }

        /// <summary>Adds an integer slider row with label and live value display.</summary>
        public static SliderInt AddSliderInt(VisualElement parent, string label, int initialValue,
            int min, int max, Action<int> onChange)
        {
            VisualElement row = CreateRow();
            row.Add(CreateNameLabel(label));

            SliderInt slider = new(min, max)
            {
                value = initialValue,
                style =
                {
                    flexGrow = 1,
                },
            };
            row.Add(slider);

            Label valueLabel = CreateValueLabel(initialValue.ToString());
            row.Add(valueLabel);

            slider.RegisterValueChangedCallback(evt =>
            {
                valueLabel.text = evt.newValue.ToString();
                onChange(evt.newValue);
            });

            parent.Add(row);

            return slider;
        }

        /// <summary>Adds a dropdown row with label and index-based callback.</summary>
        public static DropdownField AddDropdown(VisualElement parent, string label,
            string[] choices, int initialIndex, Action<int> onChange)
        {
            VisualElement row = CreateRow();
            row.Add(CreateNameLabel(label));

            System.Collections.Generic.List<string> choiceList = new(choices);

            DropdownField dropdown = new(choiceList, initialIndex)
            {
                style =
                {
                    flexGrow = 1,
                },
            };

            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = choiceList.IndexOf(evt.newValue);

                if (index >= 0)
                {
                    onChange(index);
                }
            });

            row.Add(dropdown);
            parent.Add(row);

            return dropdown;
        }

        /// <summary>Adds a toggle row with label and boolean callback.</summary>
        public static Toggle AddToggle(VisualElement parent, string label,
            bool initialValue, Action<bool> onChange)
        {
            VisualElement row = CreateRow();
            row.Add(CreateNameLabel(label));

            Toggle toggle = new()
            {
                value = initialValue,
                style =
                {
                    flexGrow = 1,
                },
            };

            toggle.RegisterValueChangedCallback(evt =>
            {
                onChange(evt.newValue);
            });

            row.Add(toggle);
            parent.Add(row);

            return toggle;
        }

        /// <summary>
        ///     Adds a keybind row with action name, current key button, and rebind callback.
        ///     When the button is clicked, onRebindRequested is called with the action name
        ///     and a callback to invoke with the new key.
        /// </summary>
        public static Button AddKeybindRow(VisualElement parent, string actionName,
            Key currentKey, Action<string, Action<Key>> onRebindRequested)
        {
            VisualElement row = CreateRow();
            row.AddToClassList("settings-row");

            Label actionLabel = CreateNameLabel(actionName);
            row.Add(actionLabel);

            Button keyButton = new()
            {
                text = "[" + FormatKeyName(currentKey) + "]",
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.25f, 0.25f, 0.3f, 1f),
                    color = Color.white,
                    fontSize = 14,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    unityTextAlign = TextAnchor.MiddleCenter,
                },
            };

            keyButton.clicked += () =>
            {
                onRebindRequested(actionName, newKey =>
                {
                    keyButton.text = "[" + FormatKeyName(newKey) + "]";
                });
            };

            row.Add(keyButton);
            parent.Add(row);

            return keyButton;
        }

        /// <summary>Formats a Key enum value into a human-readable display string.</summary>
        public static string FormatKeyName(Key key)
        {
            return key switch
            {
                Key.LeftShift => "Left Shift",
                Key.RightShift => "Right Shift",
                Key.LeftCtrl => "Left Ctrl",
                Key.RightCtrl => "Right Ctrl",
                Key.LeftAlt => "Left Alt",
                Key.RightAlt => "Right Alt",
                Key.Space => "Space",
                _ => key.ToString(),
            };
        }

        /// <summary>Creates a standard settings row container.</summary>
        private static VisualElement CreateRow()
        {
            VisualElement row = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6,
                    height = 30,
                },
            };
            row.AddToClassList("settings-row");

            return row;
        }

        /// <summary>Creates a left-column name label for a settings row.</summary>
        private static Label CreateNameLabel(string text)
        {
            Label label = new(text)
            {
                style =
                {
                    width = new Length(40, LengthUnit.Percent),
                    color = s_labelColor,
                    fontSize = 14,
                },
            };
            label.AddToClassList("settings-label");

            return label;
        }

        /// <summary>Creates a right-aligned value display label for slider rows.</summary>
        private static Label CreateValueLabel(string text)
        {
            return new Label(text)
            {
                style =
                {
                    width = 60,
                    color = Color.white,
                    fontSize = 14,
                    unityTextAlign = TextAnchor.MiddleRight,
                },
            };
        }
    }
}
