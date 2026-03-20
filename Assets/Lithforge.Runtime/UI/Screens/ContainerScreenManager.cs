using System;
using System.Collections.Generic;

using UnityEngine;

using BlockEntityBase = Lithforge.Runtime.BlockEntity.BlockEntity;
using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Runtime.UI.Screens
{
    /// <summary>
    ///     Manages all block entity container screens. Maintains one cached screen
    ///     instance per entity type ID. Provides data-driven dispatch from a
    ///     block entity instance to its screen without hardcoded type checks.
    ///     Registration: call <see cref="Register" /> at bootstrap for each screen type.
    ///     Dispatch: BlockInteraction calls <see cref="TryOpenForEntity" /> on right-click.
    ///     Adding a new block entity GUI requires no changes to this class — only a new
    ///     <see cref="ContainerScreen" /> subclass and one <see cref="Register" /> call
    ///     in LithforgeBootstrap.
    /// </summary>
    public sealed class ContainerScreenManager : MonoBehaviour
    {
        /// <summary>Logger for container screen manager diagnostics.</summary>
        private ILogger _logger;

        /// <summary>List of registered screen bindings keyed by entity type ID.</summary>
        private readonly List<BlockEntityScreenBinding> _bindings = new();

        /// <summary>The currently open block entity screen, or null if none is active.</summary>
        private ContainerScreen _activeScreen;

        /// <summary>Frame number when the last screen was closed, for same-frame close detection.</summary>
        private int _lastCloseFrame = -1;

        /// <summary>
        ///     Returns true if a block entity screen is currently open.
        /// </summary>
        public bool HasActiveScreen
        {
            get { return _activeScreen != null && _activeScreen.IsOpen; }
        }

        /// <summary>
        ///     Returns true if a block entity screen was closed during the current frame.
        ///     Used to prevent the E key from both closing a block entity screen
        ///     and opening the player inventory in the same frame.
        /// </summary>
        public bool WasClosedThisFrame
        {
            get { return _lastCloseFrame == Time.frameCount; }
        }

        /// <summary>Sets the logger for container screen manager diagnostics.</summary>
        public void SetLogger(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        ///     Records that a screen was closed this frame. Called from
        ///     <see cref="ContainerScreen.Close" /> to handle script execution
        ///     order races between block entity screens and PlayerInventoryScreen.
        /// </summary>
        public void NotifyScreenClosed()
        {
            _lastCloseFrame = Time.frameCount;
        }

        /// <summary>
        ///     Registers a screen factory and open action for the given entity type ID.
        ///     The factory is invoked lazily on first open. The open action casts the
        ///     entity to the concrete type and calls the screen's typed OpenForEntity method.
        /// </summary>
        public void Register(
            string entityTypeId,
            Func<ContainerScreen> factory,
            Action<ContainerScreen, BlockEntityBase> openAction)
        {
            _bindings.Add(new BlockEntityScreenBinding(entityTypeId, factory, openAction));
        }

        /// <summary>
        ///     Opens the appropriate screen for the given block entity.
        ///     Returns true if a registered screen was found and opened.
        ///     Returns false if no dispatch is registered for this entity type.
        /// </summary>
        public bool TryOpenForEntity(BlockEntityBase entity)
        {
            if (entity == null)
            {
                return false;
            }

            BlockEntityScreenBinding binding = FindBinding(entity.TypeId);

            if (binding == null)
            {
                return false;
            }

            if (_activeScreen != null && _activeScreen.IsOpen)
            {
                _activeScreen.Close();
            }

            ContainerScreen screen = GetOrCreate(binding);

            if (screen == null)
            {
                return false;
            }

            binding.OpenAction(screen, entity);
            _activeScreen = screen;
            return true;
        }

        /// <summary>
        ///     Closes whatever screen is currently open.
        /// </summary>
        public void CloseActive()
        {
            if (_activeScreen != null && _activeScreen.IsOpen)
            {
                _activeScreen.Close();
            }
        }

        /// <summary>
        ///     Sets visibility on all instantiated block entity screens.
        ///     Used by HudVisibilityController during spawn loading.
        /// </summary>
        public void SetAllVisible(bool visible)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].Screen != null)
                {
                    _bindings[i].Screen.SetVisible(visible);
                }
            }
        }

        /// <summary>Finds the binding for the given entity type ID, or null if not registered.</summary>
        private BlockEntityScreenBinding FindBinding(string entityTypeId)
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i].EntityTypeId == entityTypeId)
                {
                    return _bindings[i];
                }
            }

            return null;
        }

        /// <summary>Lazily creates the screen instance if it has not been instantiated yet.</summary>
        private ContainerScreen GetOrCreate(BlockEntityScreenBinding binding)
        {
            if (binding.Screen == null)
            {
                try
                {
                    binding.Screen = binding.Factory();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(
                        "[ContainerScreenManager] Factory failed for entity type '"
                        + binding.EntityTypeId + "': " + ex.Message);
                    return null;
                }
            }

            return binding.Screen;
        }
    }
}
