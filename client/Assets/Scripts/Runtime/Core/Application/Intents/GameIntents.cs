using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Network;
using UnityEngine;

namespace Panoptes.Core.Application.Intents
{
    public static class GameIntents
    {
        private enum LockSource
        {
            None,
            Domestic,
            Combat
        }

        [Serializable]
        private sealed class MinisterDirectivePayload
        {
            public string directive_type;
            public string action_id;
        }

        private static GameStateCache _cache;
        private static LockSource _lockSource = LockSource.None;

        public static void Initialize(GameStateCache cache)
        {
            if (cache == null)
            {
                Debug.LogWarning("[GameIntents] Initialize ignored: cache is null.");
                return;
            }

            if (ReferenceEquals(_cache, cache))
            {
                return;
            }

            if (_cache != null)
            {
                Unsubscribe(_cache);
            }

            _cache = cache;
            Subscribe(_cache);
            _lockSource = LockSource.None;
            Debug.Log("[GameIntents] Initialize");
        }

        public static void Dispose()
        {
            if (_cache != null)
            {
                Unsubscribe(_cache);
                _cache = null;
            }

            _lockSource = LockSource.None;
            ActionLock.Release();
            Debug.Log("[GameIntents] Dispose");
        }

        public static void SetPolicy(string policyType)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgSetPolicy
            {
                Policy = policyType ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] SetPolicy");
        }

        public static void BuildToken(string nodeId, string buildingType)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenBuild
            {
                NodeId = nodeId ?? string.Empty,
                BuildingType = buildingType ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] BuildToken");
        }

        public static void RevealToken(string nodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenReveal
            {
                NodeId = nodeId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] RevealToken");
        }

        public static void VetoToken(string actionId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenVeto
            {
                ActionId = actionId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] VetoToken");
        }

        public static void AdjustFlow(string fromNodeId, string toNodeId, string resourceType, int delta)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenAdjustFlow
            {
                FromNode = fromNodeId ?? string.Empty,
                ToNode = toNodeId ?? string.Empty,
                ResourceType = resourceType ?? string.Empty,
                Amount = delta
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] AdjustFlow");
        }

        public static void AdjustFlow(string fromNodeId, string toNodeId, int delta)
        {
            Debug.LogWarning("[GameIntents] AdjustFlow called without resourceType, sending empty resource_type.");
            AdjustFlow(fromNodeId, toNodeId, string.Empty, delta);
        }

        public static void SubmitDomestic()
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            ActionLock.Acquire();
            _lockSource = LockSource.Domestic;
            MessageSender.Send(new MsgSubmitDomestic());
            Debug.Log("[GameIntents] SubmitDomestic");
        }

        public static void SetWarZone(List<string> nodeIds)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgSetWarZone();
            if (nodeIds != null)
            {
                msg.NodeIds.AddRange(nodeIds);
            }

            MessageSender.Send(msg);
            Debug.Log("[GameIntents] SetWarZone");
        }

        public static void VetoCombat(string unitId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenVetoCombat
            {
                UnitId = unitId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] VetoCombat");
        }

        public static void MoveUnit(string unitId, string targetNodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenMicro
            {
                UnitId = unitId ?? string.Empty,
                TargetNode = targetNodeId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] MoveUnit");
        }

        public static void MicroUnit(string unitId, string targetNodeId)
        {
            MoveUnit(unitId, targetNodeId);
        }

        public static void SubmitCombat()
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            ActionLock.Acquire();
            _lockSource = LockSource.Combat;
            MessageSender.Send(new MsgSubmitCombat());
            Debug.Log("[GameIntents] SubmitCombat");
        }

        public static void AcceptMinisterAction(string actionId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgMinisterDirective
            {
                MinisterRole = string.Empty,
                Content = BuildMinisterDirectiveContent("accept", actionId)
            };
            MessageSender.Send(msg);
            Debug.LogWarning("[GameIntents] MinisterDirective encoding pending protocol confirmation, using JSON content payload.");
            Debug.Log("[GameIntents] AcceptMinisterAction");
        }

        public static void RejectMinisterAction(string actionId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgMinisterDirective
            {
                MinisterRole = string.Empty,
                Content = BuildMinisterDirectiveContent("reject", actionId)
            };
            MessageSender.Send(msg);
            Debug.LogWarning("[GameIntents] MinisterDirective encoding pending protocol confirmation, using JSON content payload.");
            Debug.Log("[GameIntents] RejectMinisterAction");
        }

        private static void Subscribe(GameStateCache cache)
        {
            cache.OnDomesticSettled += OnDomesticSettled;
            cache.OnCombatSettled += OnCombatSettled;
        }

        private static void Unsubscribe(GameStateCache cache)
        {
            cache.OnDomesticSettled -= OnDomesticSettled;
            cache.OnCombatSettled -= OnCombatSettled;
        }

        private static void OnDomesticSettled(DomesticSettledEvent _)
        {
            if (!ActionLock.IsLocked || _lockSource != LockSource.Domestic)
            {
                return;
            }

            ActionLock.Release();
            _lockSource = LockSource.None;
            Debug.Log("[GameIntents] DomesticSettled -> unlock");
        }

        private static void OnCombatSettled(CombatSettledEvent _)
        {
            if (!ActionLock.IsLocked || _lockSource != LockSource.Combat)
            {
                return;
            }

            ActionLock.Release();
            _lockSource = LockSource.None;
            Debug.Log("[GameIntents] CombatSettled -> unlock");
        }

        private static string BuildMinisterDirectiveContent(string directiveType, string actionId)
        {
            var payload = new MinisterDirectivePayload
            {
                directive_type = directiveType,
                action_id = actionId ?? string.Empty
            };
            return JsonUtility.ToJson(payload);
        }
    }
}
