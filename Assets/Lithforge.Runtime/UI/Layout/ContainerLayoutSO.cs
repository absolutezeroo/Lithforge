using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    /// ScriptableObject defining the layout of a container screen.
    /// Contains one or more SlotGroupDefinitions that describe how slots are arranged.
    /// </summary>
    [CreateAssetMenu(menuName = "Lithforge/UI/Container Layout")]
    public sealed class ContainerLayoutSo : ScriptableObject
    {
        [FormerlySerializedAs("slotGroups")]
        [SerializeField] private List<SlotGroupDefinition> _slotGroups = new List<SlotGroupDefinition>();
        [FormerlySerializedAs("screenTitle")]
        [SerializeField] private string _screenTitle = "Container";

        public IReadOnlyList<SlotGroupDefinition> SlotGroups
        {
            get { return _slotGroups; }
        }

        public string ScreenTitle
        {
            get { return _screenTitle; }
        }
    }
}
