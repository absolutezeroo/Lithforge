using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Collects edge-triggered (wasPressedThisFrame) inputs each frame and exposes
    /// them for consumption by the fixed tick loop. Each latch bit is OR-accumulated
    /// across frames between ticks, then cleared when a tick consumes them.
    ///
    /// Call LatchFrame() every frame (before the tick loop).
    /// Call ConsumeTick() inside the tick loop to retrieve and clear latches.
    /// </summary>
    public sealed class PlayerInputLatch
    {
        // Accumulated latch bits (ORed across frames between ticks)
        private bool _jumpPressed;
        private bool _flyTogglePressed;
        private bool _noclipTogglePressed;
        private bool _rightClickPressed;
        private int _hotbarSlotPressed;
        private int _scrollDelta;

        private static readonly Key[] s_digitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        /// <summary>
        /// Called once per frame by GameLoop BEFORE the tick loop.
        /// Accumulates wasPressedThisFrame events into latch bits.
        /// </summary>
        public void LatchFrame()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    _jumpPressed = true;
                }

                if (keyboard.fKey.wasPressedThisFrame)
                {
                    _flyTogglePressed = true;
                }

                if (keyboard.nKey.wasPressedThisFrame)
                {
                    _noclipTogglePressed = true;
                }

                for (int i = 0; i < s_digitKeys.Length; i++)
                {
                    if (keyboard[s_digitKeys[i]].wasPressedThisFrame)
                    {
                        _hotbarSlotPressed = i + 1;
                    }
                }
            }

            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    _rightClickPressed = true;
                }

                float scroll = mouse.scroll.ReadValue().y;

                if (scroll > 0.1f)
                {
                    _scrollDelta++;
                }
                else if (scroll < -0.1f)
                {
                    _scrollDelta--;
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of accumulated latches and clears them.
        /// Call once at the start of each tick.
        /// </summary>
        public InputLatchSnapshot ConsumeTick()
        {
            InputLatchSnapshot snap = new InputLatchSnapshot
            {
                JumpPressed = _jumpPressed,
                FlyTogglePressed = _flyTogglePressed,
                NoclipTogglePressed = _noclipTogglePressed,
                RightClickPressed = _rightClickPressed,
                HotbarSlot = _hotbarSlotPressed,
                ScrollDelta = _scrollDelta,
            };

            _jumpPressed = false;
            _flyTogglePressed = false;
            _noclipTogglePressed = false;
            _rightClickPressed = false;
            _hotbarSlotPressed = 0;
            _scrollDelta = 0;

            return snap;
        }
    }
}
