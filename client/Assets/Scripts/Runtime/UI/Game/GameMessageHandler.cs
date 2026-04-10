using System;
using Panoptes.Protocol.V1;
using Panoptes.Runtime.Cache;
using Panoptes.Runtime.Network;
using UnityEngine;

namespace Panoptes.Runtime.UI.Game
{
    public sealed class GameMessageHandler : MonoBehaviour
    {
        private bool _registered;

        private void Awake()
        {
            RegisterHandlers();
        }

        private void OnDestroy()
        {
            UnregisterHandlers();
        }

        private void RegisterHandlers()
        {
            var dispatcher = MessageDispatcher.Instance;
            if (_registered || dispatcher == null)
            {
                return;
            }

            dispatcher.Register<MsgDomesticPhaseStart>("MsgDomesticPhaseStart", OnDomesticPhaseStart);
            dispatcher.Register<MsgCombatPhaseStart>("MsgCombatPhaseStart", OnCombatPhaseStart);
            dispatcher.Register<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Register<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Register<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Register<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Register<MsgMinisterAction>("MsgMinisterAction", OnMinisterAction);
            dispatcher.Register<MsgDomesticSettlement>("MsgDomesticSettlement", OnDomesticSettlement);
            dispatcher.Register<MsgCombatSettlement>("MsgCombatSettlement", OnCombatSettlement);
            dispatcher.Register<MsgGameOver>("MsgGameOver", OnGameOver);

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
            dispatcher.Unregister<MsgCombatPhaseStart>("MsgCombatPhaseStart", OnCombatPhaseStart);
            dispatcher.Unregister<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Unregister<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Unregister<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Unregister<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Unregister<MsgMinisterAction>("MsgMinisterAction", OnMinisterAction);
            dispatcher.Unregister<MsgDomesticSettlement>("MsgDomesticSettlement", OnDomesticSettlement);
            dispatcher.Unregister<MsgCombatSettlement>("MsgCombatSettlement", OnCombatSettlement);
            dispatcher.Unregister<MsgGameOver>("MsgGameOver", OnGameOver);

            _registered = false;
        }

        private static void OnDomesticPhaseStart(MsgDomesticPhaseStart msg)
        {
            var cache = GameStateCache.Instance;
            cache?.ApplyDomesticPhaseStart(msg);
            Debug.Log($"[Game] 内政阶段开始 turn={msg.Turn} timeout={msg.Timeout}s tokens={msg.Tokens}");
        }

        private static void OnCombatPhaseStart(MsgCombatPhaseStart msg)
        {
            var cache = GameStateCache.Instance;
            cache?.ApplyCombatPhaseStart(msg);
            Debug.Log($"[Game] 战斗阶段开始 timeout={msg.Timeout}s");
        }

        private static void OnTokenResult(MsgTokenResult msg)
        {
            if (msg.Success)
            {
                GameStateCache.Instance?.UpdateTokens(msg.TokensLeft);
                Debug.Log($"[Game] 令牌操作成功 action={msg.Action} tokens_left={msg.TokensLeft}");
            }
            else
            {
                Debug.LogWarning($"[Game] 令牌操作失败 error={msg.ErrorCode}");
            }
        }

        private static void OnRevealResult(MsgRevealResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = GameStateCache.Instance;
            cache?.UpdateNode(msg.TrueState);
            cache?.UpdateTokens(msg.TokensLeft);

            var owner = msg.TrueState != null ? msg.TrueState.Owner : string.Empty;
            var buildingType = msg.TrueState != null ? msg.TrueState.BuildingType : string.Empty;
            Debug.Log($"[Game] 节点真实状态 id={msg.NodeId} owner={owner} building={buildingType}");
        }

        private static void OnMinisterReportChunk(MsgMinisterReportChunk msg)
        {
            if (msg == null)
            {
                return;
            }

            if (msg.IsFinal)
            {
                Debug.Log($"[Minister] {msg.MinisterRole} 汇报完成");
                return;
            }

            var chunk = msg.Chunk ?? string.Empty;
            var preview = chunk.Length <= 50 ? chunk : chunk.Substring(0, 50);
            Debug.Log($"[Minister] {msg.MinisterRole} chunk: {preview}...");
        }

        private static void OnMinisterMetrics(MsgMinisterMetrics msg)
        {
            if (msg == null)
            {
                return;
            }

            Debug.Log($"[Minister] {msg.MinisterRole} 数值轨 count={msg.Metrics.Count}");
            for (var i = 0; i < msg.Metrics.Count; i++)
            {
                var metric = msg.Metrics[i];
                if (metric == null)
                {
                    continue;
                }

                Debug.Log($"  {metric.Label}: {metric.Value} trend={metric.Trend} confidence={metric.Confidence}");
            }
        }

        private static void OnMinisterAction(MsgMinisterAction msg)
        {
            if (msg == null)
            {
                return;
            }

            Debug.Log($"[Minister] {msg.Minister} 自主行动 action_id={msg.ActionId} actions={msg.Actions.Count}");
            for (var i = 0; i < msg.Actions.Count; i++)
            {
                var action = msg.Actions[i];
                if (action == null)
                {
                    continue;
                }

                Debug.Log($"  action={action.Type} params={JoinMap(action.Params)}");
            }
        }

        private static void OnDomesticSettlement(MsgDomesticSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyDomesticSettlement(msg);
            Debug.Log($"[Game] 内政结算 changes={msg.Changes.Count}");
            for (var i = 0; i < msg.Changes.Count; i++)
            {
                var change = msg.Changes[i];
                if (change == null)
                {
                    continue;
                }

                Debug.Log($"  {change.Type}: {JoinMap(change.Data)}");
            }
        }

        private static void OnCombatSettlement(MsgCombatSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyCombatSettlement(msg);
            Debug.Log($"[Game] 战斗结算 events={msg.Events.Count}");
            for (var i = 0; i < msg.Events.Count; i++)
            {
                var evt = msg.Events[i];
                if (evt == null)
                {
                    continue;
                }

                Debug.Log($"  {evt.Type}");
            }
        }

        private static void OnGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            Debug.Log($"[Game] 游戏结束 winner={msg.WinnerId} reason={msg.Reason}");
            Debug.Log($"[Game] 叙事: {msg.Narrative}");
        }

        private static string JoinMap(Google.Protobuf.Collections.MapField<string, string> map)
        {
            if (map == null || map.Count == 0)
            {
                return string.Empty;
            }

            var pairs = new string[map.Count];
            var index = 0;
            foreach (var pair in map)
            {
                pairs[index++] = $"{pair.Key}={pair.Value}";
            }

            return string.Join(",", pairs);
        }
    }
}
