namespace Lithforge.Item.Interaction
{
    /// <summary>
    ///     Result of a slot action execution. Contains which slots changed
    ///     and whether the cursor changed, enabling delta network sync.
    /// </summary>
    public struct SlotActionResult
    {
        /// <summary>Maximum number of changed slot indices tracked inline.</summary>
        public const int MaxTrackedSlots = 8;

        /// <summary>Whether the action succeeded, failed validation, or detected a conservation violation.</summary>
        public SlotActionOutcome Outcome;

        /// <summary>Whether the cursor (held item) changed during this action.</summary>
        public bool CursorChanged;

        /// <summary>Number of valid entries in ChangedSlots.</summary>
        public int ChangedSlotCount;

        /// <summary>Indices of inventory slots that were modified. Only first ChangedSlotCount entries are valid.</summary>
        public int Slot0;

        /// <summary>Second changed slot index.</summary>
        public int Slot1;

        /// <summary>Third changed slot index.</summary>
        public int Slot2;

        /// <summary>Fourth changed slot index.</summary>
        public int Slot3;

        /// <summary>Fifth changed slot index.</summary>
        public int Slot4;

        /// <summary>Sixth changed slot index.</summary>
        public int Slot5;

        /// <summary>Seventh changed slot index.</summary>
        public int Slot6;

        /// <summary>Eighth changed slot index.</summary>
        public int Slot7;

        /// <summary>Returns the changed slot index at the given position.</summary>
        public int GetSlot(int index)
        {
            return index switch
            {
                0 => Slot0,
                1 => Slot1,
                2 => Slot2,
                3 => Slot3,
                4 => Slot4,
                5 => Slot5,
                6 => Slot6,
                7 => Slot7,
                _ => -1,
            };
        }

        /// <summary>Records a slot index as changed. Drops silently if capacity exceeded.</summary>
        public void AddChangedSlot(int slotIndex)
        {
            if (ChangedSlotCount >= MaxTrackedSlots)
            {
                return;
            }

            switch (ChangedSlotCount)
            {
                case 0:
                    Slot0 = slotIndex;
                    break;
                case 1:
                    Slot1 = slotIndex;
                    break;
                case 2:
                    Slot2 = slotIndex;
                    break;
                case 3:
                    Slot3 = slotIndex;
                    break;
                case 4:
                    Slot4 = slotIndex;
                    break;
                case 5:
                    Slot5 = slotIndex;
                    break;
                case 6:
                    Slot6 = slotIndex;
                    break;
                case 7:
                    Slot7 = slotIndex;
                    break;
            }

            ChangedSlotCount++;
        }

        /// <summary>Creates a success result with no changes.</summary>
        public static SlotActionResult NoOp()
        {
            return new SlotActionResult { Outcome = SlotActionOutcome.Success };
        }

        /// <summary>Creates a failed result with the given outcome.</summary>
        public static SlotActionResult Fail(SlotActionOutcome outcome)
        {
            return new SlotActionResult { Outcome = outcome };
        }
    }
}
