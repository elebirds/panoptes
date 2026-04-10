using Panoptes.Protocol.V1;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Infrastructure.Network;
using UnityEngine;
using System.Collections;

namespace Panoptes.DebugTools
{
    public sealed class IntegrationChecker : MonoBehaviour
    {
        private bool _registered;
        private bool _initChecked;
        private bool _domesticPhaseChecked;
        private bool _domesticSettlementChecked;
        private bool _combatSettlementChecked;
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
            dispatcher.Register<MsgDomesticPhaseStart>("MsgDomesticPhaseStart", OnDomesticPhaseStart);
            dispatcher.Register<MsgDomesticSettlement>("MsgDomesticSettlement", OnDomesticSettlement);
            dispatcher.Register<MsgCombatSettlement>("MsgCombatSettlement", OnCombatSettlement);
            _registered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_registered || MessageDispatcher.Instance == null)
            {
                return;
            }

            var dispatcher = MessageDispatcher.Instance;
            dispatcher.Unregister<MsgDomesticPhaseStart>("MsgDomesticPhaseStart", OnDomesticPhaseStart);
            dispatcher.Unregister<MsgDomesticSettlement>("MsgDomesticSettlement", OnDomesticSettlement);
            dispatcher.Unregister<MsgCombatSettlement>("MsgCombatSettlement", OnCombatSettlement);
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

            if (cache.Phase == "domestic" && cache.TokensLeft == 3)
            {
                _domesticPhaseChecked = true;
            }
        }

        private void OnDomesticPhaseStart(MsgDomesticPhaseStart msg)
        {
            StartCoroutine(ValidateDomesticPhaseNextFrame());
        }

        private IEnumerator ValidateDomesticPhaseNextFrame()
        {
            yield return null;

            var cache = GameStateCache.Instance;
            if (cache == null)
            {
                Fail("DomesticPhaseStart 时 GameStateCache 不存在");
                yield break;
            }

            if (cache.Phase != "domestic")
            {
                Fail($"DomesticPhaseStart 后 Phase 异常: {cache.Phase}");
                yield break;
            }

            if (cache.TokensLeft != 3)
            {
                Fail($"DomesticPhaseStart 后 TokensLeft 异常: {cache.TokensLeft}");
                yield break;
            }

            _domesticPhaseChecked = true;
            TryFinalize();
        }

        private void OnDomesticSettlement(MsgDomesticSettlement msg)
        {
            try
            {
                _domesticSettlementChecked = true;
                Debug.Log("[Check] ✓ DomesticSettlement 处理正常");
                TryFinalize();
            }
            catch (System.Exception e)
            {
                Fail($"DomesticSettlement 处理异常: {e.Message}");
            }
        }

        private void OnCombatSettlement(MsgCombatSettlement msg)
        {
            try
            {
                _combatSettlementChecked = true;
                Debug.Log("[Check] ✓ CombatSettlement 处理正常");
                TryFinalize();
            }
            catch (System.Exception e)
            {
                Fail($"CombatSettlement 处理异常: {e.Message}");
            }
        }

        private void TryFinalize()
        {
            if (_failed || _allPassedLogged)
            {
                return;
            }

            if (_initChecked && _domesticPhaseChecked && _domesticSettlementChecked && _combatSettlementChecked)
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
