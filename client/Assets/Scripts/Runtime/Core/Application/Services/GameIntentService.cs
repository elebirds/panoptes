using System;
using System.Collections.Generic;
using Google.Protobuf;
using Panoptes.Core.Application.Intents;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;
using R3;

namespace Panoptes.Core.Application.Services
{
    public sealed class GameIntentService : IDisposable
    {
        private readonly IClientMessageSender _sender;
        private readonly IDisposable _turnSubscription;
        private readonly IDisposable _gameOverSubscription;

        public GameIntentService(
            IClientMessageSender sender,
            TurnStore turnStore = null,
            GameOverStore gameOverStore = null)
        {
            _sender = sender ?? throw new ArgumentNullException(nameof(sender));

            _turnSubscription = turnStore?.State.Subscribe(static state => ReleaseActionLockIfInteractive(state));
            _gameOverSubscription = gameOverStore?.State.Subscribe(static state => ReleaseActionLockIfGameOver(state));
        }

        public event Action TurnSubmitRequested;

        public void Dispose()
        {
            _turnSubscription?.Dispose();
            _gameOverSubscription?.Dispose();
            ActionLock.Release();
        }

        public bool SetPolicy(string policyType)
        {
            return SendIfUnlocked(new MsgSetPolicy
            {
                NationalPolicyId = policyType ?? string.Empty
            });
        }

        public bool SetInstitutionLoadout(params string[] institutionIds)
        {
            var msg = new MsgSetInstitutionLoadout();
            if (institutionIds != null)
            {
                for (var i = 0; i < institutionIds.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(institutionIds[i]))
                    {
                        msg.InstitutionIds.Add(institutionIds[i]);
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

        private static void ReleaseActionLockIfInteractive(TurnState state)
        {
            if (!ActionLock.IsLocked || state == null || !state.IsInteractive)
            {
                return;
            }

            ActionLock.Release();
        }

        private static void ReleaseActionLockIfGameOver(GameOverState state)
        {
            if (ActionLock.IsLocked && state != null && state.IsGameOver)
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
