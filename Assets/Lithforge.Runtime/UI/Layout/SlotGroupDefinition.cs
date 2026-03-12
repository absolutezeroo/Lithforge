using UnityEngine;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    /// Defines a group of slots within a container screen layout.
    /// Each group maps to a named ISlotContainer and specifies its grid dimensions.
    /// </summary>
    [System.Serializable]
    public sealed class SlotGroupDefinition
    {
        [SerializeField] private string containerName = "";
        [SerializeField] private int columns = 9;
        [SerializeField] private int rows = 1;
        [SerializeField] private int startIndex;
        [SerializeField] private int slotSize = 60;
        [SerializeField] private int slotSpacing = 3;
        [SerializeField] private string label = "";

        public string ContainerName
        {
            get { return containerName; }
        }

        public int Columns
        {
            get { return columns; }
        }

        public int Rows
        {
            get { return rows; }
        }

        public int StartIndex
        {
            get { return startIndex; }
        }

        public int SlotSize
        {
            get { return slotSize; }
        }

        public int SlotSpacing
        {
            get { return slotSpacing; }
        }

        public string Label
        {
            get { return label; }
        }

        public int TotalSlots
        {
            get { return columns * rows; }
        }

        /// <summary>
        /// Creates a SlotGroupDefinition programmatically (for code-driven layouts).
        /// </summary>
        internal static SlotGroupDefinition Create(
            string name, int cols, int rowCount,
            int start = 0, int size = 60, int spacing = 3, string groupLabel = "")
        {
            SlotGroupDefinition def = new SlotGroupDefinition();
            def.containerName = name;
            def.columns = cols;
            def.rows = rowCount;
            def.startIndex = start;
            def.slotSize = size;
            def.slotSpacing = spacing;
            def.label = groupLabel;
            return def;
        }
    }
}
