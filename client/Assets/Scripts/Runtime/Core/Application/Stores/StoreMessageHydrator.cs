using System;
using System.Collections.Generic;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Mapper;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;

namespace Panoptes.Core.Application.Stores
{
    public sealed class StoreMessageHydrator : IDisposable
    {
        private readonly MessageDispatcher _dispatcher;
        private readonly StoreHydrationHelper _helper;
        private readonly GameStateStore _gameStateStore;
        private readonly PlanningDraftStore _planningDraftStore;
        private readonly TurnStore _turnStore;
        private readonly GameChatStore _gameChatStore;
        private readonly GameOverStore _gameOverStore;
        private readonly SettlementStore _settlementStore;
        private readonly GameplayFeedbackStore _feedbackStore;
        private bool _attached;

        public StoreMessageHydrator(
            MessageDispatcher dispatcher,
            StoreHydrationHelper helper,
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            TurnStore turnStore,
            GameChatStore gameChatStore,
            GameOverStore gameOverStore,
            SettlementStore settlementStore,
            GameplayFeedbackStore feedbackStore)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
            _gameChatStore = gameChatStore ?? throw new ArgumentNullException(nameof(gameChatStore));
            _gameOverStore = gameOverStore ?? throw new ArgumentNullException(nameof(gameOverStore));
            _settlementStore = settlementStore ?? throw new ArgumentNullException(nameof(settlementStore));
            _feedbackStore = feedbackStore ?? throw new ArgumentNullException(nameof(feedbackStore));
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            _dispatcher.Register<MsgGameInit>("MsgGameInit", HandleGameInit);
            _dispatcher.Register<MsgPlanningStart>("MsgPlanningStart", HandlePlanningStart);
            _dispatcher.Register<MsgPlanningSnapshot>("MsgPlanningSnapshot", HandlePlanningSnapshot);
            _dispatcher.Register<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", HandlePlanningPathPreviewResponse);
            _dispatcher.Register<MsgBuildStructurePreviewResponse>("MsgBuildStructurePreviewResponse", HandleBuildStructurePreviewResponse);
            _dispatcher.Register<MsgSetBuildingRecipePreviewResponse>("MsgSetBuildingRecipePreviewResponse", HandleSetBuildingRecipePreviewResponse);
            _dispatcher.Register<MsgGameSync>("MsgGameSync", HandleGameSync);
            _dispatcher.Register<MsgTokenResult>("MsgTokenResult", HandleTokenResult);
            _dispatcher.Register<MsgRevealResult>("MsgRevealResult", HandleRevealResult);
            _dispatcher.Register<MsgIssueUnitOrderResult>("MsgIssueUnitOrderResult", HandleIssueUnitOrderResult);
            _dispatcher.Register<MsgResearchResult>("MsgResearchResult", HandleResearchResult);
            _dispatcher.Register<MsgSetPolicyResult>("MsgSetPolicyResult", HandleSetPolicyResult);
            _dispatcher.Register<MsgSetInstitutionLoadoutResult>("MsgSetInstitutionLoadoutResult", HandleSetInstitutionLoadoutResult);
            _dispatcher.Register<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", HandleSetBuildingRecipeResult);
            _dispatcher.Register<MsgBuildStructureResult>("MsgBuildStructureResult", HandleBuildStructureResult);
            _dispatcher.Register<MsgGameChatPosted>("MsgGameChatPosted", HandleGameChatPosted);
            _dispatcher.Register<MsgGameChatSync>("MsgGameChatSync", HandleGameChatSync);
            _dispatcher.Register<MsgGameOver>("MsgGameOver", HandleGameOver);
            _dispatcher.Register<Problem>("Problem", HandleProblem);
            _attached = true;
        }

