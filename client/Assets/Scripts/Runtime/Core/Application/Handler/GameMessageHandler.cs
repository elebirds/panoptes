using Panoptes.Core.Application.Cache;
using Panoptes.Core.Events;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Core.Application.Handler
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

            dispatcher.Register<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Register<MsgPlanningSnapshot>("MsgPlanningSnapshot", OnPlanningSnapshot);
            dispatcher.Register<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", OnPlanningPathPreviewResponse);
            dispatcher.Register<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Register<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Register<MsgResearchResult>("MsgResearchResult", OnResearchResult);
            dispatcher.Register<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", OnSetBuildingRecipeResult);
            dispatcher.Register<MsgTurnReport>("MsgTurnReport", OnTurnReport);
            dispatcher.Register<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Register<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Register<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
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
            dispatcher.Unregister<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Unregister<MsgPlanningSnapshot>("MsgPlanningSnapshot", OnPlanningSnapshot);
            dispatcher.Unregister<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", OnPlanningPathPreviewResponse);
            dispatcher.Unregister<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Unregister<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Unregister<MsgResearchResult>("MsgResearchResult", OnResearchResult);
            dispatcher.Unregister<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", OnSetBuildingRecipeResult);
            dispatcher.Unregister<MsgTurnReport>("MsgTurnReport", OnTurnReport);
            dispatcher.Unregister<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Unregister<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Unregister<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
            dispatcher.Unregister<MsgGameOver>("MsgGameOver", OnGameOver);

            _registered = false;
        }

        private static void OnPlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = GameStateCache.Instance;
            cache?.ApplyPlanningStart(msg);

            Debug.Log(FormatPhaseStartLog($"[Game] 规划阶段开始 turn={msg.Turn} timeout={msg.Timeout}s tokens={msg.Tokens} phase={msg.Phase}"));
        }

        private static void OnPlanningSnapshot(MsgPlanningSnapshot msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyPlanningSnapshot(msg);
            Debug.Log($"[Game] 规划快照 turn={msg.Turn} phase={msg.Phase} unit_orders={msg.UnitOrders.Count}");
        }

        private static void OnTokenResult(MsgTokenResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = GameStateCache.Instance;
            if (msg.Success)
            {
                cache?.UpdateTokens(msg.TokensLeft);
                Debug.Log($"[Game] 令牌操作成功 action={msg.Action} tokens_left={msg.TokensLeft}");
            }
            else
            {
                Debug.LogWarning($"[Game] 令牌操作失败 error={msg.ErrorCode}");
            }

            cache?.PublishTokenResult(new TokenResultEvent
            {
                Success = msg.Success,
                Action = msg.Action,
                TokensLeft = msg.TokensLeft,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty)
            });
        }

        private static void OnRevealResult(MsgRevealResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = GameStateCache.Instance;
            var trueState = NodeMapper.ToDto(msg.TrueState);
            cache?.UpdateNode(trueState);
            cache?.PublishRevealResult(new RevealResultEvent
            {
                NodeID = msg.NodeId,
                TrueState = trueState
            });

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
                GameStateCache.Instance?.PublishMinisterChunk(new MinisterChunkEvent
                {
                    MinisterRole = msg.MinisterRole,
                    Chunk = msg.Chunk,
                    IsFinal = true
                });
                Debug.Log($"[Minister] {msg.MinisterRole} 汇报完成");
                return;
            }

            var chunk = msg.Chunk ?? string.Empty;
            var preview = chunk.Length <= 50 ? chunk : chunk.Substring(0, 50);
            GameStateCache.Instance?.PublishMinisterChunk(new MinisterChunkEvent
            {
                MinisterRole = msg.MinisterRole,
                Chunk = msg.Chunk,
                IsFinal = false
            });
            Debug.Log($"[Minister] {msg.MinisterRole} chunk: {preview}...");
        }

        private static void OnMinisterMetrics(MsgMinisterMetrics msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.PublishMinisterMetrics(new MinisterMetricsEvent
            {
                MinisterRole = msg.MinisterRole,
                Metrics = MinisterMapper.ToDtoList(msg.Metrics)
            });

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

        private static void OnResearchResult(MsgResearchResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                GameStateCache.Instance?.PublishGameError(new GameErrorEvent
                {
                    Code = msg.ErrorCode,
                    Message = msg.TechnologyId
                });
            }

            Debug.Log($"[Game] 研究结果 success={msg.Success} tech={msg.TechnologyId} error={msg.ErrorCode}");
        }

        private static void OnSetBuildingRecipeResult(MsgSetBuildingRecipeResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                GameStateCache.Instance?.PublishGameError(new GameErrorEvent
                {
                    Code = msg.ErrorCode,
                    Message = msg.NodeId
                });
            }

            Debug.Log($"[Game] 配方结果 success={msg.Success} node={msg.NodeId} recipe={msg.RecipeId} error={msg.ErrorCode}");
        }

        private static void OnTurnReport(MsgTurnReport msg)
        {
            if (msg == null)
            {
                return;
            }

            Debug.Log($"[Game] 回合汇总 turn={msg.Turn} summary={msg.Summary}");
        }

        private static void OnTurnSettlement(MsgTurnSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyTurnSettlement(msg);
            Debug.Log($"[Game] 回合结算 turn={msg.Turn} sections={msg.Sections.Count} next_phase={msg.NextPhase}");
        }

        private static void OnPlanningPathPreviewResponse(MsgPlanningPathPreviewResponse msg)
        {
            PlanningDraftCache.EnsureInstance()?.ApplyPreviewResponse(msg);
        }

        private static void OnGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyGameOver(msg);

            Debug.Log($"[Game] 游戏结束 winner={msg.WinnerId} reason={msg.Reason}");
            Debug.Log($"[Game] 叙事: {msg.Narrative}");
        }

        private static string FormatPhaseStartLog(string text)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Panoptes.DebugTools.MessageLogger.WrapPhaseStartColor(text);
#else
            return text;
#endif
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
