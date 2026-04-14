using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Network;
using UnityEngine;
using System.Collections;

namespace Panoptes.DebugTools
{
    public sealed class IntegrationChecker : MonoBehaviour
    {
        private bool _registered;
        private bool _initChecked;
        private bool _planningPhaseChecked;
        private bool _turnSettlementChecked;
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

            _initChecked = true;

            if (cache.Phase == GamePhases.Planning && cache.TokensLeft == 3)
            {
                _planningPhaseChecked = true;
            }
        }

        private void OnPlanningStart(MsgPlanningStart msg)
        {
            StartCoroutine(ValidatePlanningPhaseNextFrame());
        }

        private IEnumerator ValidatePlanningPhaseNextFrame()
        {
            yield return null;

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                Fail("PlanningStart 时 GameStateCache 不存在");
                yield break;
            }

            if (cache.Phase != GamePhases.Planning)
            {
                Fail($"PlanningStart 后 Phase 异常: {cache.Phase}");
                yield break;
            }

            if (cache.TokensLeft != 3)
            {
                Fail($"PlanningStart 后 TokensLeft 异常: {cache.TokensLeft}");
                yield break;
            }

            _planningPhaseChecked = true;
            TryFinalize();
        }

        private void OnTurnSettlement(MsgTurnSettlement msg)
        {
            try
            {
                _turnSettlementChecked = true;
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

            if (_initChecked && _planningPhaseChecked && _turnSettlementChecked)
            {
                _allPassedLogged = true;
                Debug.Log("[Integration] ✓ 所有检查通过，回合链路正常");
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
    }
}