        public void Dispose()
        {
            Detach();
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            _dispatcher.Unregister<MsgGameInit>("MsgGameInit", HandleGameInit);
            _dispatcher.Unregister<MsgPlanningStart>("MsgPlanningStart", HandlePlanningStart);
            _dispatcher.Unregister<MsgPlanningSnapshot>("MsgPlanningSnapshot", HandlePlanningSnapshot);
            _dispatcher.Unregister<MsgPlanningPathPreviewResponse>("MsgPlanningPathPreviewResponse", HandlePlanningPathPreviewResponse);
            _dispatcher.Unregister<MsgBuildStructurePreviewResponse>("MsgBuildStructurePreviewResponse", HandleBuildStructurePreviewResponse);
            _dispatcher.Unregister<MsgSetBuildingRecipePreviewResponse>("MsgSetBuildingRecipePreviewResponse", HandleSetBuildingRecipePreviewResponse);
            _dispatcher.Unregister<MsgGameSync>("MsgGameSync", HandleGameSync);
            _dispatcher.Unregister<MsgTokenResult>("MsgTokenResult", HandleTokenResult);
            _dispatcher.Unregister<MsgRevealResult>("MsgRevealResult", HandleRevealResult);
            _dispatcher.Unregister<MsgIssueUnitOrderResult>("MsgIssueUnitOrderResult", HandleIssueUnitOrderResult);
            _dispatcher.Unregister<MsgResearchResult>("MsgResearchResult", HandleResearchResult);
            _dispatcher.Unregister<MsgSetPolicyResult>("MsgSetPolicyResult", HandleSetPolicyResult);
            _dispatcher.Unregister<MsgSetInstitutionLoadoutResult>("MsgSetInstitutionLoadoutResult", HandleSetInstitutionLoadoutResult);
            _dispatcher.Unregister<MsgSetBuildingRecipeResult>("MsgSetBuildingRecipeResult", HandleSetBuildingRecipeResult);
            _dispatcher.Unregister<MsgBuildStructureResult>("MsgBuildStructureResult", HandleBuildStructureResult);
            _dispatcher.Unregister<MsgGameChatPosted>("MsgGameChatPosted", HandleGameChatPosted);
            _dispatcher.Unregister<MsgGameChatSync>("MsgGameChatSync", HandleGameChatSync);
            _dispatcher.Unregister<MsgGameOver>("MsgGameOver", HandleGameOver);
            _dispatcher.Unregister<Problem>("Problem", HandleProblem);
            _attached = false;
        }

