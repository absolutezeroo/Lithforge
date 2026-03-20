using System.Collections.Generic;

using Lithforge.Network.Connection;
using Lithforge.Network.Messages;
using Lithforge.Voxel.Storage;

using ILogger = Lithforge.Core.Logging.ILogger;

namespace Lithforge.Network.Server
{
    /// <summary>
    ///     Parses and executes admin chat commands (/op, /deop, /kick, /ban, /unban, /whitelist, /list).
    ///     Non-command messages are broadcast to all playing peers.
    /// </summary>
    public sealed class ChatCommandProcessor
    {
        /// <summary>The network server for sending messages and disconnecting peers.</summary>
        private readonly NetworkServer _server;

        /// <summary>Admin store for op/ban/whitelist management.</summary>
        private readonly AdminStore _adminStore;

        /// <summary>Logger for diagnostic messages.</summary>
        private readonly ILogger _logger;

        /// <summary>Creates the chat command processor with server, admin store, and logger.</summary>
        public ChatCommandProcessor(NetworkServer server, AdminStore adminStore, ILogger logger)
        {
            _server = server;
            _adminStore = adminStore;
            _logger = logger;
        }

        /// <summary>
        ///     Processes a chat command from a peer. Commands starting with "/" are admin
        ///     commands (require IsAdmin). All others are broadcast as chat messages.
        /// </summary>
        public void ProcessChat(PeerInfo sender, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            if (content.StartsWith("/"))
            {
                ProcessCommand(sender, content);
                return;
            }

            // Broadcast chat to all playing peers
            ChatMessage broadcastMsg = new()
            {
                SenderPlayerId = sender.AssignedPlayerId,
                Content = content,
            };
            _server.Broadcast(broadcastMsg, PipelineId.ReliableSequenced);
        }

        /// <summary>Parses and executes an admin command.</summary>
        private void ProcessCommand(PeerInfo sender, string command)
        {
            string[] parts = command.Split(' ', 3);
            string cmd = parts[0].ToLowerInvariant();

            if (cmd == "/list")
            {
                ExecuteList(sender);
                return;
            }

            // All other commands require admin
            if (!sender.IsAdmin)
            {
                SendReply(sender, "You do not have permission to use this command.");
                return;
            }

            if (_adminStore == null)
            {
                SendReply(sender, "Admin store is not available.");
                return;
            }

            string targetName = parts.Length > 1 ? parts[1] : "";
            string reason = parts.Length > 2 ? parts[2] : "";

            switch (cmd)
            {
                case "/op":
                    ExecuteOp(sender, targetName);
                    break;
                case "/deop":
                    ExecuteDeop(sender, targetName);
                    break;
                case "/kick":
                    ExecuteKick(sender, targetName, reason);
                    break;
                case "/ban":
                    ExecuteBan(sender, targetName, reason);
                    break;
                case "/unban":
                    ExecuteUnban(sender, targetName);
                    break;
                case "/whitelist":
                    ExecuteWhitelist(sender, targetName, reason);
                    break;
                default:
                    SendReply(sender, $"Unknown command: {cmd}");
                    break;
            }
        }

        /// <summary>Lists all online players.</summary>
        private void ExecuteList(PeerInfo sender)
        {
            IReadOnlyList<PeerInfo> peers = _server.AllPeers;
            List<string> names = new();

            for (int i = 0; i < peers.Count; i++)
            {
                if (peers[i].StateMachine.Current == ConnectionState.Playing)
                {
                    names.Add(peers[i].PlayerName);
                }
            }

            string msg = names.Count > 0
                ? $"Online ({names.Count}): {string.Join(", ", names)}"
                : "No players online.";
            SendReply(sender, msg);
        }

        /// <summary>Grants operator status to a player by name.</summary>
        private void ExecuteOp(PeerInfo sender, string targetName)
        {
            PeerInfo target = FindPeerByName(targetName);

            if (target == null)
            {
                SendReply(sender, $"Player '{targetName}' not found.");
                return;
            }

            _adminStore.AddOp(target.PlayerUuid);
            target.IsAdmin = true;
            _adminStore.Save();
            SendReply(sender, $"Opped {target.PlayerName}.");
            _logger.LogInfo($"[Chat] {sender.PlayerName} opped {target.PlayerName}");
        }

