using UnityEngine;

namespace Lithforge.Runtime.Session
{
    /// <summary>
    ///     Thin MonoBehaviour that forwards Unity Update/LateUpdate to
    ///     <see cref="GameLoopPoco" />. This is the only MonoBehaviour
    ///     created per session (aside from UI components).
    /// </summary>
    public sealed class SessionBridge : MonoBehaviour
    {
        /// <summary>Whether this bridge is actively forwarding Unity lifecycle calls.</summary>
        private bool _active;

        /// <summary>
        ///     Set to true by the <see cref="PauseMenuSubsystem" /> quit callback
        ///     to signal that the session should end.
        /// </summary>
        public bool QuitRequested { get; set; }

        /// <summary>The POCO game loop that receives forwarded Update/LateUpdate calls.</summary>
        public GameLoopPoco GameLoop { get; private set; }

        /// <summary>Forwards Unity Update to the game loop each frame.</summary>
        private void Update()
        {
            if (_active && GameLoop != null)
            {
                GameLoop.Update();
            }
        }

        /// <summary>Forwards Unity LateUpdate to the game loop each frame.</summary>
        private void LateUpdate()
        {
            if (_active && GameLoop != null)
            {
                GameLoop.LateUpdate();
            }
        }

        /// <summary>Activates the bridge with the given game loop to begin forwarding updates.</summary>
        public void Activate(GameLoopPoco gameLoop)
        {
            GameLoop = gameLoop;
            _active = true;
        }

        /// <summary>Deactivates the bridge, stopping all forwarded updates.</summary>
        public void Deactivate()
        {
            _active = false;
            GameLoop = null;
        }
    }
}
