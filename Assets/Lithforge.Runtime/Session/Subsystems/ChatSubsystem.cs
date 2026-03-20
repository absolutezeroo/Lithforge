using System;
using System.Collections.Generic;

using Lithforge.Network;
using Lithforge.Network.Client;
using Lithforge.Network.Message;
using Lithforge.Network.Messages;
using Lithforge.Network.Server;
using Lithforge.Runtime.UI.Chat;
using Lithforge.Runtime.World;
using Lithforge.Voxel.Storage;

using UnityEngine;
using UnityEngine.UIElements;

namespace Lithforge.Runtime.Session.Subsystems
{
    /// <summary>
    ///     Subsystem that creates the chat panel UI and wires up chat message
    ///     send/receive between client and server.
    /// </summary>
    public sealed class ChatSubsystem : IGameSubsystem
    {
        /// <summary>Human-readable name for logging.</summary>
        public string Name
        {
            get
            {
                return "Chat";
            }
        }

        /// <summary>Depends on network subsystems for message routing.</summary>
        public IReadOnlyList<Type> Dependencies { get; } = new[]
        {
            typeof(NetworkServerSubsystem),
            typeof(NetworkClientSubsystem),
        };

        /// <summary>Created for sessions that render (not dedicated servers).</summary>
        public bool ShouldCreate(SessionConfig config)
        {
            return config.RequiresRendering;
        }

        /// <summary>Creates the chat panel and registers message handlers.</summary>
        public void Initialize(SessionContext context)
        {
            PanelSettings panelSettings =
                SessionInitArgsHolder.Current?.PanelSettings;

            if (panelSettings == null)
            {
                return;
            }

            GameObject chatObject = new("ChatPanel");
            ChatPanel chatPanel = chatObject.AddComponent<ChatPanel>();
            chatPanel.Initialize(panelSettings);

            context.Register(chatPanel);
        }

        /// <summary>Wires chat panel to network client for send/receive.</summary>
        public void PostInitialize(SessionContext context)
        {
            if (!context.TryGet(out ChatPanel chatPanel))
            {
                return;
            }

            // Wire server-side chat processor if we're a server
            if (context.TryGet(out NetworkServer server)
                && context.TryGet(out AdminStore adminStore))
            {
                ChatCommandProcessor chatProcessor = new(
                    server, adminStore, context.App.Logger);
                server.SetChatProcessor(chatProcessor);
            }

            // Wire client-side chat message handler
            if (context.TryGet(out NetworkClient client))
            {
                client.Dispatcher.RegisterHandler(MessageType.Chat,
                    (_, data, offset, length) =>
                    {
                        ChatMessage msg = ChatMessage.Deserialize(data, offset, length);
                        string senderName = msg.SenderPlayerId == 0
                            ? "[Server]"
                            : $"Player{msg.SenderPlayerId}";
                        chatPanel.AddMessage(senderName, msg.Content);
                    });

                // Wire submit to send ChatCmdMessage
                chatPanel.OnSubmit = text =>
                {
                    ChatCmdMessage cmd = new()
                    {
                        Content = text,
                    };
                    client.Send(cmd, PipelineId.ReliableSequenced);
                };
            }
        }

        /// <summary>No in-flight jobs to complete.</summary>
        public void Shutdown()
        {
        }

        /// <summary>Chat panel GameObject destroyed in session cleanup.</summary>
        public void Dispose()
        {
        }
    }
}
