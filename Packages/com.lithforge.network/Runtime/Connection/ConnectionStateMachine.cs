namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Per-connection finite state machine with validated transitions and timeout detection.
    ///     Valid transitions:
    ///     Disconnected → Connecting
    ///     Connecting → Handshaking
    ///     Handshaking → Loading | Disconnecting
    ///     Loading → Playing
    ///     Playing → Disconnecting
    ///     Any → Disconnected
    /// </summary>
    public sealed class ConnectionStateMachine
    {
        public ConnectionState Current { get; private set; } = ConnectionState.Disconnected;

        public float StateEntryTime { get; private set; }

        /// <summary>
        ///     Attempts to transition to a new state. Returns true if the transition is valid.
        /// </summary>
        public bool Transition(ConnectionState newState, float currentTime)
        {
            if (!IsValidTransition(Current, newState))
            {
                return false;
            }

            Current = newState;
            StateEntryTime = currentTime;

            return true;
        }

        /// <summary>
        ///     Returns the time in seconds since entering the current state.
        /// </summary>
        public float GetTimeInState(float currentTime)
        {
            return currentTime - StateEntryTime;
        }

        /// <summary>
        ///     Returns true if the connection has been in the current state longer than the given timeout.
        /// </summary>
        public bool IsTimedOut(float currentTime, float timeoutSeconds)
        {
            return GetTimeInState(currentTime) > timeoutSeconds;
        }

        /// <summary>
        ///     Validates whether a transition from one state to another is permitted.
        /// </summary>
        public static bool IsValidTransition(ConnectionState from, ConnectionState to)
        {
            // Any state can transition to Disconnected
            if (to == ConnectionState.Disconnected)
            {
                return true;
            }

            return from switch
            {
                ConnectionState.Disconnected => to == ConnectionState.Connecting,
                ConnectionState.Connecting => to == ConnectionState.Handshaking,
                ConnectionState.Handshaking => to == ConnectionState.Loading || to == ConnectionState.Disconnecting,
                ConnectionState.Loading => to == ConnectionState.Playing,
                ConnectionState.Playing => to == ConnectionState.Disconnecting,
                ConnectionState.Disconnecting => false // Only Disconnected allowed, handled above
                ,
                _ => false,
            };
        }
    }
}
