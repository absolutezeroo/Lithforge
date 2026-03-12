using System.Collections.Generic;
using UnityEngine;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    /// ScriptableObject defining the layout of a container screen.
    /// Contains one or more SlotGroupDefinitions that describe how slots are arranged.
    /// </summary>
    [CreateAssetMenu(menuName = "Lithforge/UI/Container Layout")]
    public sealed class ContainerLayoutSO : ScriptableObject
    {
        [SerializeField] private List<SlotGroupDefinition> slotGroups = new List<SlotGroupDefinition>();
        [SerializeField] private string screenTitle = "Container";

        public IReadOnlyList<SlotGroupDefinition> SlotGroups
        {
            get { return slotGroups; }
        }

        public string ScreenTitle
        {
            get { return screenTitle; }
        }
    }
}
