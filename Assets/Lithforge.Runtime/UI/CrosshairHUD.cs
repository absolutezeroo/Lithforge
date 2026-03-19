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
        /// <summary>Full width/height of the crosshair in pixels.</summary>
        private const int CrosshairSize = 20;

        /// <summary>Thickness of each crosshair line in pixels.</summary>
        private const int CrosshairThickness = 2;

        /// <summary>Gap between the center and each crosshair line in pixels.</summary>
        private const int CrosshairGap = 3;

        /// <summary>The UI Toolkit document hosting the crosshair visual elements.</summary>
        private UIDocument _document;

        /// <summary>Unique screen name identifier for the crosshair HUD.</summary>
        public string ScreenName
        {
            get
            {
                return ScreenNames.Crosshair;
            }
        }

        /// <summary>Returns false because the crosshair does not block input to elements beneath it.</summary>
        public bool IsInputOpaque
        {
            get
            {
                return false;
            }
        }

        /// <summary>Returns false because the crosshair does not require a visible mouse cursor.</summary>
        public bool RequiresCursor
        {
            get
            {
                return false;
            }
        }

        /// <summary>Shows the crosshair when the screen is displayed.</summary>
        public void OnShow(ScreenShowArgs args)
        {
            SetVisible(true);
        }

        /// <summary>Hides the crosshair and invokes the completion callback.</summary>
        public void OnHide(Action onComplete)
        {
            SetVisible(false);
            onComplete();
        }

        /// <summary>Returns false because the crosshair does not handle Escape key input.</summary>
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

        /// <summary>Creates the UIDocument and builds the crosshair visual elements.</summary>
        public void Initialize(PanelSettings panelSettings)
        {
            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = panelSettings;
            _document.sortingOrder = 100;

            VisualElement root = _document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;

            BuildCrosshair(root);
        }

        /// <summary>Builds the four crosshair lines (top, bottom, left, right) centered on screen.</summary>
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

        /// <summary>Creates a single white crosshair line as an absolutely-positioned VisualElement.</summary>
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
