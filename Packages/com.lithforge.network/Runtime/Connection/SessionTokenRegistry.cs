using System;
using System.Collections.Generic;

namespace Lithforge.Network.Connection
{
    /// <summary>
    ///     Server-side registry of active session tokens. Issues tokens when players are
    ///     accepted and validates them during reconnection attempts. Tokens expire after
    ///     <see cref="TokenLifetimeSeconds" /> to prevent indefinite reconnection windows.
    ///     Thread-safety: main-thread only (same thread as ServerGameLoop).
    /// </summary>
    public sealed class SessionTokenRegistry
    {
        /// <summary>Seconds before an issued token expires and can no longer be used for reconnection.</summary>
        public const float TokenLifetimeSeconds = 120f;

        /// <summary>Random number generator for producing unique token values.</summary>
        private readonly Random _random = new();

        /// <summary>Map from raw token values to their associated player ID and issue time.</summary>
        private readonly Dictionary<ulong, TokenEntry> _tokens = new();

        /// <summary>Scratch list for expiry sweep to avoid modifying the dictionary during iteration.</summary>
        private readonly List<ulong> _expiredKeys = new();

        /// <summary>Number of active (non-expired) tokens.</summary>
        public int Count
        {
            get { return _tokens.Count; }
        }

        /// <summary>
        ///     Issues a new session token for the given player ID. The token is valid for
        ///     <see cref="TokenLifetimeSeconds" /> from the time of issue.
        /// </summary>
        public SessionToken Issue(ushort playerId, float currentTime)
        {
            ulong value = GenerateTokenValue();

            _tokens[value] = new TokenEntry
            {
                PlayerId = playerId,
                IssuedAt = currentTime,
            };

            return new SessionToken(value);
        }

        /// <summary>
        ///     Validates a session token. Returns true and outputs the associated player ID
        ///     if the token exists and has not expired. Consumes the token on success
        ///     (single-use).
        /// </summary>
        public bool TryValidate(SessionToken token, float currentTime, out ushort playerId)
        {
            playerId = 0;

            if (!token.IsValid)
            {
                return false;
            }

            if (!_tokens.TryGetValue(token.Value, out TokenEntry entry))
            {
                return false;
            }

            // Check expiry before consuming — expired tokens are cleaned up but not consumed
            if (currentTime - entry.IssuedAt > TokenLifetimeSeconds)
            {
                _tokens.Remove(token.Value);

                return false;
            }

            // Consume on success (single-use)
            _tokens.Remove(token.Value);
            playerId = entry.PlayerId;

            return true;
        }

        /// <summary>
        ///     Revokes the token with the given value, if present. Called when a player
        ///     disconnects gracefully (no reconnection expected).
        /// </summary>
        public void Revoke(SessionToken token)
        {
            if (token.IsValid)
            {
                _tokens.Remove(token.Value);
            }
        }

        /// <summary>
        ///     Removes all expired tokens. Call periodically (e.g. once per second) to
        ///     prevent unbounded growth.
        /// </summary>
        public void PurgeExpired(float currentTime)
        {
            _expiredKeys.Clear();

            foreach (KeyValuePair<ulong, TokenEntry> pair in _tokens)
            {
                if (currentTime - pair.Value.IssuedAt > TokenLifetimeSeconds)
                {
                    _expiredKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < _expiredKeys.Count; i++)
            {
                _tokens.Remove(_expiredKeys[i]);
            }
        }

        /// <summary>Removes all tokens. Call on session teardown.</summary>
        public void Clear()
        {
            _tokens.Clear();
        }

        /// <summary>Generates a random non-zero 64-bit token value.</summary>
        private ulong GenerateTokenValue()
        {
            byte[] bytes = new byte[8];
            _random.NextBytes(bytes);

            ulong value = BitConverter.ToUInt64(bytes, 0);

            // Ensure non-zero (zero is the invalid sentinel)
            if (value == 0)
            {
                value = 1;
            }

            return value;
        }

        /// <summary>Entry stored per issued token.</summary>
        private struct TokenEntry
        {
            /// <summary>Player ID the token was issued for.</summary>
            public ushort PlayerId;

            /// <summary>Wall-clock time the token was issued.</summary>
            public float IssuedAt;
        }
    }
}
