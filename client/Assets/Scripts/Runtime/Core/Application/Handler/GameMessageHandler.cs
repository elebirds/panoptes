using System;
using System.Collections.Generic;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Feedback;
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
        private MessageDispatcher _dispatcher;
        private GameStateCache _gameStateCache;
        private PlanningDraftCache _planningDraftCache;
        private GameChatCache _gameChatCache;
        private StaticCatalogCache _staticCatalogCache;

        public void UseProjectServices(
            MessageDispatcher dispatcher,
            GameStateCache gameStateCache,
            PlanningDraftCache planningDraftCache,
            GameChatCache gameChatCache,
            StaticCatalogCache staticCatalogCache)
        {
            _dispatcher = dispatcher;
            _gameStateCache = gameStateCache;
            _planningDraftCache = planningDraftCache;
            _gameChatCache = gameChatCache;
            _staticCatalogCache = staticCatalogCache;
            RegisterHandlers();
        }

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
            var dispatcher = _dispatcher;
            if (_registered || dispatcher == null)
            {
                return;
            }

            dispatcher.Register<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Register<MsgPlanningSnapshot>("MsgPlanningSnapshot", OnPlanningSnapshot);
            dispatcher.Register<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", OnPlanningPathPreviewResponse);
            dispatcher.Register<MsgBuildStructurePreviewResponse>("MsgBuildStructurePreviewResponse", OnBuildStructurePreviewResponse);
            dispatcher.Register<MsgSetBuildingRecipePreviewResponse>("MsgSetBuildingRecipePreviewResponse", OnSetBuildingRecipePreviewResponse);
            dispatcher.Register<MsgGameSync>("MsgGameSync", OnGameSync);
            dispatcher.Register<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Register<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Register<MsgIssueUnitOrderResult>("MsgIssueUnitOrderResult", OnIssueUnitOrderResult);
            dispatcher.Register<MsgResearchResult>("MsgResearchResult", HandleResearchResult);
            dispatcher.Register<MsgSetPolicyResult>("MsgSetPolicyResult", HandleSetPolicyResult);
            dispatcher.Register<MsgSetInstitutionLoadoutResult>("MsgSetInstitutionLoadoutResult", HandleSetInstitutionLoadoutResult);
            dispatcher.Register<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", HandleSetBuildingRecipeResult);
            dispatcher.Register<MsgBuildStructureResult>("MsgBuildStructureResult", HandleBuildStructureResult);
            dispatcher.Register<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Register<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Register<MsgGameChatPosted>("MsgGameChatPosted", HandleGameChatPosted);
            dispatcher.Register<MsgGameChatSync>("MsgGameChatSync", OnGameChatSync);
            dispatcher.Register<MsgGameOver>("MsgGameOver", OnGameOver);
            _registered = true;
        }

        private void UnregisterHandlers()
        {
            if (!_registered || _dispatcher == null)
            {
                return;
            }

            var dispatcher = _dispatcher;
            dispatcher.Unregister<MsgPlanningStart>("MsgPlanningStart", OnPlanningStart);
            dispatcher.Unregister<MsgPlanningSnapshot>("MsgPlanningSnapshot", OnPlanningSnapshot);
            dispatcher.Unregister<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", OnPlanningPathPreviewResponse);
            dispatcher.Unregister<MsgBuildStructurePreviewResponse>("MsgBuildStructurePreviewResponse", OnBuildStructurePreviewResponse);
            dispatcher.Unregister<MsgSetBuildingRecipePreviewResponse>("MsgSetBuildingRecipePreviewResponse", OnSetBuildingRecipePreviewResponse);
            dispatcher.Unregister<MsgGameSync>("MsgGameSync", OnGameSync);
            dispatcher.Unregister<MsgTokenResult>("MsgTokenResult", OnTokenResult);
            dispatcher.Unregister<MsgRevealResult>("MsgRevealResult", OnRevealResult);
            dispatcher.Unregister<MsgIssueUnitOrderResult>("MsgIssueUnitOrderResult", OnIssueUnitOrderResult);
            dispatcher.Unregister<MsgResearchResult>("MsgResearchResult", HandleResearchResult);
            dispatcher.Unregister<MsgSetPolicyResult>("MsgSetPolicyResult", HandleSetPolicyResult);
            dispatcher.Unregister<MsgSetInstitutionLoadoutResult>("MsgSetInstitutionLoadoutResult", HandleSetInstitutionLoadoutResult);
            dispatcher.Unregister<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", HandleSetBuildingRecipeResult);
            dispatcher.Unregister<MsgBuildStructureResult>("MsgBuildStructureResult", HandleBuildStructureResult);
            dispatcher.Unregister<MsgMinisterReportChunk>("MsgMinisterReportChunk", OnMinisterReportChunk);
            dispatcher.Unregister<MsgMinisterMetrics>("MsgMinisterMetrics", OnMinisterMetrics);
            dispatcher.Unregister<MsgGameChatPosted>("MsgGameChatPosted", HandleGameChatPosted);
            dispatcher.Unregister<MsgGameChatSync>("MsgGameChatSync", OnGameChatSync);
            dispatcher.Unregister<MsgGameOver>("MsgGameOver", OnGameOver);
            _registered = false;
        }

        private void OnPlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.ApplyPlanningStart(msg);
            Debug.Log(FormatPhaseStartLog($"[Game] 规划阶段开始 turn={msg.Turn} timeout={msg.Timeout}s tokens={msg.Tokens} phase={msg.Phase}"));
        }

        private void OnPlanningSnapshot(MsgPlanningSnapshot msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.ApplyPlanningSnapshot(msg);
            Debug.Log($"[Game] 规划快照 turn={msg.Turn} phase={msg.Phase} unit_orders={msg.UnitOrders.Count}");
        }

        private void OnPlanningPathPreviewResponse(MsgPlanningPathPreviewResponse msg)
        {
            _planningDraftCache?.ApplyPreviewResponse(msg);
            if (msg != null)
            {
                Debug.Log($"[Game] 路径预览 unit={msg.UnitId} target={msg.TargetNodeId} valid={msg.Valid} path_nodes={msg.PathNodeIds.Count} error={msg.ErrorCode}");
            }
        }

        private void OnBuildStructurePreviewResponse(MsgBuildStructurePreviewResponse msg)
        {
            _planningDraftCache?.ApplyBuildPreviewResponse(msg);
            if (msg != null)
            {
                Debug.Log($"[Game] 建造预览 node={msg.NodeId} building={msg.BuildingTypeId} city={msg.CityId} valid={msg.Valid} error={msg.ErrorCode}");
            }
        }

        private void OnSetBuildingRecipePreviewResponse(MsgSetBuildingRecipePreviewResponse msg)
        {
            _planningDraftCache?.ApplyRecipePreviewResponse(msg);
            if (msg != null)
            {
                Debug.Log($"[Game] 配方预览 node={msg.NodeId} recipe={msg.RecipeId} valid={msg.Valid} error={msg.ErrorCode}");
            }
        }

        private void OnGameSync(MsgGameSync msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.ApplyGameSync(msg);
            Debug.Log($"[Game] 游戏同步 turn={msg.Turn} phase={msg.Phase} next_phase={msg.NextPhase} events={msg.Events.Count}");
            for (var i = 0; i < msg.Events.Count; i++)
            {
                var evt = msg.Events[i];
                if (evt == null)
                {
                    continue;
                }

                Debug.Log($"  event={evt.Kind} channel={evt.Channel}");
            }

            // Combat diagnostics: quickly surface whether structure damage actually arrived from server.
            var settlement = SettlementMapper.ToDto(msg);
            if (settlement?.Sections == null)
            {
                return;
            }

            for (var sectionIndex = 0; sectionIndex < settlement.Sections.Count; sectionIndex++)
            {
                var section = settlement.Sections[sectionIndex];
                if (section?.Events == null)
                {
                    continue;
                }

                for (var eventIndex = 0; eventIndex < section.Events.Count; eventIndex++)
                {
                    var evt = section.Events[eventIndex];
                    if (evt == null)
                    {
                        continue;
                    }

                    var eventType = evt.Type ?? string.Empty;
                    if (!string.Equals(eventType, "city_core_damaged", StringComparison.Ordinal) &&
                        !string.Equals(eventType, "building_damaged", StringComparison.Ordinal) &&
                        !string.Equals(eventType, "unit_died", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Debug.Log($"[Game][SyncEvent] section={section.Section} type={eventType} unit={evt.UnitId} node={evt.NodeId} damage={evt.Damage} hp_after={evt.HpAfter} killer={evt.KillerId}");
                }
            }
        }

        private void OnTokenResult(MsgTokenResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = _gameStateCache;
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

        private void OnRevealResult(MsgRevealResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var cache = _gameStateCache;
            var trueState = NodeMapper.ToDto(msg.TrueState, _staticCatalogCache);
            cache?.UpdateNode(trueState);
            cache?.PublishRevealResult(new RevealResultEvent
            {
                NodeID = msg.NodeId,
                TrueState = trueState
            });
            Debug.Log($"[Game] 节点侦察完成 id={msg.NodeId}");
        }

        private void OnIssueUnitOrderResult(MsgIssueUnitOrderResult msg)
        {
            if (msg == null)
            {
                return;
            }

            PublishPlanningCommandResult(new PlanningCommandResultEvent
            {
                CommandType = "unit_order",
                Action = msg.Action ?? string.Empty,
                Success = msg.Success,
                PrimaryId = msg.UnitId ?? string.Empty,
                SecondaryId = msg.TargetNodeId ?? string.Empty,
                TertiaryId = msg.TargetUnitId ?? string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = ResolveFailureMessage(msg.Success, string.Empty, msg.ErrorCode),
                Details = BuildDetails(
                    ("unit_id", msg.UnitId),
                    ("action", msg.Action),
                    ("target_node_id", msg.TargetNodeId),
                    ("target_unit_id", msg.TargetUnitId))
            });

            if (!msg.Success)
            {
                PublishGameError(msg.ErrorCode, ResolveFailureMessage(false, string.Empty, msg.ErrorCode), BuildDetails(
                    ("unit_id", msg.UnitId),
                    ("action", msg.Action),
                    ("target_node_id", msg.TargetNodeId),
                    ("target_unit_id", msg.TargetUnitId)));
                Debug.LogWarning($"[Game] 单位命令失败 unit={msg.UnitId} action={msg.Action} node={msg.TargetNodeId} target={msg.TargetUnitId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 单位命令草案已接受 unit={msg.UnitId} action={msg.Action} node={msg.TargetNodeId} target={msg.TargetUnitId}");
        }

        private void HandleResearchResult(MsgResearchResult msg)
        {
            PublishResearchResult(_gameStateCache, msg);
        }

        private static void OnResearchResult(MsgResearchResult msg)
        {
            PublishResearchResult(GameStateCache.Instance, msg);
        }

        private static void PublishResearchResult(GameStateCache cache, MsgResearchResult msg)
        {
            if (msg == null)
            {
                return;
            }

            PublishPlanningCommandResult(cache, new PlanningCommandResultEvent
            {
                CommandType = "research",
                Action = "set_research",
                Success = msg.Success,
                PrimaryId = msg.TechnologyId ?? string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = ResolveFailureMessage(msg.Success, string.Empty, msg.ErrorCode),
                Details = BuildDetails(("technology_id", msg.TechnologyId))
            });

            if (!msg.Success)
            {
                PublishGameError(cache, msg.ErrorCode, ResolveFailureMessage(false, string.Empty, msg.ErrorCode), BuildDetails(("technology_id", msg.TechnologyId)));
                Debug.LogWarning($"[Game] 研究目标设置失败 tech={msg.TechnologyId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 研究目标草案已接受 tech={msg.TechnologyId}");
        }

        private void HandleSetPolicyResult(MsgSetPolicyResult msg)
        {
            PublishSetPolicyResult(_gameStateCache, msg);
        }

        private static void OnSetPolicyResult(MsgSetPolicyResult msg)
        {
            PublishSetPolicyResult(GameStateCache.Instance, msg);
        }

        private static void PublishSetPolicyResult(GameStateCache cache, MsgSetPolicyResult msg)
        {
            if (msg == null)
            {
                return;
            }

            PublishPlanningCommandResult(cache, new PlanningCommandResultEvent
            {
                CommandType = "policy",
                Action = "set_policy",
                Success = msg.Success,
                PrimaryId = msg.NationalPolicyId ?? string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = ResolveFailureMessage(msg.Success, string.Empty, msg.ErrorCode),
                Details = BuildDetails(("national_policy_id", msg.NationalPolicyId))
            });

            if (!msg.Success)
            {
                PublishGameError(cache, msg.ErrorCode, ResolveFailureMessage(false, string.Empty, msg.ErrorCode), BuildDetails(("national_policy_id", msg.NationalPolicyId)));
                Debug.LogWarning($"[Game] 国策设置失败 policy={msg.NationalPolicyId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 国策草案已接受 policy={msg.NationalPolicyId}");
        }

        private void HandleSetInstitutionLoadoutResult(MsgSetInstitutionLoadoutResult msg)
        {
            PublishSetInstitutionLoadoutResult(_gameStateCache, msg);
        }

        private static void OnSetInstitutionLoadoutResult(MsgSetInstitutionLoadoutResult msg)
        {
            PublishSetInstitutionLoadoutResult(GameStateCache.Instance, msg);
        }

        private static void PublishSetInstitutionLoadoutResult(GameStateCache cache, MsgSetInstitutionLoadoutResult msg)
        {
            if (msg == null)
            {
                return;
            }

            PublishPlanningCommandResult(cache, new PlanningCommandResultEvent
            {
                CommandType = "institution_loadout",
                Action = "set_institution_loadout",
                Success = msg.Success,
                PrimaryId = msg.PolicyIds.Count > 0 ? msg.PolicyIds[0] : string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = ResolveFailureMessage(msg.Success, string.Empty, msg.ErrorCode),
                Details = BuildDetails(("policy_ids", string.Join(",", msg.PolicyIds))),
                RelatedIds = new System.Collections.Generic.List<string>(msg.PolicyIds)
            });

            if (!msg.Success)
            {
                PublishGameError(cache, msg.ErrorCode, ResolveFailureMessage(false, string.Empty, msg.ErrorCode), BuildDetails(("policy_ids", string.Join(",", msg.PolicyIds))));
                Debug.LogWarning($"[Game] 制度装填失败 policies={string.Join(",", msg.PolicyIds)} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 制度装填草案已接受 policies={string.Join(",", msg.PolicyIds)}");
        }

        private void HandleSetBuildingRecipeResult(MsgSetBuildingRecipeResult msg)
        {
            PublishSetBuildingRecipeResult(_gameStateCache, msg);
        }

        private static void OnSetBuildingRecipeResult(MsgSetBuildingRecipeResult msg)
        {
            PublishSetBuildingRecipeResult(GameStateCache.Instance, msg);
        }

        private static void PublishSetBuildingRecipeResult(GameStateCache cache, MsgSetBuildingRecipeResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var details = ToDetailMap(msg.FeedbackDetails);
            var message = ResolveFailureMessage(msg.Success, msg.FeedbackMessage, msg.ErrorCode);
            PublishPlanningCommandResult(cache, new PlanningCommandResultEvent
            {
                CommandType = "building_recipe",
                Action = "set_building_recipe",
                Success = msg.Success,
                PrimaryId = msg.NodeId ?? string.Empty,
                SecondaryId = msg.RecipeId ?? string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = message,
                Details = details
            });

            if (!msg.Success)
            {
                PublishGameError(cache, msg.ErrorCode, message, details);
                Debug.LogWarning($"[Game] 生产配方设置失败 node={msg.NodeId} recipe={msg.RecipeId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 生产配方已设置 node={msg.NodeId} recipe={msg.RecipeId}");
        }

        private void HandleBuildStructureResult(MsgBuildStructureResult msg)
        {
            PublishBuildStructureResult(_gameStateCache, msg);
        }

        private static void OnBuildStructureResult(MsgBuildStructureResult msg)
        {
            PublishBuildStructureResult(GameStateCache.Instance, msg);
        }

        private static void PublishBuildStructureResult(GameStateCache cache, MsgBuildStructureResult msg)
        {
            if (msg == null)
            {
                return;
            }

            var details = ToDetailMap(msg.FeedbackDetails);
            var message = ResolveFailureMessage(msg.Success, msg.FeedbackMessage, msg.ErrorCode);
            PublishPlanningCommandResult(cache, new PlanningCommandResultEvent
            {
                CommandType = "build",
                Action = "build_structure",
                Success = msg.Success,
                PrimaryId = msg.NodeId ?? string.Empty,
                SecondaryId = msg.BuildingTypeId ?? string.Empty,
                TertiaryId = msg.CityId ?? string.Empty,
                ErrorCode = msg.Success ? string.Empty : (msg.ErrorCode ?? string.Empty),
                Message = message,
                Details = details
            });

            if (!msg.Success)
            {
                PublishGameError(cache, msg.ErrorCode, message, details);
                Debug.LogWarning($"[Game] 建筑建造失败 node={msg.NodeId} building={msg.BuildingTypeId} city={msg.CityId} error={msg.ErrorCode}");
                return;
            }

            Debug.Log($"[Game] 建筑建造草案已记录 node={msg.NodeId} building={msg.BuildingTypeId} city={msg.CityId}");
        }

        private void OnMinisterReportChunk(MsgMinisterReportChunk msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.PublishMinisterChunk(new MinisterChunkEvent
            {
                MinisterRole = msg.MinisterRole,
                Chunk = msg.Chunk,
                IsFinal = msg.IsFinal
            });
        }

        private void OnMinisterMetrics(MsgMinisterMetrics msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.PublishMinisterMetrics(new MinisterMetricsEvent
            {
                MinisterRole = msg.MinisterRole,
                Metrics = MinisterMapper.ToDtoList(msg.Metrics)
            });
        }

        private void HandleGameChatPosted(MsgGameChatPosted msg)
        {
            PublishGameChatPosted(_gameChatCache, msg);
        }

        private static void OnGameChatPosted(MsgGameChatPosted msg)
        {
            PublishGameChatPosted(GameChatCache.Instance, msg);
        }

        private static void PublishGameChatPosted(GameChatCache cache, MsgGameChatPosted msg)
        {
            if (msg == null)
            {
                return;
            }

            cache?.ApplyPosted(msg);
            var entry = msg.Entry;
            Debug.Log($"[Game] 聊天表情 sender={entry?.SenderPlayerId} turn={entry?.Turn} phase={entry?.Phase} payload={entry?.Payload?.BodyCase}");
        }

        private void OnGameChatSync(MsgGameChatSync msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameChatCache?.ApplySync(msg);
            Debug.Log($"[Game] 聊天同步 entries={msg.Entries.Count}");
        }

        private void OnGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            _gameStateCache?.ApplyGameOver(msg);
            Debug.Log($"[Game] 游戏结束 winner={msg.WinnerId} reason={msg.Reason}");
        }

        private void PublishGameError(string code, string message, Dictionary<string, string> details = null)
        {
            _gameStateCache?.PublishGameError(new GameErrorEvent
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                Details = details
            });
        }

        private static void PublishGameError(GameStateCache cache, string code, string message, Dictionary<string, string> details = null)
        {
            cache?.PublishGameError(new GameErrorEvent
            {
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                Details = details
            });
        }

        private static string ResolveFailureMessage(bool success, string serverMessage, string code)
        {
            return success ? string.Empty : GameplayFeedbackText.ResolveMessage(serverMessage, code);
        }

        private static Dictionary<string, string> ToDetailMap(IEnumerable<FeedbackDetail> details)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (details == null)
            {
                return result;
            }

            foreach (var detail in details)
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.Key))
                {
                    continue;
                }

                result[detail.Key] = detail.Value ?? string.Empty;
            }

            return result;
        }

        private static Dictionary<string, string> BuildDetails(params (string Key, string Value)[] pairs)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (pairs == null)
            {
                return result;
            }

            for (var i = 0; i < pairs.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(pairs[i].Key))
                {
                    continue;
                }

                result[pairs[i].Key] = pairs[i].Value ?? string.Empty;
            }

            return result;
        }

        private void PublishPlanningCommandResult(PlanningCommandResultEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            _gameStateCache?.PublishPlanningCommandResult(evt);
        }

        private static void PublishPlanningCommandResult(GameStateCache cache, PlanningCommandResultEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            cache?.PublishPlanningCommandResult(evt);
        }

        private static string FormatPhaseStartLog(string text)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || PANOPTES_DEBUG_PANEL
            return Panoptes.DebugTools.MessageLogger.WrapPhaseStartColor(text);
#else
            return text;
#endif
        }
    }
}
