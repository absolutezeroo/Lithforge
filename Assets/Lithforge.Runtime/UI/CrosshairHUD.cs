using System;

using Lithforge.Runtime.UI.Navigation;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    ///     Displays a simple crosshair at the center of the screen.
    ///     Uses UI Toolkit with code-driven VisualElement construction.
    /// </summary>
    public sealed class CrosshairHUD : MonoBehaviour, IScreen
    {
        private const int CrosshairSize = 20;
        private const int CrosshairThickness = 2;
        private const int CrosshairGap = 3;

        private UIDocument _document;

        public string ScreenName { get { return ScreenNames.Crosshair; } }
        public bool IsInputOpaque { get { return false; } }
        public bool RequiresCursor { get { return false; } }

        public void OnShow(ScreenShowArgs args)
        {
            SetVisible(true);
        }

        public void OnHide(Action onComplete)
        {
            SetVisible(false);
            onComplete();
        }

        public bool HandleEscape()
        {
            return false;
        }

        /// <summary>
        ///     Shows or hides the crosshair by toggling the root document visibility.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_document != null && _document.rootVisualElement != null)
            {
                _document.rootVisualElement.style.display =
                    visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 100;

            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            BuildCrosshair(root);
        }

        private void BuildCrosshair(VisualElement root)
        {
            // Container centered on screen
            VisualElement container = new()
            {
                name = "crosshair-container",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = new StyleLength(new Length(50, LengthUnit.Percent)),
                    top = new StyleLength(new Length(50, LengthUnit.Percent)),
                    width = 0,
                    height = 0,
                },
            };
            root.Add(container);

            // Top line
            VisualElement top = CreateLine();
            top.style.left = -CrosshairThickness / 2;
            top.style.top = -(CrosshairSize / 2 + CrosshairGap);
            top.style.width = CrosshairThickness;
            top.style.height = CrosshairSize / 2 - CrosshairGap;
            container.Add(top);

            // Bottom line
            VisualElement bottom = CreateLine();
            bottom.style.left = -CrosshairThickness / 2;
            bottom.style.top = CrosshairGap;
            bottom.style.width = CrosshairThickness;
            bottom.style.height = CrosshairSize / 2 - CrosshairGap;
            container.Add(bottom);

            // Left line
            VisualElement left = CreateLine();
            left.style.left = -(CrosshairSize / 2 + CrosshairGap);
            left.style.top = -CrosshairThickness / 2;
            left.style.width = CrosshairSize / 2 - CrosshairGap;
            left.style.height = CrosshairThickness;
            container.Add(left);

            // Right line
            VisualElement right = CreateLine();
            right.style.left = CrosshairGap;
            right.style.top = -CrosshairThickness / 2;
            right.style.width = CrosshairSize / 2 - CrosshairGap;
            right.style.height = CrosshairThickness;
            container.Add(right);
        }

        private VisualElement CreateLine()
        {
            VisualElement line = new()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute, backgroundColor = new Color(1f, 1f, 1f, 0.9f),
                },
            };

            return line;
        }
    }
}
