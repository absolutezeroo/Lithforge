using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    /// Defines a group of slots within a container screen layout.
    /// Each group maps to a named ISlotContainer and specifies its grid dimensions.
    /// </summary>
    [System.Serializable]
    public sealed class SlotGroupDefinition
    {
        [FormerlySerializedAs("containerName")]
        [SerializeField] private string _containerName = "";
        [FormerlySerializedAs("columns")]
        [SerializeField] private int _columns = 9;
        [FormerlySerializedAs("rows")]
        [SerializeField] private int _rows = 1;
        [FormerlySerializedAs("startIndex")]
        [SerializeField] private int _startIndex;
        [FormerlySerializedAs("slotSize")]
        [SerializeField] private int _slotSize = 60;
        [FormerlySerializedAs("slotSpacing")]
        [SerializeField] private int _slotSpacing = 3;
        [FormerlySerializedAs("label")]
        [SerializeField] private string _label = "";

        public string ContainerName
        {
            get { return _containerName; }
        }

        public int Columns
        {
            get { return _columns; }
        }

        public int Rows
        {
            get { return _rows; }
        }

        public int StartIndex
        {
            get { return _startIndex; }
        }

        public int SlotSize
        {
            get { return _slotSize; }
        }

        public int SlotSpacing
        {
            get { return _slotSpacing; }
        }

        public string Label
        {
            get { return _label; }
        }

        public int TotalSlots
        {
            get { return _columns * _rows; }
        }

        /// <summary>
        /// Creates a SlotGroupDefinition programmatically (for code-driven layouts).
        /// </summary>
        internal static SlotGroupDefinition Create(
            string name, int cols, int rowCount,
            int start = 0, int size = 60, int spacing = 3, string groupLabel = "")
        {
            SlotGroupDefinition def = new SlotGroupDefinition();
            def._containerName = name;
            def._columns = cols;
            def._rows = rowCount;
            def._startIndex = start;
            def._slotSize = size;
            def._slotSpacing = spacing;
            def._label = groupLabel;
            return def;
        }
    }
}
