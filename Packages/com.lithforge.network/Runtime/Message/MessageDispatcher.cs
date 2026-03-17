using System;
using Lithforge.Core.Logging;
using Lithforge.Network.Transport;

namespace Lithforge.Network.Message
{
    /// <summary>
    /// Drains transport events, parses message headers, and dispatches payloads
    /// to registered handlers. Also forwards raw Connect/Disconnect events.
    /// </summary>
    public sealed class MessageDispatcher
    {
        private readonly ILogger _logger;
        private readonly MessageHandler[] _handlers = new MessageHandler[256];
        private Action<ConnectionId> _onConnect;
        private Action<ConnectionId> _onDisconnect;

        public MessageDispatcher(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Registers a handler for the given message type.
        /// Only one handler per type — later registration overwrites earlier.
        /// </summary>
        public void RegisterHandler(MessageType type, MessageHandler handler)
        {
            _handlers[(byte)type] = handler;
        }

        /// <summary>
        /// Registers a callback for raw Connect events from the transport.
        /// </summary>
        public void OnConnect(Action<ConnectionId> callback)
        {
            _onConnect = callback;
        }

        /// <summary>
        /// Registers a callback for raw Disconnect events from the transport.
        /// </summary>
        public void OnDisconnect(Action<ConnectionId> callback)
        {
            _onDisconnect = callback;
        }

        /// <summary>
        /// Drains all buffered events from the transport, parsing Data events
        /// into message headers and dispatching to the appropriate handler.
        /// Must be called once per frame after transport.Update().
        /// </summary>
        public void ProcessEvents(INetworkTransport transport)
        {
            NetworkEventType eventType;

            while ((eventType = transport.PollEvent(
                out ConnectionId connectionId,
                out byte[] data,
                out int offset,
                out int length)) != NetworkEventType.Empty)
            {
                switch (eventType)
                {
                    case NetworkEventType.Connect:
                        _onConnect?.Invoke(connectionId);
                        break;

                    case NetworkEventType.Disconnect:
                        _onDisconnect?.Invoke(connectionId);
                        break;

                    case NetworkEventType.Data:
                        ProcessDataEvent(connectionId, data, offset, length);
                        break;
                }
            }
        }

        private void ProcessDataEvent(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            if (length < MessageHeader.Size)
            {
                _logger.LogWarning(
                    $"Received data packet too small for header from connection {connectionId}: {length} bytes");
                return;
            }

            MessageHeader header = MessageHeader.Read(data, offset);
            int expectedTotal = MessageHeader.Size + header.PayloadLength;

            if (length < expectedTotal)
            {
                _logger.LogWarning(
                    $"Truncated message from connection {connectionId}: " +
                    $"type={header.Type}, expected={expectedTotal}, got={length}");
                return;
            }

            MessageHandler handler = _handlers[(byte)header.Type];

            if (handler == null)
            {
                _logger.LogWarning(
                    $"No handler registered for message type {header.Type} from connection {connectionId}");
                return;
            }

            handler(connectionId, data, offset + MessageHeader.Size, header.PayloadLength);
        }
    }
}
