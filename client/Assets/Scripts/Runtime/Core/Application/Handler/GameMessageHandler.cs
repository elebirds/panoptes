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
            dispatcher.Register<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
            dispatcher.Register<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Register<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Register<MsgResearchResult>("MsgResearchResult", OnResearchResult);
            dispatcher.Register<MsgSetPolicyResult>("MsgSetPolicyResult", OnSetPolicyResult);
            dispatcher.Register<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", OnSetBuildingRecipeResult);
            dispatcher.Register<MsgBuildStructureResult>("MsgBuildStructureResult", OnBuildStructureResult);
            dispatcher.Register<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Register<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
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
            dispatcher.Unregister<MsgTurnSettlement>("MsgTurnSettlement", OnTurnSettlement);
            dispatcher.Unregister<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Unregister<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Unregister<MsgResearchResult>("MsgResearchResult", OnResearchResult);
            dispatcher.Unregister<MsgSetPolicyResult>("MsgSetPolicyResult", OnSetPolicyResult);
            dispatcher.Unregister<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", OnSetBuildingRecipeResult);
            dispatcher.Unregister<MsgBuildStructureResult>("MsgBuildStructureResult", OnBuildStructureResult);
            dispatcher.Unregister<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Unregister<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Unregister<MsgGameOver>("MsgGameOver", OnGameOver);
            _registered = false;
        }

        private static void OnPlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyPlanningStart(msg);
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

        private static void OnPlanningPathPreviewResponse(MsgPlanningPathPreviewResponse msg)
        {
            PlanningDraftCache.EnsureInstance()?.ApplyPreviewResponse(msg);
            if (msg != null)
            {
                Debug.Log($"[Game] 路径预览 unit={msg.UnitId} target={msg.TargetNodeId} valid={msg.Valid} path_nodes={msg.PathNodeIds.Count} error={msg.ErrorCode}");
            }
        }

        private static void OnTurnSettlement(MsgTurnSettlement msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyTurnSettlement(msg);
            Debug.Log($"[Game] 回合结算 turn={msg.Turn} phase={msg.Phase} next_phase={msg.NextPhase} sections={msg.Sections.Count}");
            for (var i = 0; i < msg.Sections.Count; i++)
            {
                var section = msg.Sections[i];
                if (section == null)
                {
                    continue;
                }

                Debug.Log($"  section={section.Section} events={section.Events.Count}");
            }
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
                Debug.Log($"[Game] 指令成功 action={msg.Action} tokens_left={msg.TokensLeft}");
            }
            else
            {
                Debug.LogWarning($"[Game] 指令失败 error={msg.ErrorCode}");
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
            Debug.Log($"[Game] 节点侦察完成 id={msg.NodeId}");
        }

        private static void OnResearchResult(MsgResearchResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                PublishGameError(msg.ErrorCode, msg.TechnologyId);
                Debug.LogWarning($"[Game] 研究目标设置失败 tech={msg.TechnologyId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 研究目标草案已接受 tech={msg.TechnologyId}");
        }

        private static void OnSetPolicyResult(MsgSetPolicyResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                PublishGameError(msg.ErrorCode, msg.NationalPolicyId);
                Debug.LogWarning($"[Game] 国策设置失败 policy={msg.NationalPolicyId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 国策草案已接受 policy={msg.NationalPolicyId}");
        }

        private static void OnSetBuildingRecipeResult(MsgSetBuildingRecipeResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                PublishGameError(msg.ErrorCode, $"{msg.NodeId}:{msg.RecipeId}");
                Debug.LogWarning($"[Game] 生产配方设置失败 node={msg.NodeId} recipe={msg.RecipeId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 生产配方已设置 node={msg.NodeId} recipe={msg.RecipeId}");
        }

        private static void OnBuildStructureResult(MsgBuildStructureResult msg)
        {
            if (msg == null)
            {
                return;
            }

            if (!msg.Success)
            {
                PublishGameError(msg.ErrorCode, $"{msg.NodeId}:{msg.BuildingTypeId}:{msg.CityId}");
                Debug.LogWarning($"[Game] 建筑建造失败 node={msg.NodeId} building={msg.BuildingTypeId} city={msg.CityId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 建筑建造已排队 node={msg.NodeId} building={msg.BuildingTypeId} city={msg.CityId}");
        }

        private static void OnMinisterReportChunk(MsgMinisterReportChunk msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.PublishMinisterChunk(new MinisterChunkEvent
            {
                MinisterRole = msg.MinisterRole,
                Chunk = msg.Chunk,
                IsFinal = msg.IsFinal
            });
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
        }

        private static void OnGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            GameStateCache.Instance?.ApplyGameOver(msg);
            Debug.Log($"[Game] 游戏结束 winner={msg.WinnerId} reason={msg.Reason}");
        }

        private static void PublishGameError(string code, string message)
        {
            GameStateCache.Instance?.PublishGameError(new GameErrorEvent
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty
            });
        }

        private static string FormatPhaseStartLog(string text)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return Panoptes.DebugTools.MessageLogger.WrapPhaseStartColor(text);
#else
            return text;
#endif
        }
    }
}
