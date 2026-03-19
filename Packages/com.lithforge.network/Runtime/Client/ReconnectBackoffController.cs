using Lithforge.Network.Connection;

namespace Lithforge.Network.Client
{
    /// <summary>
    ///     Client-side exponential backoff controller for reconnection attempts.
    ///     After a disconnect, the client waits an increasing delay before each
    ///     reconnection attempt: 1s, 2s, 4s, 8s, 16s (capped at <see cref="MaxDelaySeconds" />).
    ///     Resets on successful reconnection or when the session token expires.
    /// </summary>
    public sealed class ReconnectBackoffController
    {
        /// <summary>Initial delay before the first reconnection attempt.</summary>
        public const float BaseDelaySeconds = 1f;

        /// <summary>Maximum delay between reconnection attempts.</summary>
        public const float MaxDelaySeconds = 16f;

        /// <summary>Maximum number of reconnection attempts before giving up.</summary>
        public const int MaxAttempts = 5;

        private int _attempts;

        private float _nextRetryTime;

        /// <summary>The session token to use for reconnection. Zero = not eligible.</summary>
        public SessionToken Token { get; private set; }

        /// <summary>Number of reconnection attempts made so far.</summary>
        public int Attempts
        {
            get { return _attempts; }
        }

        /// <summary>True if the controller has a valid token and hasn't exhausted attempts.</summary>
        public bool CanReconnect
        {
            get { return Token.IsValid && _attempts < MaxAttempts; }
        }

        /// <summary>
        ///     Stores a session token received from the server. Called after a successful
        ///     handshake to enable future reconnection.
        /// </summary>
        public void SetToken(SessionToken token)
        {
            Token = token;
            _attempts = 0;
            _nextRetryTime = 0f;
        }

        /// <summary>
        ///     Returns true if enough time has passed for the next reconnection attempt.
        /// </summary>
        public bool ShouldAttempt(float currentTime)
        {
            if (!CanReconnect)
            {
                return false;
            }

            return currentTime >= _nextRetryTime;
        }

        /// <summary>
        ///     Records a reconnection attempt and computes the next retry time.
        /// </summary>
        public void RecordAttempt(float currentTime)
        {
            float delay = BaseDelaySeconds * (1 << _attempts);

            if (delay > MaxDelaySeconds)
            {
                delay = MaxDelaySeconds;
            }

            _nextRetryTime = currentTime + delay;
            _attempts++;
        }

        /// <summary>
        ///     Resets the backoff state. Called on successful reconnection.
        /// </summary>
        public void Reset()
        {
            _attempts = 0;
            _nextRetryTime = 0f;
        }

        /// <summary>
        ///     Clears the token and resets all state. Called when the player explicitly
        ///     disconnects or when the token expires.
        /// </summary>
        public void Clear()
        {
            Token = default;
            _attempts = 0;
            _nextRetryTime = 0f;
        }
    }
}
