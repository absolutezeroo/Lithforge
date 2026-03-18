using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    ///     ScriptableObject defining the layout of a container screen.
    ///     Contains one or more SlotGroupDefinitions that describe how slots are arranged.
    /// </summary>
    [CreateAssetMenu(menuName = "Lithforge/UI/Container Layout")]
    public sealed class ContainerLayout : ScriptableObject
    {
        [FormerlySerializedAs("_slotGroups"), SerializeField]
        private List<SlotGroupDefinition> slotGroups = new();
        [FormerlySerializedAs("_screenTitle"), SerializeField]
        private string screenTitle = "Container";

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
