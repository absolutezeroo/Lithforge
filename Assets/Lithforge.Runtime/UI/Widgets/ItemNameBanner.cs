using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI.Widgets
{
    /// <summary>
    /// Label shown above the hotbar when the selected slot changes.
    /// Fades out after a configurable duration.
    /// Uses USS class "lf-item-name-banner".
    /// </summary>
    public sealed class ItemNameBanner : Label
    {
        /// <summary>Duration in seconds before the banner fully fades out.</summary>
        private const float FadeDuration = 2.0f;

        /// <summary>Remaining time before the banner fades to zero opacity.</summary>
        private float _timer;

        /// <summary>Creates a new ItemNameBanner starting with zero opacity.</summary>
        public ItemNameBanner() : base("")
        {
            AddToClassList("lf-item-name-banner");
            pickingMode = PickingMode.Ignore;
            style.opacity = 0f;
        }

        /// <summary>
        /// Shows the banner with the given item name.
        /// </summary>
        public void ShowName(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
            {
                text = "";
                style.opacity = 0f;
                _timer = 0f;
                return;
            }

            text = FormatFullName(itemName);
            style.opacity = 1f;
            _timer = FadeDuration;
        }

        /// <summary>
        /// Call each frame to update the fade timer.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_timer <= 0f)
            {
                return;
            }

            _timer -= deltaTime;

            if (_timer <= 0f)
            {
                style.opacity = 0f;
            }
        }

        /// <summary>Converts an underscore-separated name to title case.</summary>
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
