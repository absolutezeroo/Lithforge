using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace Lithforge.Runtime.UI.Layout
{
    /// <summary>
    ///     Defines a group of slots within a container screen layout.
    ///     Each group maps to a named ISlotContainer and specifies its grid dimensions.
    /// </summary>
    [Serializable]
    public sealed class SlotGroupDefinition
    {
        /// <summary>Name of the container this group belongs to, used for UI binding.</summary>
        [FormerlySerializedAs("_containerName"), SerializeField]
        private string containerName = "";

        /// <summary>Number of columns in the slot grid.</summary>
        [FormerlySerializedAs("_columns"), SerializeField]
        private int columns = 9;

        /// <summary>Number of rows in the slot grid.</summary>
        [FormerlySerializedAs("_rows"), SerializeField]
        private int rows = 1;

        /// <summary>Index of the first slot in the backing container.</summary>
        [FormerlySerializedAs("_startIndex"), SerializeField]
        private int startIndex;

        /// <summary>Size of each slot in pixels.</summary>
        [FormerlySerializedAs("_slotSize"), SerializeField]
        private int slotSize = 60;

        /// <summary>Spacing between slots in pixels.</summary>
        [FormerlySerializedAs("_slotSpacing"), SerializeField]
        private int slotSpacing = 3;

        /// <summary>Optional display label shown above the slot group.</summary>
        [FormerlySerializedAs("_label"), SerializeField]
        private string label = "";

        /// <summary>Name of the container this group belongs to.</summary>
        public string ContainerName
        {
            get { return containerName; }
        }

        /// <summary>Number of columns in the slot grid.</summary>
        public int Columns
        {
            get { return columns; }
        }

        /// <summary>Number of rows in the slot grid.</summary>
        public int Rows
        {
            get { return rows; }
        }

        /// <summary>Index of the first slot in the backing container.</summary>
        public int StartIndex
        {
            get { return startIndex; }
        }

        /// <summary>Size of each slot in pixels.</summary>
        public int SlotSize
        {
            get { return slotSize; }
        }

        /// <summary>Spacing between slots in pixels.</summary>
        public int SlotSpacing
        {
            get { return slotSpacing; }
        }

        /// <summary>Optional display label shown above the slot group.</summary>
        public string Label
        {
            get { return label; }
        }

        /// <summary>Total number of slots in this group (columns times rows).</summary>
        public int TotalSlots
        {
            get { return columns * rows; }
        }

        /// <summary>
        ///     Creates a SlotGroupDefinition programmatically (for code-driven layouts).
        /// </summary>
        internal static SlotGroupDefinition Create(
            string name, int cols, int rowCount,
            int start = 0, int size = 60, int spacing = 3, string groupLabel = "")
        {
            SlotGroupDefinition def = new()
            {
                containerName = name,
                columns = cols,
                rows = rowCount,
                startIndex = start,
                slotSize = size,
                slotSpacing = spacing,
                label = groupLabel,
            };
            return def;
        }
    }
}
