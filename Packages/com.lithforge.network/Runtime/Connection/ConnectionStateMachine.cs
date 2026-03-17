using System;

namespace Lithforge.Network.Connection
{
    /// <summary>
    /// Per-connection finite state machine with validated transitions and timeout detection.
    /// Valid transitions:
    ///   Disconnected → Connecting
    ///   Connecting → Handshaking
    ///   Handshaking → Loading | Disconnecting
    ///   Loading → Playing
    ///   Playing → Disconnecting
    ///   Any → Disconnected
    /// </summary>
    public sealed class ConnectionStateMachine
    {
        private ConnectionState _state;
        private float _stateEntryTime;

        public ConnectionState Current
        {
            get { return _state; }
        }

        public float StateEntryTime
        {
            get { return _stateEntryTime; }
        }

        public ConnectionStateMachine()
        {
            _state = ConnectionState.Disconnected;
            _stateEntryTime = 0f;
        }

        /// <summary>
        /// Attempts to transition to a new state. Returns true if the transition is valid.
        /// </summary>
        public bool Transition(ConnectionState newState, float currentTime)
        {
            if (!IsValidTransition(_state, newState))
            {
                return false;
            }

            _state = newState;
            _stateEntryTime = currentTime;
            return true;
        }

        /// <summary>
        /// Returns the time in seconds since entering the current state.
        /// </summary>
        public float GetTimeInState(float currentTime)
        {
            return currentTime - _stateEntryTime;
        }

        /// <summary>
        /// Returns true if the connection has been in the current state longer than the given timeout.
        /// </summary>
        public bool IsTimedOut(float currentTime, float timeoutSeconds)
        {
            return GetTimeInState(currentTime) > timeoutSeconds;
        }

        /// <summary>
        /// Validates whether a transition from one state to another is permitted.
        /// </summary>
        public static bool IsValidTransition(ConnectionState from, ConnectionState to)
        {
            // Any state can transition to Disconnected
            if (to == ConnectionState.Disconnected)
            {
                return true;
            }

            switch (from)
            {
                case ConnectionState.Disconnected:
                    return to == ConnectionState.Connecting;

                case ConnectionState.Connecting:
                    return to == ConnectionState.Handshaking;

                case ConnectionState.Handshaking:
                    return to == ConnectionState.Loading || to == ConnectionState.Disconnecting;

                case ConnectionState.Loading:
                    return to == ConnectionState.Playing;

                case ConnectionState.Playing:
                    return to == ConnectionState.Disconnecting;

                case ConnectionState.Disconnecting:
                    return false; // Only Disconnected allowed, handled above

                default:
                    return false;
            }
        }
    }
}
