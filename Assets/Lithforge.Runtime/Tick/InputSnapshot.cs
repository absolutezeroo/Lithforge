namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Complete per-tick capture of all player inputs. Replaces live
    /// Keyboard.current / Mouse.current reads in PlayerPhysicsBody and BlockInteraction.
    /// Produced once per tick by <see cref="InputSnapshotBuilder"/> and consumed
    /// by all tick-rate simulation systems.
    /// Value type — no heap allocation.
    /// </summary>
    public struct InputSnapshot
    {
        // --- Continuous movement (replaces Keyboard.current.xKey.isPressed) ---

        /// <summary>W key held (forward movement).</summary>
        public bool MoveForward;

        /// <summary>S key held (backward movement).</summary>
        public bool MoveBack;

        /// <summary>A key held (left strafe).</summary>
        public bool MoveLeft;

        /// <summary>D key held (right strafe).</summary>
        public bool MoveRight;

        /// <summary>LeftShift held (sprint when walking, fly-down when flying).</summary>
        public bool Sprint;

        /// <summary>Space key held (fly-up when flying).</summary>
        public bool JumpHeld;

        // --- Edge-triggered movement ---

        /// <summary>Space wasPressedThisFrame (jump when walking).</summary>
        public bool JumpPressed;

        /// <summary>F wasPressedThisFrame (toggle fly mode).</summary>
        public bool FlyTogglePressed;

        /// <summary>N wasPressedThisFrame (toggle noclip).</summary>
        public bool NoclipTogglePressed;

        // --- Look direction (replaces Transform.forward read) ---

        /// <summary>Player yaw in degrees (from player transform Y rotation).</summary>
        public float Yaw;

        /// <summary>Camera pitch in degrees (from camera local X rotation).</summary>
        public float Pitch;

        // --- Interaction (replaces Mouse.current reads) ---

        /// <summary>Left mouse button isPressed (mining hold).</summary>
        public bool PrimaryHeld;

        /// <summary>Left mouse button wasPressedThisFrame (mining start).</summary>
        public bool PrimaryPressed;

        /// <summary>Right mouse button wasPressedThisFrame (placement / interact).</summary>
        public bool SecondaryPressed;

        // --- Scroll and hotbar ---

        /// <summary>Net scroll clicks this tick (positive = up, negative = down).</summary>
        public int ScrollDelta;

        /// <summary>
        /// Digit key pressed this tick for hotbar slot selection.
        /// 0-8 = digit key, -1 = none.
        /// </summary>
        public sbyte HotbarSlotPressed;
    }
}