        /// <summary>Revokes operator status from a player by name.</summary>
        private void ExecuteDeop(PeerInfo sender, string targetName)
        {
            PeerInfo target = FindPeerByName(targetName);

            if (target == null)
            {
                SendReply(sender, $"Player '{targetName}' not found.");
                return;
            }

            _adminStore.RemoveOp(target.PlayerUuid);
            target.IsAdmin = false;
            _adminStore.Save();
            SendReply(sender, $"De-opped {target.PlayerName}.");
            _logger.LogInfo($"[Chat] {sender.PlayerName} de-opped {target.PlayerName}");
        }

        /// <summary>Kicks a player by name with an optional reason.</summary>
        private void ExecuteKick(PeerInfo sender, string targetName, string reason)
        {
            PeerInfo target = FindPeerByName(targetName);

            if (target == null)
            {
                SendReply(sender, $"Player '{targetName}' not found.");
                return;
            }

            string displayReason = string.IsNullOrEmpty(reason) ? "Kicked by operator" : reason;
            _server.DisconnectPeer(target.ConnectionId, DisconnectReason.Kicked);
            SendReply(sender, $"Kicked {target.PlayerName}: {displayReason}");
            _logger.LogInfo($"[Chat] {sender.PlayerName} kicked {target.PlayerName}: {displayReason}");
        }

        /// <summary>Bans a player by name with an optional reason.</summary>
        private void ExecuteBan(PeerInfo sender, string targetName, string reason)
        {
            PeerInfo target = FindPeerByName(targetName);

            if (target == null)
            {
                SendReply(sender, $"Player '{targetName}' not found.");
                return;
            }

            BanEntry ban = new()
            {
                PlayerUuid = target.PlayerUuid,
                Reason = string.IsNullOrEmpty(reason) ? "Banned by operator" : reason,
                BannedBy = sender.PlayerUuid,
            };

            _adminStore.Ban(ban);
            _adminStore.Save();
            _server.DisconnectPeer(target.ConnectionId, DisconnectReason.Kicked);
            SendReply(sender, $"Banned {target.PlayerName}.");
            _logger.LogInfo($"[Chat] {sender.PlayerName} banned {target.PlayerName}");
        }

        /// <summary>Unbans a player by name (requires UUID match from admin store).</summary>
        private void ExecuteUnban(PeerInfo sender, string targetUuid)
        {
            _adminStore.Unban(targetUuid);
            _adminStore.Save();
            SendReply(sender, $"Unbanned {targetUuid}.");
            _logger.LogInfo($"[Chat] {sender.PlayerName} unbanned {targetUuid}");
        }

        /// <summary>Manages the whitelist (add/remove subcommands).</summary>
        private void ExecuteWhitelist(PeerInfo sender, string subCommand, string targetName)
        {
            switch (subCommand.ToLowerInvariant())
            {
                case "add":
                {
                    PeerInfo target = FindPeerByName(targetName);

                    if (target == null)
                    {
                        SendReply(sender, $"Player '{targetName}' not found.");
                        return;
                    }

                    _adminStore.AddWhitelist(target.PlayerUuid);
                    _adminStore.Save();
                    SendReply(sender, $"Added {target.PlayerName} to whitelist.");
                    break;
                }
                case "remove":
                {
                    PeerInfo target = FindPeerByName(targetName);

                    if (target == null)
                    {
                        SendReply(sender, $"Player '{targetName}' not found.");
                        return;
                    }

                    _adminStore.RemoveWhitelist(target.PlayerUuid);
                    _adminStore.Save();
                    SendReply(sender, $"Removed {target.PlayerName} from whitelist.");
                    break;
                }
                default:
                    SendReply(sender, "Usage: /whitelist add|remove <name>");
                    break;
            }
        }

        /// <summary>Sends a system reply message to a specific peer.</summary>
        private void SendReply(PeerInfo peer, string content)
        {
            ChatMessage reply = new()
            {
                SenderPlayerId = 0,
                Content = content,
            };
            _server.SendTo(peer.ConnectionId, reply, PipelineId.ReliableSequenced);
        }

        /// <summary>Finds a connected peer by display name (case-insensitive).</summary>
        private PeerInfo FindPeerByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            IReadOnlyList<PeerInfo> peers = _server.AllPeers;

            for (int i = 0; i < peers.Count; i++)
            {
                if (string.Equals(peers[i].PlayerName, name,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    return peers[i];
                }
            }

            return null;
        }
    }
}
