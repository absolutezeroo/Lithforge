using System;

namespace Lithforge.Network.Bridge
{
    /// <summary>
    ///     Wraps an exception thrown on the server thread for rethrowing on the main thread.
    /// </summary>
    public sealed class ServerThreadFaultException : Exception
    {
        /// <summary>Creates a new fault exception wrapping the inner server thread exception.</summary>
        public ServerThreadFaultException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
