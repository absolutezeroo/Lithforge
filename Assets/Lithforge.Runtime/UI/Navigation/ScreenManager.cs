using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;

using Cursor = UnityEngine.Cursor;
using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.UI.Navigation
{
    /// <summary>
    ///     Manages a push/pop stack of <see cref="IScreen" /> instances. Owns cursor
    ///     state centrally so individual screens never touch <c>Cursor.lockState</c>.
    ///     Handles Escape key dispatch to the topmost opaque screen.
    /// </summary>
    /// <remarks>
    ///     Lives on the same root GameObject as LithforgeBootstrap and persists for
    ///     the entire application lifetime. Screens are registered by name and can
    ///     be pushed/popped by name or by direct reference.
    /// </remarks>
    public sealed class ScreenManager : MonoBehaviour
    {
        /// <summary>Lookup of registered screens by name for push-by-name operations.</summary>
        private readonly Dictionary<string, IScreen> _registry = new();

        /// <summary>Ordered stack of active screens from bottom (index 0) to top.</summary>
        private readonly List<IScreen> _stack = new();

        /// <summary>Logger for screen manager diagnostics.</summary>
        private ILogger _logger;

        /// <summary>True while a hide transition is in progress to prevent re-entrant stack mutations.</summary>
        private bool _transitioning;

        /// <summary>The topmost screen in the stack, or null if empty.</summary>
        public IScreen Top
        {
            get { return _stack.Count > 0 ? _stack[_stack.Count - 1] : null; }
        }

        /// <summary>Number of screens currently in the stack.</summary>
        public int Count
        {
            get { return _stack.Count; }
        }

        /// <summary>
        ///     Callback invoked when Escape is pressed but the stack is empty.
        ///     Set by the bootstrap during gameplay to push the pause menu.
        ///     Cleared when the game session ends.
        /// </summary>
        public Action OnEscapeEmpty { get; set; }

        /// <summary>Sets the logger for screen manager diagnostics.</summary>
        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>Polls for Escape key and dispatches it to the topmost opaque screen.</summary>
        private void Update()
        {
            if (_transitioning)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Find the topmost opaque screen that can handle Escape
            IScreen topOpaque = FindTopmostOpaque();

            if (topOpaque == null)
            {
                OnEscapeEmpty?.Invoke();
                return;
            }

            // Let the screen handle Escape first; if it returns false, pop it
            if (!topOpaque.HandleEscape())
            {
                Pop();
            }
        }

        /// <summary>Re-applies cursor state when the application regains focus.</summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                // Re-apply the cursor state based on current screen stack.
                // If no UI screen is open, this will re-lock the cursor.
                ApplyCursorState();
            }
        }

        /// <summary>
        ///     Registers a screen by name for later push-by-name operations.
        /// </summary>
        public void Register(IScreen screen)
        {
            _registry[screen.ScreenName] = screen;
        }

        /// <summary>
        ///     Pushes a screen onto the stack by name. The screen must be registered.
        /// </summary>
        public void Push(string screenName, object context = null)
        {
            if (!_registry.TryGetValue(screenName, out IScreen screen))
            {
                _logger?.LogError(
                    $"[ScreenManager] Screen '{screenName}' is not registered.");

                return;
            }

            PushInternal(screen, context);
        }

        /// <summary>
        ///     Pushes a screen directly onto the stack (does not need to be registered).
        /// </summary>
        public void Push(IScreen screen, object context = null)
        {
            PushInternal(screen, context);
        }

        /// <summary>
        ///     Pops the topmost screen from the stack. If a screen below it exists,
        ///     that screen receives an <see cref="IScreen.OnShow" /> with <c>IsReturning=true</c>.
        /// </summary>
        public void Pop()
        {
            if (_stack.Count == 0 || _transitioning)
            {
                return;
            }

            IScreen popped = _stack[_stack.Count - 1];

            _stack.RemoveAt(_stack.Count - 1);

            _transitioning = true;
            popped.OnHide(() =>
            {
                _transitioning = false;

                // Show the screen that's now on top
                if (_stack.Count > 0)
                {
                    IScreen newTop = _stack[_stack.Count - 1];

                    newTop.OnShow(new ScreenShowArgs(true));
                }

                ApplyCursorState();
            });
        }

        /// <summary>
        ///     Pops all screens down to (but not including) the screen with the
        ///     given name. If the named screen is not in the stack, does nothing.
        /// </summary>
        public void PopTo(string screenName)
        {
            if (_transitioning)
            {
                return;
            }

            int targetIndex = -1;

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].ScreenName == screenName)
                {
                    targetIndex = i;

                    break;
                }
            }

            if (targetIndex < 0 || targetIndex == _stack.Count - 1)
            {
                return;
            }

            // Hide screens above the target without animation (instant)
            for (int i = _stack.Count - 1; i > targetIndex; i--)
            {
                IScreen screen = _stack[i];

                screen.OnHide(() => { });
            }

            _stack.RemoveRange(targetIndex + 1, _stack.Count - targetIndex - 1);

            IScreen revealed = _stack[targetIndex];

            revealed.OnShow(new ScreenShowArgs(true));
            ApplyCursorState();
        }

        /// <summary>
        ///     Clears the entire stack, hiding all screens. Used when transitioning
        ///     between major application states (e.g., ending a session).
        /// </summary>
        public void ClearAll()
        {
            _transitioning = false;

            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                _stack[i].OnHide(() => { });
            }

            _stack.Clear();
            ApplyCursorState();
        }

        /// <summary>
        ///     Returns true if the named screen is anywhere in the stack.
        /// </summary>
        public bool Contains(string screenName)
        {
            for (int i = 0; i < _stack.Count; i++)
            {
                if (_stack[i].ScreenName == screenName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Replaces the topmost screen with a new one. The old screen is hidden
        ///     and the new screen is shown without back-navigation to the old one.
        /// </summary>
        public void Replace(string screenName, object context = null)
        {
            if (!_registry.TryGetValue(screenName, out IScreen screen))
            {
                _logger?.LogError(
                    $"[ScreenManager] Screen '{screenName}' is not registered.");
                return;
            }

            if (_stack.Count > 0)
            {
                IScreen old = _stack[_stack.Count - 1];

                _stack.RemoveAt(_stack.Count - 1);
                old.OnHide(() => { });
            }

            _stack.Add(screen);
            screen.OnShow(new ScreenShowArgs(false, context));
            ApplyCursorState();
        }

        /// <summary>Internal push implementation that hides the current top screen and shows the new one.</summary>
        private void PushInternal(IScreen screen, object context)
        {
            if (_transitioning)
            {
                return;
            }

            // Hide the current top screen before showing the new one
            if (_stack.Count > 0)
            {
                IScreen current = _stack[_stack.Count - 1];

                current.OnHide(() => { });
            }

            _stack.Add(screen);
            screen.OnShow(new ScreenShowArgs(false, context));
            ApplyCursorState();
        }

        /// <summary>Finds the topmost screen in the stack that blocks input (IsInputOpaque is true).</summary>
        private IScreen FindTopmostOpaque()
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (_stack[i].IsInputOpaque)
                {
                    return _stack[i];
                }
            }

            return null;
        }

        /// <summary>Sets cursor lock and visibility based on whether the topmost opaque screen requires a cursor.</summary>
        private void ApplyCursorState()
        {
            // Find the topmost opaque screen that wants cursor
            IScreen topmostOpaque = FindTopmostOpaque();

            if (topmostOpaque is
                {
                    RequiresCursor: true,
                })
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
