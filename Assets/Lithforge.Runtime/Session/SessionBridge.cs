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
        private bool _active;

        /// <summary>
        ///     Set to true by the <see cref="PauseMenuSubsystem" /> quit callback
        ///     to signal that the session should end.
        /// </summary>
        public bool QuitRequested { get; set; }

        public GameLoopPoco GameLoop { get; private set; }

        private void Update()
        {
            if (_active && GameLoop != null)
            {
                GameLoop.Update();
            }
        }

        private void LateUpdate()
        {
            if (_active && GameLoop != null)
            {
                GameLoop.LateUpdate();
            }
        }

        public void Activate(GameLoopPoco gameLoop)
        {
            GameLoop = gameLoop;
            _active = true;
        }

        public void Deactivate()
        {
            _active = false;
            GameLoop = null;
        }
    }
}
