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
        /// <summary>
        /// Logger instance for diagnostic messages.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Array of registered message handlers indexed by MessageType byte value.
        /// </summary>
        private readonly MessageHandler[] _handlers = new MessageHandler[256];

        /// <summary>
        /// Callback invoked when a Connect event is received from the transport.
        /// </summary>
        private Action<ConnectionId> _onConnect;

        /// <summary>
        /// Callback invoked when a Disconnect event is received from the transport.
        /// </summary>
        private Action<ConnectionId> _onDisconnect;

        /// <summary>
        ///     Optional callback invoked on each received data event with the total byte count.
        ///     Used by <see cref="INetworkMetricsSource" /> implementations to track received bandwidth.
        /// </summary>
        private Action<int> _onDataReceived;

        /// <summary>
        /// Creates a new MessageDispatcher with the given logger.
        /// </summary>
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
        ///     Registers a callback invoked for each received data event with the byte count.
        ///     Used for bandwidth metrics tracking.
        /// </summary>
        public void OnDataReceived(Action<int> callback)
        {
            _onDataReceived = callback;
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

        /// <summary>
        /// Parses the message header from a data event and dispatches to the registered handler.
        /// </summary>
        private void ProcessDataEvent(ConnectionId connectionId, byte[] data, int offset, int length)
        {
            _onDataReceived?.Invoke(length);

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
