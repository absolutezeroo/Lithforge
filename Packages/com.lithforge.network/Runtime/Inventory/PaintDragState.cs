using System.Collections.Generic;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Server-side accumulator for a paint-drag gesture.
    ///     Paint-drag is a multi-message interaction (Begin/Slot/End).
    ///     The server accumulates slot indices during the drag and commits
    ///     atomically on End to prevent partial-drag corruption.
    ///     Cleared on End sub-message, container close, or disconnect.
    /// </summary>
    internal sealed class PaintDragState
    {
        /// <summary>Slots that have been painted during this drag gesture.</summary>
        public readonly HashSet<int> PaintedSlots = new();

        /// <summary>True when the drag has been initialized by a Begin sub-message.</summary>
        public bool IsActive;

        /// <summary>Resets the drag state for a new gesture or on cancellation.</summary>
        public void Reset()
        {
            IsActive = false;
            PaintedSlots.Clear();
        }
    }
}
