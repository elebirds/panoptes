/*************************************************
 * Project: Panoptes
 * File: GameChatCache.cs
 * Author: Panoptes Team
 * Date: 2026-04-17
 * Description: Session-scoped chat stream cache for UI presentation.
 *************************************************/

using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Cache
{
    public sealed class GameChatCache : MonoBehaviour
    {
        public static GameChatCache Instance { get; private set; }

        private readonly List<GameChatEntryDto> _entries = new();
        public IReadOnlyList<GameChatEntryDto> Entries => _entries;

        public event Action OnEntriesChanged;
        public event Action<GameChatEntryDto> OnEntryAdded;

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

        public static GameChatCache EnsureInstance()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = UnityEngine.Object.FindAnyObjectByType<GameChatCache>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("GameChatCache");
            return go.AddComponent<GameChatCache>();
        }

        public void ApplyPosted(MsgGameChatPosted msg)
        {
            var entry = GameChatMapper.ToDto(msg?.Entry);
            if (entry == null)
            {
                return;
            }

            _entries.Add(entry);
            OnEntryAdded?.Invoke(entry);
            OnEntriesChanged?.Invoke();
        }

        public void ApplySync(MsgGameChatSync msg)
        {
            _entries.Clear();
            if (msg?.Entries != null)
            {
                for (var i = 0; i < msg.Entries.Count; i++)
                {
                    var entry = GameChatMapper.ToDto(msg.Entries[i]);
                    if (entry != null)
                    {
                        _entries.Add(entry);
                    }
                }
            }

            OnEntriesChanged?.Invoke();
        }

        public void Clear()
        {
            _entries.Clear();
            OnEntriesChanged?.Invoke();
        }
    }
}
