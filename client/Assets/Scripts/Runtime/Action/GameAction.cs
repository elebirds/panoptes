using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Events;
using Panoptes.Runtime.Network;
using UnityEngine;

namespace Panoptes.Runtime.Action
{
    public static class GameAction
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
                Debug.LogWarning("[GameAction] Initialize ignored: cache is null.");
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
            Debug.Log("[GameAction] Initialize");
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
            Debug.Log("[GameAction] Dispose");
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
            Debug.Log("[GameAction] SetPolicy");
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
            Debug.Log("[GameAction] BuildToken");
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
            Debug.Log("[GameAction] RevealToken");
        }

        public static void VetoToken(string nodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenVeto
            {
                ActionId = nodeId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameAction] VetoToken");
        }

        public static void AdjustFlow(string fromNodeId, string toNodeId, int delta)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenAdjustFlow
            {
                FromNode = fromNodeId ?? string.Empty,
                ToNode = toNodeId ?? string.Empty,
                Amount = delta
            };
            MessageSender.Send(msg);
            Debug.Log("[GameAction] AdjustFlow");
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
            Debug.Log("[GameAction] SubmitDomestic");
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
            Debug.Log("[GameAction] SetWarZone");
        }

        public static void VetoCombat(string nodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgTokenVetoCombat
            {
                UnitId = nodeId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameAction] VetoCombat");
        }

        public static void MicroUnit(string unitId, string targetNodeId)
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
            Debug.Log("[GameAction] MicroUnit");
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
            Debug.Log("[GameAction] SubmitCombat");
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
            Debug.Log("[GameAction] AcceptMinisterAction");
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
            Debug.Log("[GameAction] RejectMinisterAction");
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
            Debug.Log("[GameAction] DomesticSettled -> unlock");
        }

        private static void OnCombatSettled(CombatSettledEvent _)
        {
            if (!ActionLock.IsLocked || _lockSource != LockSource.Combat)
            {
                return;
            }

            ActionLock.Release();
            _lockSource = LockSource.None;
            Debug.Log("[GameAction] CombatSettled -> unlock");
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
