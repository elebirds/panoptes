using System;
using System.Collections.Generic;
using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
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
                NationalPolicyId = policyType ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] SetPolicy");
        }

        public static void SetInstitutionLoadout(params string[] policyIds)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgSetInstitutionLoadout();
            if (policyIds != null)
            {
                for (var i = 0; i < policyIds.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(policyIds[i]))
                    {
                        continue;
                    }

                    msg.PolicyIds.Add(policyIds[i]);
                }
            }

            MessageSender.Send(msg);
            Debug.Log("[GameIntents] SetInstitutionLoadout");
        }

        public static void BuildToken(string nodeId, string buildingTypeId, string cityId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgBuildStructure
            {
                NodeId = nodeId ?? string.Empty,
                BuildingTypeId = buildingTypeId ?? string.Empty,
                CityId = cityId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] BuildStructure");
        }

        public static void ExpandTerritory(string unitId, string centerNodeId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            IssueUnitOrder(unitId, "settle_city", centerNodeId, null, null);
            Debug.Log("[GameIntents] ExpandTerritory");
        }

        public static void DeployTerritoryUnit(string unitId, string centerNodeId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            IssueUnitOrder(unitId, "settle_city", centerNodeId, null, null);
            Debug.Log("[GameIntents] DeployTerritoryUnit");
        }

        public static void RevealToken(string nodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgRevealNode
            {
                NodeId = nodeId ?? string.Empty
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] RevealNode");
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

            var msg = new MsgSetWarZone
            {
                ZoneId = "frontline",
                Name = "Frontline"
            };
            if (nodeIds != null)
            {
                msg.NodeIds.AddRange(nodeIds);
            }

            MessageSender.Send(msg);
            Debug.Log("[GameIntents] SetWarZone");
        }

        public static void MoveUnit(string unitId, string targetNodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            IssueUnitOrder(unitId, "move", targetNodeId, null, null);
            Debug.Log("[GameIntents] MoveUnit");
        }

        public static void PreviewMove(string requestId, string unitId, string targetNodeId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgPlanningPathPreviewRequest
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

            IssueUnitOrder(unitId, "attack", null, targetUnitId, null);
            Debug.Log("[GameIntents] AttackUnit");
        }

        public static void HoldUnit(string unitId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            IssueUnitOrder(unitId, "hold", null, null, null);
            Debug.Log("[GameIntents] HoldUnit");
        }

        public static void ChargeUnit(string unitId, string targetNodeId, string targetUnitId = null)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            IssueUnitOrder(unitId, "charge", targetNodeId, targetUnitId, null);
            Debug.Log("[GameIntents] ChargeUnit");
        }

        public static void AcceptMinisterAction(string actionId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgSetMinisterDirective
            {
                MinisterRole = string.Empty,
                Content = BuildMinisterDirectiveContent("accept", actionId)
            };
            MessageSender.Send(msg);
            Debug.Log("[GameIntents] AcceptMinisterAction");
        }

        public static void RejectMinisterAction(string actionId)
        {
            if (ActionLock.IsLocked)
            {
                return;
            }

            var msg = new MsgSetMinisterDirective
            {
                MinisterRole = string.Empty,
                Content = BuildMinisterDirectiveContent("reject", actionId)
            };
            MessageSender.Send(msg);
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

        private static void IssueUnitOrder(string unitId, string action, string targetNodeId, string targetUnitId, string secondaryNodeId)
        {
            var msg = new MsgIssueUnitOrder
            {
                UnitId = unitId ?? string.Empty,
                Action = action ?? string.Empty,
                TargetNodeId = targetNodeId ?? string.Empty,
                TargetUnitId = targetUnitId ?? string.Empty,
                SecondaryNodeId = secondaryNodeId ?? string.Empty
            };
            MessageSender.Send(msg);
        }
    }
}