        public void HandleGameInit(MsgGameInit msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.ToGameState(msg));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.ToTurn(msg));
            _gameChatStore.Clear();
            _gameOverStore.Clear();
            _settlementStore.Clear();
            _feedbackStore.Clear();
        }

        public void HandlePlanningStart(MsgPlanningStart msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergePlanningStart(_gameStateStore.Snapshot, msg));
            if (msg.Snapshot != null)
            {
                _helper.HydratePlanningDraft(StoreHydrationProtocolMapper.ToPlanningDraft(msg.Snapshot));
            }
            _helper.HydrateTurn(StoreHydrationProtocolMapper.ToTurn(msg));
        }

        public void HandlePlanningSnapshot(MsgPlanningSnapshot msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydratePlanningDraft(StoreHydrationProtocolMapper.ToPlanningDraft(msg));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeTurn(_turnStore.Snapshot, msg));
        }

        public void HandlePlanningPathPreviewResponse(MsgPlanningPathPreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydratePlanningDraft(
                StoreHydrationProtocolMapper.MergePathPreview(_planningDraftStore.Snapshot, msg));
        }

        public void HandleBuildStructurePreviewResponse(MsgBuildStructurePreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydratePlanningDraft(
                StoreHydrationProtocolMapper.MergeBuildPreview(_planningDraftStore.Snapshot, msg));
        }

        public void HandleSetBuildingRecipePreviewResponse(MsgSetBuildingRecipePreviewResponse msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydratePlanningDraft(
                StoreHydrationProtocolMapper.MergeRecipePreview(_planningDraftStore.Snapshot, msg));
        }

        public void HandleGameSync(MsgGameSync msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergeGameSync(_gameStateStore.Snapshot, msg));
            if (msg.Snapshot != null)
            {
                _helper.HydratePlanningDraft(StoreHydrationProtocolMapper.ToPlanningDraft(msg.Snapshot));
            }
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeTurn(_turnStore.Snapshot, msg));
            _settlementStore.Replace(SettlementMapper.ToDto(msg));
        }

        public void HandleTokenResult(MsgTokenResult msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergeTokenResult(_gameStateStore.Snapshot, msg));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeTurn(_turnStore.Snapshot, msg));
            if (!msg.Success)
            {
                _feedbackStore.PublishFeedback("token", msg.ErrorCode, string.Empty, false);
            }
        }

        public void HandleRevealResult(MsgRevealResult msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergeRevealResult(_gameStateStore.Snapshot, msg));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeTurn(_turnStore.Snapshot, msg));
        }

        public void HandleGameOver(MsgGameOver msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergeGameOver(_gameStateStore.Snapshot));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeGameOver(_turnStore.Snapshot));
            _gameOverStore.Replace(ToGameOverState(msg, _gameStateStore.Snapshot));
        }

        public void HandleProblem(Problem msg)
        {
            if (msg == null)
            {
                return;
            }

            _feedbackStore.PublishFeedback("problem", msg.Code, msg.Message, false, ToDetailMap(msg.Details));
        }

        public void HandleIssueUnitOrderResult(MsgIssueUnitOrderResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback("unit_order", msg.ErrorCode, string.Empty, false, BuildDetails(
                    ("unit_id", msg.UnitId),
                    ("action", msg.Action),
                    ("target_node_id", msg.TargetNodeId),
                    ("target_unit_id", msg.TargetUnitId)));
            }
        }

        public void HandleResearchResult(MsgResearchResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback("research", msg.ErrorCode, string.Empty, false, BuildDetails(("technology_id", msg.TechnologyId)));
            }
        }

        public void HandleSetPolicyResult(MsgSetPolicyResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback("policy", msg.ErrorCode, string.Empty, false, BuildDetails(("national_policy_id", msg.NationalPolicyId)));
            }
        }

        public void HandleSetInstitutionLoadoutResult(MsgSetInstitutionLoadoutResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback(
                    "institution_loadout",
                    msg.ErrorCode,
                    string.Empty,
                    false,
                    BuildDetails(("policy_ids", string.Join(",", msg.PolicyIds))));
            }
        }

        public void HandleSetBuildingRecipeResult(MsgSetBuildingRecipeResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback("building_recipe", msg.ErrorCode, msg.FeedbackMessage, false, ToDetailMap(msg.FeedbackDetails));
            }
        }

        public void HandleBuildStructureResult(MsgBuildStructureResult msg)
        {
            if (msg != null && !msg.Success)
            {
                _feedbackStore.PublishFeedback("build", msg.ErrorCode, msg.FeedbackMessage, false, ToDetailMap(msg.FeedbackDetails));
            }
        }

        public void HandleGameChatPosted(MsgGameChatPosted msg)
        {
            var entry = GameChatMapper.ToDto(msg?.Entry);
            if (entry != null)
            {
                _gameChatStore.Append(entry);
            }
        }

        public void HandleGameChatSync(MsgGameChatSync msg)
        {
            var entries = new List<GameChatEntryDto>();
            if (msg?.Entries != null)
            {
                for (var i = 0; i < msg.Entries.Count; i++)
                {
                    var entry = GameChatMapper.ToDto(msg.Entries[i]);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
            }

            _gameChatStore.Replace(entries);
        }

        private static GameOverState ToGameOverState(MsgGameOver msg, GameStateStoreState gameState)
        {
            var myPlayerId = gameState?.MyPlayerId ?? string.Empty;
            var winnerId = msg?.WinnerId ?? string.Empty;
            var isWinner = !string.IsNullOrWhiteSpace(winnerId) &&
                           string.Equals(winnerId, myPlayerId, StringComparison.Ordinal);

            return new GameOverState(
                true,
                isWinner,
                winnerId,
                string.Empty,
                msg?.Reason,
                msg?.Narrative);
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

        private static Dictionary<string, string> ToDetailMap(IEnumerable<ProblemDetail> details)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (details == null)
            {
                return result;
            }

            foreach (var detail in details)
            {
                if (detail == null || string.IsNullOrWhiteSpace(detail.Path))
                {
                    continue;
                }

                result[detail.Path] = detail.Detail ?? string.Empty;
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
                if (!string.IsNullOrWhiteSpace(pairs[i].Key))
                {
                    result[pairs[i].Key] = pairs[i].Value ?? string.Empty;
                }
            }

            return result;
        }
    }
}
