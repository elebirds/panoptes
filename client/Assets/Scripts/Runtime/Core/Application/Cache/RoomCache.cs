using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Service;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class RoomCache : MonoBehaviour
    {
        public static RoomCache Instance { get; private set; }
        private SessionManager _sessionManager;

        public string RoomID { get; private set; } = string.Empty;
        public string RoomCode { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public List<RoomPlayerDto> Players { get; } = new();
        public string Status { get; private set; } = string.Empty;
        public int MaxPlayers { get; private set; }
        public bool IsHost { get; private set; }

        public event Action OnRoomStateChanged;
        public event Action<string, string> OnRoomCreated;
        public event Action<int> OnGameStarting;
        public event Action<string> OnLobbyError;
        public event Action<string, string> OnPlayerKicked;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void UseSessionManager(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void Apply(MsgRoomState msg)
        {
            if (msg == null)
            {
                return;
            }

            RoomID = msg.RoomId ?? string.Empty;
            RoomCode = msg.RoomCode ?? string.Empty;
            RoomName = msg.Name ?? string.Empty;
            Status = msg.Status ?? string.Empty;
            MaxPlayers = msg.MaxPlayers;

            Players.Clear();
            foreach (var player in msg.Players)
            {
                if (player == null)
                {
                    continue;
                }

                Players.Add(new RoomPlayerDto
                {
                    PlayerId = player.PlayerId,
                    Username = player.Username,
                    IsHost = player.IsHost,
                    IsReady = player.IsReady,
                    IsBot = player.IsBot,
                });
            }

            var selfPlayerId = _sessionManager != null ? _sessionManager.PlayerID : string.Empty;
            IsHost = false;
            foreach (var player in Players)
            {
                if (player.IsHost && string.Equals(player.PlayerId, selfPlayerId, StringComparison.Ordinal))
                {
                    IsHost = true;
                    break;
                }
            }

            OnRoomStateChanged?.Invoke();
        }

        public void PublishRoomCreated(MsgRoomCreated msg)
        {
            if (msg == null)
            {
                return;
            }

            RoomID = msg.RoomId ?? RoomID;
            RoomCode = msg.RoomCode ?? RoomCode;
            OnRoomCreated?.Invoke(RoomID, RoomCode);
        }

        public void PublishGameStarting(MsgGameStarting msg)
        {
            var countdown = msg != null ? msg.Countdown : 0;
            Status = "starting";
            OnGameStarting?.Invoke(countdown);
        }

        public void PublishLobbyError(MsgLobbyError msg)
        {
            OnLobbyError?.Invoke(msg != null ? msg.Code : string.Empty);
        }

        public void PublishPlayerKicked(MsgPlayerKicked msg)
        {
            if (msg == null)
            {
                return;
            }

            OnPlayerKicked?.Invoke(msg.PlayerId ?? string.Empty, msg.Username ?? string.Empty);
        }

        public void Clear()
        {
            RoomID = string.Empty;
            RoomCode = string.Empty;
            RoomName = string.Empty;
            Players.Clear();
            Status = string.Empty;
            MaxPlayers = 0;
            IsHost = false;

            OnRoomStateChanged?.Invoke();
        }

        public int GetBotCount()
        {
            var count = 0;
            foreach (var player in Players)
            {
                if (player.IsBot)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
