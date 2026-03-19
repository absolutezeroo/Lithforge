namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Per-connection finite state machine with validated transitions and timeout detection.
    ///     Valid transitions:
    ///     Disconnected → Connecting | Reconnecting
    ///     Connecting → Handshaking
    ///     Handshaking → Authenticating | Disconnecting
    ///     Authenticating → Configuring | Disconnecting
    ///     Configuring → Loading | Disconnecting
    ///     Loading → Playing
    ///     Playing → Disconnecting | Configuring (hot-reload)
    ///     Reconnecting → Connecting | Disconnected
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
                ConnectionState.Disconnected => to is ConnectionState.Connecting or ConnectionState.Reconnecting,
                ConnectionState.Connecting => to == ConnectionState.Handshaking,
                ConnectionState.Handshaking => to is ConnectionState.Authenticating or ConnectionState.Disconnecting,
                ConnectionState.Authenticating => to is ConnectionState.Configuring or ConnectionState.Disconnecting,
                ConnectionState.Configuring => to is ConnectionState.Loading or ConnectionState.Disconnecting,
                ConnectionState.Loading => to == ConnectionState.Playing,
                ConnectionState.Playing => to is ConnectionState.Disconnecting or ConnectionState.Configuring,
                ConnectionState.Reconnecting => to == ConnectionState.Connecting,
                ConnectionState.Disconnecting => false, // Only Disconnected allowed, handled above
                _ => false,
            };
        }
    }
}
