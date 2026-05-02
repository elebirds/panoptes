using System;
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
        private bool _attached;

        public StoreMessageHydrator(
            MessageDispatcher dispatcher,
            StoreHydrationHelper helper,
            GameStateStore gameStateStore,
            PlanningDraftStore planningDraftStore,
            TurnStore turnStore)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _gameStateStore = gameStateStore ?? throw new ArgumentNullException(nameof(gameStateStore));
            _planningDraftStore = planningDraftStore ?? throw new ArgumentNullException(nameof(planningDraftStore));
            _turnStore = turnStore ?? throw new ArgumentNullException(nameof(turnStore));
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
            _dispatcher.Register<MsgGameOver>("MsgGameOver", HandleGameOver);
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
            _dispatcher.Unregister<MsgGameOver>("MsgGameOver", HandleGameOver);
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
        }

        public void HandleTokenResult(MsgTokenResult msg)
        {
            if (msg == null)
            {
                return;
            }

            _helper.HydrateGameState(StoreHydrationProtocolMapper.MergeTokenResult(_gameStateStore.Snapshot, msg));
            _helper.HydrateTurn(StoreHydrationProtocolMapper.MergeTurn(_turnStore.Snapshot, msg));
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
        }
    }
}
