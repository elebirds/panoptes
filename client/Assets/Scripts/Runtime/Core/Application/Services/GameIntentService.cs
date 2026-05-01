using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Domain;
using Panoptes.Core.Events;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Services
{
    public sealed class GameIntentService : IDisposable
    {
        private readonly IClientMessageSender _sender;
        private GameStateCache _cache;

        public GameIntentService(IClientMessageSender sender)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));
        }

        public event Action TurnSubmitRequested;

        public void Initialize(GameStateCache cache)
        {
            if (cache == null || ReferenceEquals(_cache, cache))
            {
                return;
            }

            if (_cache != null)
            {
                Unsubscribe(_cache);
            }

            _cache = cache;
            Subscribe(_cache);
        }

        public void Dispose()
        {
            if (_cache != null)
            {
                Unsubscribe(_cache);
                _cache = null;
            }

            ActionLock.Release();
        }

        public bool SetPolicy(string policyType)
        {
            return SendIfUnlocked(new MsgSetPolicy
            {
                NationalPolicyId = policyType ?? string.Empty
            });
        }

        public bool SetInstitutionLoadout(params string[] policyIds)
        {
            var msg = new MsgSetInstitutionLoadout();
            if (policyIds != null)
            {
                for (var i = 0; i < policyIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(policyIds[i]))
                    {
                        msg.PolicyIds.Add(policyIds[i]);
                    }
                }
            }

            return SendIfUnlocked(msg);
        }

        public bool SetResearchTarget(string technologyId)
        {
            return SendIfUnlocked(new MsgSetResearchTarget
            {
                TechnologyId = technologyId ?? string.Empty
            });
        }

        public bool RevealToken(string nodeId)
        {
            return SendIfUnlocked(new MsgRevealNode
            {
                NodeId = nodeId ?? string.Empty
            });
        }

        public bool SubmitTurn()
        {
            if (ActionLock.IsLocked)
            {
                return false;
            }

            if (!Send(new MsgSubmitTurn()))
            {
                return false;
            }

            ActionLock.Acquire();
            TurnSubmitRequested?.Invoke();
            return true;
        }

        public bool SendChatEmote(GameChatEmoteKind emote)
        {
            return Send(new MsgSendGameChat
            {
                Payload = new ChatPayload
                {
                    Emote = ToProtocol(emote)
                }
            });
        }

        public bool SetWarZone(IReadOnlyList<string> nodeIds)
        {
            var msg = new MsgSetWarZone
            {
                ZoneId = "frontline",
                Name = "Frontline"
            };
            if (nodeIds != null)
            {
                msg.NodeIds.AddRange(nodeIds);
            }

            return SendIfUnlocked(msg);
        }

        private bool SendIfUnlocked(IMessage message)
        {
            return !ActionLock.IsLocked && Send(message);
        }

        private bool Send(IMessage message)
        {
            return _sender.Send(message);
        }

        private void Subscribe(GameStateCache cache)
        {
            cache.OnPhaseChanged += OnPhaseChanged;
            cache.OnGameOver += OnGameOver;
        }

        private void Unsubscribe(GameStateCache cache)
        {
            cache.OnPhaseChanged -= OnPhaseChanged;
            cache.OnGameOver -= OnGameOver;
        }

        private static void OnPhaseChanged(PhaseChangedEvent evt)
        {
            if (!ActionLock.IsLocked || evt == null || !evt.IsInteractive)
            {
                return;
            }

            ActionLock.Release();
        }

        private static void OnGameOver(GameOverEvent _)
        {
            if (ActionLock.IsLocked)
            {
                ActionLock.Release();
            }
        }

        private static ChatEmote ToProtocol(GameChatEmoteKind emote)
        {
            return emote switch
            {
                GameChatEmoteKind.ThumbsUp => ChatEmote.ThumbsUp,
                GameChatEmoteKind.Thinking => ChatEmote.Thinking,
                GameChatEmoteKind.Laugh => ChatEmote.Laugh,
                GameChatEmoteKind.Angry => ChatEmote.Angry,
                GameChatEmoteKind.Warning => ChatEmote.Warning,
                GameChatEmoteKind.Gg => ChatEmote.Gg,
                _ => ChatEmote.Unspecified
            };
        }
    }
}
