using System.Collections;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.DebugTools
{
    public sealed class IntegrationChecker : MonoBehaviour
    {
        private bool _registered;
        private bool _initChecked;
        private bool _planningChecked;
        private bool _settlementChecked;
        private bool _allPassedLogged;
        private bool _failed;

        private void Awake()
        {
            RegisterHandlers();
            CheckGameInitState();
            TryFinalize();
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private void RegisterHandlers()
        {
            if (_registered || MessageDispatcher.Instance == null)
            {
                return;
            }

            var dispatcher = MessageDispatcher.Instance;
            dispatcher.Register<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Register<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
            _registered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_registered || MessageDispatcher.Instance == null)
            {
                return;
            }

            var dispatcher = MessageDispatcher.Instance;
            dispatcher.Unregister<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Unregister<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
            _registered = false;
        }

        private void CheckGameInitState()
        {
            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                Fail("GameStateCache 不存在");
                return;
            }

            if (cache.Nodes == null || cache.Nodes.Count <= 0)
            {
                Fail("MsgGameInit 后 Nodes 为空");
                return;
            }

            if (cache.MyPlayer == null)
            {
                Fail("MsgGameInit 后 MyPlayer 为空");
                return;
            }

            if (cache.Turn != 1)
            {
                Fail($"MsgGameInit 后 Turn 异常: {cache.Turn}");
                return;
            }

            var localPlayerId = NormalizePlayerId(cache.MyPlayerID, cache.MyPlayer.Id);
            if (string.IsNullOrWhiteSpace(localPlayerId))
            {
                Fail("MsgGameInit 后缺少本地玩家标识");
                return;
            }

            var ownedCityCoreCount = 0;
            foreach (var node in cache.Nodes.Values)
            {
                if (node == null || !string.Equals(Normalize(node.BuildingType), "city_core", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var ownerId = NormalizePlayerId(node.Owner, node.TerritoryOwner);
                if (string.Equals(ownerId, localPlayerId, System.StringComparison.Ordinal))
                {
                    ownedCityCoreCount++;
                }
            }

            if (ownedCityCoreCount <= 0)
            {
                Fail("MsgGameInit 后缺少己方 city_core");
                return;
            }

            _initChecked = true;
            if (cache.Phase == "planning" && cache.TokensLeft == 3)
            {
                _planningChecked = true;
            }
        }

        private void OnPlanningStart(MsgPlanningStart _)
        {
            StartCoroutine(ValidatePlanningStartNextFrame());
        }

        private IEnumerator ValidatePlanningStartNextFrame()
        {
            yield return null;

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                Fail("PlanningStart 时 GameStateCache 不存在");
                yield break;
            }

            if (cache.Phase != "planning")
            {
                Fail($"PlanningStart 后 Phase 异常: {cache.Phase}");
                yield break;
            }

            _planningChecked = true;
            TryFinalize();
        }

        private void OnTurnSettlement(MsgTurnSettlement msg)
        {
            try
            {
                if (msg == null || msg.Sections == null)
                {
                    Fail("TurnSettlement 为空");
                    return;
                }

                _settlementChecked = true;
                Debug.Log("[Check] ✓ TurnSettlement 处理正常");
                TryFinalize();
            }
            catch (System.Exception e)
            {
                Fail($"TurnSettlement 处理异常: {e.Message}");
            }
        }

        private void TryFinalize()
        {
            if (_failed || _allPassedLogged)
            {
                return;
            }

            if (_initChecked && _planningChecked && _settlementChecked)
            {
                _allPassedLogged = true;
                Debug.Log("[Integration] ✓ 所有检查通过，Turn V2 链路正常");
            }
        }

        private void Fail(string reason)
        {
            if (_failed)
            {
                return;
            }

            _failed = true;
            Debug.LogError($"[Integration] ✗ 检查失败: {reason}");
        }

        private static string NormalizePlayerId(string primary, string fallback)
        {
            var normalizedPrimary = Normalize(primary);
            if (!string.IsNullOrEmpty(normalizedPrimary))
            {
                return normalizedPrimary;
            }

            return Normalize(fallback);
        }

        private static string Normalize(string value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
