using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.UI
{
    /// <summary>
    /// Displays a simple crosshair at the center of the screen.
    /// Uses UI Toolkit with code-driven VisualElement construction.
    /// </summary>
    public sealed class CrosshairHUD : MonoBehaviour
    {
        private const int _crosshairSize = 20;
        private const int _crosshairThickness = 2;
        private const int _crosshairGap = 3;

        private UIDocument _document;

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
            VisualElement container = new VisualElement();
            container.name = "crosshair-container";
            container.pickingMode = PickingMode.Ignore;
            container.style.position = Position.Absolute;
            container.style.left = new StyleLength(new Length(50, LengthUnit.Percent));
            container.style.top = new StyleLength(new Length(50, LengthUnit.Percent));
            container.style.width = 0;
            container.style.height = 0;
            root.Add(container);

            // Top line
            VisualElement top = CreateLine();
            top.style.left = -_crosshairThickness / 2;
            top.style.top = -(_crosshairSize / 2 + _crosshairGap);
            top.style.width = _crosshairThickness;
            top.style.height = _crosshairSize / 2 - _crosshairGap;
            container.Add(top);

            // Bottom line
            VisualElement bottom = CreateLine();
            bottom.style.left = -_crosshairThickness / 2;
            bottom.style.top = _crosshairGap;
            bottom.style.width = _crosshairThickness;
            bottom.style.height = _crosshairSize / 2 - _crosshairGap;
            container.Add(bottom);

            // Left line
            VisualElement left = CreateLine();
            left.style.left = -(_crosshairSize / 2 + _crosshairGap);
            left.style.top = -_crosshairThickness / 2;
            left.style.width = _crosshairSize / 2 - _crosshairGap;
            left.style.height = _crosshairThickness;
            container.Add(left);

            // Right line
            VisualElement right = CreateLine();
            right.style.left = _crosshairGap;
            right.style.top = -_crosshairThickness / 2;
            right.style.width = _crosshairSize / 2 - _crosshairGap;
            right.style.height = _crosshairThickness;
            container.Add(right);
        }

        private VisualElement CreateLine()
        {
            VisualElement line = new VisualElement();
            line.pickingMode = PickingMode.Ignore;
            line.style.position = Position.Absolute;
            line.style.backgroundColor = new Color(1f, 1f, 1f, 0.9f);

            return line;
        }

    }
}
