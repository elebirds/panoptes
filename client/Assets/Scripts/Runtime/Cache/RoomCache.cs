using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Service;
using UnityEngine;

namespace Panoptes.Runtime.Cache
{
    public sealed class RoomCache : MonoBehaviour
    {
        public static RoomCache Instance { get; private set; }

        public string RoomID { get; private set; } = string.Empty;
        public string RoomCode { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public List<RoomPlayer> Players { get; } = new();
        public string Status { get; private set; } = string.Empty;
        public int MaxPlayers { get; private set; }
        public bool IsHost { get; private set; }

        public event Action OnRoomStateChanged;

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
                Players.Add(player.Clone());
            }

            var selfPlayerId = SessionManager.Instance != null ? SessionManager.Instance.PlayerID : string.Empty;
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
