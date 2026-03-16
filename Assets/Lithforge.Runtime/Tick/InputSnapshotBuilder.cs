using UnityEngine;
using UnityEngine.InputSystem;

namespace Lithforge.Runtime.Tick
{
    /// <summary>
    /// Samples all keyboard, mouse, and transform state each frame and produces a
    /// complete <see cref="InputSnapshot"/> per tick. Edge-triggered inputs are
    /// OR-accumulated across frames between ticks. Continuous inputs are sampled
    /// at tick time.
    ///
    /// Call <see cref="LatchFrame"/> once per frame before the tick loop.
    /// Call <see cref="ConsumeTick"/> inside the tick loop to retrieve and clear edges.
    /// </summary>
    public sealed class InputSnapshotBuilder
    {
        private readonly Transform _playerTransform;
        private readonly Transform _cameraTransform;

        // Edge-triggered accumulators (OR-latched across frames between ticks)
        private bool _jumpPressed;
        private bool _flyTogglePressed;
        private bool _noclipTogglePressed;
        private bool _primaryPressed;
        private bool _secondaryPressed;
        private int _scrollDelta;
        private sbyte _hotbarSlotPressed = -1;

        private static readonly Key[] s_digitKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9,
        };

        public InputSnapshotBuilder(Transform playerTransform, Transform cameraTransform)
        {
            _playerTransform = playerTransform;
            _cameraTransform = cameraTransform;
        }

        /// <summary>
        /// Called once per frame by GameLoop BEFORE the tick loop.
        /// OR-accumulates edge-triggered events into internal latches.
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

                // Hotbar digit keys — first match wins
                if (_hotbarSlotPressed < 0)
                {
                    for (int i = 0; i < s_digitKeys.Length; i++)
                    {
                        if (keyboard[s_digitKeys[i]].wasPressedThisFrame)
                        {
                            _hotbarSlotPressed = (sbyte)i;
                            break;
                        }
                    }
                }
            }

            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    _primaryPressed = true;
                }

                if (mouse.rightButton.wasPressedThisFrame)
                {
                    _secondaryPressed = true;
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
        /// Returns a complete snapshot of all inputs and clears edge-triggered latches.
        /// Continuous inputs (WASD, sprint, jump held, primary held) are sampled at
        /// call time and gated on cursor lock — if the cursor is unlocked, all
        /// continuous fields are false/zero. Edge-triggered inputs that were
        /// accumulated while the cursor was locked are still returned.
        /// Call once at the start of each tick.
        /// </summary>
        public InputSnapshot ConsumeTick()
        {
            bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            InputSnapshot snapshot = new InputSnapshot
            {
                // Edge-triggered (accumulated since last tick)
                JumpPressed = _jumpPressed,
                FlyTogglePressed = _flyTogglePressed,
                NoclipTogglePressed = _noclipTogglePressed,
                PrimaryPressed = _primaryPressed,
                SecondaryPressed = _secondaryPressed,
                ScrollDelta = _scrollDelta,
                HotbarSlotPressed = _hotbarSlotPressed,

                // Continuous (sampled now at tick time, gated on cursor lock)
                MoveForward = cursorLocked && keyboard != null && keyboard.wKey.isPressed,
                MoveBack = cursorLocked && keyboard != null && keyboard.sKey.isPressed,
                MoveLeft = cursorLocked && keyboard != null && keyboard.aKey.isPressed,
                MoveRight = cursorLocked && keyboard != null && keyboard.dKey.isPressed,
                Sprint = cursorLocked && keyboard != null && keyboard.leftShiftKey.isPressed,
                JumpHeld = cursorLocked && keyboard != null && keyboard.spaceKey.isPressed,
                PrimaryHeld = cursorLocked && mouse != null && mouse.leftButton.isPressed,

                // Look direction (sampled from transforms)
                Yaw = _playerTransform != null ? _playerTransform.eulerAngles.y : 0f,
                Pitch = _cameraTransform != null ? _cameraTransform.localEulerAngles.x : 0f,
            };

            // Normalize pitch to [-180, 180] range
            if (snapshot.Pitch > 180f)
            {
                snapshot.Pitch -= 360f;
            }

            // Clear edge-triggered accumulators
            _jumpPressed = false;
            _flyTogglePressed = false;
            _noclipTogglePressed = false;
            _primaryPressed = false;
            _secondaryPressed = false;
            _scrollDelta = 0;
            _hotbarSlotPressed = -1;

            return snapshot;
        }
    }
}
