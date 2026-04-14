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
        [Serializable]
        private sealed class MinisterDirectivePayload
        {
            public string directive_type;
            public string action_id;
        }

        [Serializable]
        private sealed class ExpandTerritoryPayload
        {
            public string unit_id;
            public string center_node_id;
        }

        [Serializable]
        private sealed class TokenBuildPayload
        {
            public string castle_id;
            public string node_id;
            public string building_type;
        }

        private static GameStateCache _cache;
        public static event Action TurnSubmitRequested;

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
            Debug.Log("[GameIntents] Initialize");
        }

        public static void Dispose()
        {
            if (_cache != null)
            {
                Unsubscribe(_cache);
                _cache = null;
            }

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

        public static void BuildToken(string nodeId, string buildingType, string castleId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var normalizedCastleId = castleId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedCastleId))
            {
                var payload = new TokenBuildPayload
                {
                    castle_id = normalizedCastleId,
                    node_id = nodeId ?? string.Empty,
                    building_type = buildingType ?? string.Empty
                };
                MessageSender.SendRaw("MsgTokenBuild", JsonUtility.ToJson(payload));
                Debug.Log("[GameIntents] BuildToken");
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

        public static void ExpandTerritory(string unitId, string centerNodeId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            if (IsCombatPhase())
            {
                DeployTerritoryUnit(unitId, centerNodeId);
                return;
            }

            var payload = new ExpandTerritoryPayload
            {
                unit_id = unitId ?? string.Empty,
                center_node_id = centerNodeId ?? string.Empty
            };
            MessageSender.SendRaw("MsgTokenExpandTerritory", JsonUtility.ToJson(payload));
            Debug.Log("[GameIntents] ExpandTerritory");
        }

        public static void DeployTerritoryUnit(string unitId, string centerNodeId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            SendCombatOrder(unitId, "deploy", centerNodeId, null);
            Debug.Log("[GameIntents] DeployTerritoryUnit");
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

        public static void SubmitTurn()
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            ActionLock.Acquire();
            MessageSender.Send(new MsgSubmitTurn());
            Debug.Log("[GameIntents] SubmitTurn");
            TurnSubmitRequested?.Invoke();
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

            SendCombatOrder(unitId, "move", targetNodeId, null);
            Debug.Log("[GameIntents] MoveUnit");
        }

        public static void PreviewCombatMove(string requestId, string unitId, string targetNodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgCombatPathPreviewRequest
            {
                RequestId = requestId ?? string.Empty,
                UnitId = unitId ?? string.Empty,
                Action = "move",
                TargetNodeId = targetNodeId ?? string.Empty
            };
            MessageSender.Send(msg);
        }

        public static void MicroUnit(string unitId, string targetNodeId)
        {
            MoveUnit(unitId, targetNodeId);
        }

        public static void AttackUnit(string unitId, string targetUnitId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            SendCombatOrder(unitId, "attack", null, targetUnitId);
            Debug.Log("[GameIntents] AttackUnit");
        }

        public static void HoldUnit(string unitId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            SendCombatOrder(unitId, "hold", null, null);
            Debug.Log("[GameIntents] HoldUnit");
        }

        public static void ChargeUnit(string unitId, string targetNodeId, string targetUnitId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            SendCombatOrder(unitId, "charge", targetNodeId, targetUnitId);
            Debug.Log("[GameIntents] ChargeUnit");
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
            cache.OnPhaseChanged += OnPhaseChanged;
            cache.OnGameOver += OnGameOver;
        }

        private static void Unsubscribe(GameStateCache cache)
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
            Debug.Log($"[GameIntents] PhaseChanged -> unlock at {evt.Phase}");
        }

        private static void OnGameOver(GameOverEvent _)
        {
            if (!ActionLock.IsLocked)
            {
                return;
            }

            ActionLock.Release();
            Debug.Log("[GameIntents] GameOver -> unlock");
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

        private static bool IsCombatPhase()
        {
            var cache = _cache ?? GameStateCache.Instance;
            if (cache == null)
            {
                return false;
            }

            var phase = (cache.Phase ?? string.Empty).Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(phase))
            {
                return false;
            }

            // Compatibility: old "combat" + new phase-state names like "combat_planning".
            return string.Equals(phase, "combat", StringComparison.Ordinal)
                   || phase.IndexOf("combat", StringComparison.Ordinal) >= 0;
        }

        private static void SendCombatOrder(string unitId, string action, string targetNodeId, string targetUnitId)
        {
            var msg = new MsgCombatOrder
            {
                UnitId = unitId ?? string.Empty,
                Action = action ?? string.Empty,
                TargetNodeId = targetNodeId ?? string.Empty,
                TargetUnitId = targetUnitId ?? string.Empty
            };
            MessageSender.Send(msg);
        }
    }
}
