using System.Reflection;
using NUnit.Framework;
using Panoptes.Core.Application.Cache;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Core.Infrastructure.Network;
using Panoptes.Protocol.V1;
using UnityEngine;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreMessageHydratorTests
    {
        private GameObject _dispatcherObject;
        private GameObject _cacheObject;
        private StoreMessageHydrator _hydrator;

        [TearDown]
        public void TearDown()
        {
            _hydrator?.Dispose();
            _hydrator = null;

            if (_dispatcherObject != null)
            {
                Object.DestroyImmediate(_dispatcherObject);
                _dispatcherObject = null;
            }

            if (_cacheObject != null)
            {
                Object.DestroyImmediate(_cacheObject);
                _cacheObject = null;
            }

            typeof(GameStateCache)
                .GetProperty(nameof(GameStateCache.Instance), BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { null });
        }

        [Test]
        public void HandlePlanningStart_ShouldHydrateGameDraftAndTurnStores()
        {
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            _hydrator = CreateHydrator(gameStore, draftStore, catalogStore, turnStore);

            _hydrator.HandleGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 2,
                Phase = "planning",
                MapWidth = 3,
                MapHeight = 4,
                RoomPlayers =
                {
                    new RoomPlayer
                    {
                        PlayerId = "player-1",
                        Username = "alice"
                    },
                    new RoomPlayer
                    {
                        PlayerId = "player-2",
                        Username = "bob"
                    }
                }
            });
            _hydrator.HandlePlanningStart(new MsgPlanningStart
            {
                Turn = 3,
                Phase = "planning",
                Tokens = 5,
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 5,
                    Resources = new ResourceBag
                    {
                        Items = { new ResourceValue { Key = ResourceKeys.ResourceWood, Amount = 7 } }
                    }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "n1",
                        Pos = new Position { Q = 1, R = -1 },
                        ControllerPlayerId = "player-1",
                        Terrain = "plain"
                    }
                },
                Snapshot = new MsgPlanningSnapshot
                {
                    Turn = 3,
                    Phase = "planning",
                    BuildOrders =
                    {
                        new QueuedBuildOrder { NodeId = "n1", BuildingTypeId = "farm", CityId = "city-1" }
                    }
                }
            });

            Assert.That(gameStore.Snapshot.Turn, Is.EqualTo(3));
            Assert.That(gameStore.Snapshot.TokensLeft, Is.EqualTo(5));
            Assert.That(gameStore.Snapshot.MyResources.Wood, Is.EqualTo(7));
            Assert.That(gameStore.Snapshot.Nodes["n1"].Owner, Is.EqualTo("player-1"));
            Assert.That(gameStore.Snapshot.RoomPlayers, Has.Count.EqualTo(2));
            Assert.That(gameStore.Snapshot.RoomPlayers[1].Username, Is.EqualTo("bob"));
            Assert.That(draftStore.Snapshot.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(turnStore.Snapshot.Turn, Is.EqualTo(3));
            Assert.That(turnStore.Snapshot.TokensLeft, Is.EqualTo(5));
        }

        [Test]
        public void AttachAndDispose_ShouldRegisterAndUnregisterDispatcherHandlers()
        {
            _cacheObject = new GameObject("GameStateCache");
            var cache = _cacheObject.AddComponent<GameStateCache>();
            typeof(GameStateCache)
                .GetProperty(nameof(GameStateCache.Instance), BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { cache });
            cache.SetActiveGameSession("game-1");

            _dispatcherObject = new GameObject("MessageDispatcher");
            var dispatcher = _dispatcherObject.AddComponent<MessageDispatcher>();
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            _hydrator = CreateHydrator(gameStore, draftStore, catalogStore, turnStore, dispatcher);
            _hydrator.Attach();

            dispatcher.Dispatch(GameFrame(new MsgTokenResult
            {
                Success = true,
                Action = "reveal",
                TokensLeft = 4
            }));

            Assert.That(gameStore.Snapshot.TokensLeft, Is.EqualTo(4));
            Assert.That(turnStore.Snapshot.TokensLeft, Is.EqualTo(4));

            _hydrator.Dispose();
            dispatcher.Dispatch(GameFrame(new MsgTokenResult
            {
                Success = true,
                Action = "reveal",
                TokensLeft = 2
            }));

            Assert.That(gameStore.Snapshot.TokensLeft, Is.EqualTo(4));
            Assert.That(turnStore.Snapshot.TokensLeft, Is.EqualTo(4));
        }

        [Test]
        public void HandleChatAndGameOver_ShouldHydrateReactiveStores()
        {
            var dispatcher = new GameObject("MessageDispatcher").AddComponent<MessageDispatcher>();
            _dispatcherObject = dispatcher.gameObject;
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var chatStore = new GameChatStore();
            var gameOverStore = new GameOverStore();
            var settlementStore = new SettlementStore();
            var feedbackStore = new GameplayFeedbackStore();
            _hydrator = new StoreMessageHydrator(
                dispatcher,
                new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore),
                gameStore,
                draftStore,
                turnStore,
                chatStore,
                gameOverStore,
                settlementStore,
                feedbackStore);

            _hydrator.HandleGameInit(new MsgGameInit
            {
                GameId = "game-1",
                YourPlayerId = "player-1",
                Turn = 1,
                Phase = "planning"
            });
            _hydrator.HandleGameChatSync(new MsgGameChatSync
            {
                Entries =
                {
                    new ChatEntry
                    {
                        SenderPlayerId = "player-2",
                        Turn = 1,
                        Payload = new ChatPayload { Emote = ChatEmote.Warning }
                    }
                }
            });
            _hydrator.HandleGameChatPosted(new MsgGameChatPosted
            {
                Entry = new ChatEntry
                {
                    SenderPlayerId = "player-1",
                    Turn = 1,
                    Payload = new ChatPayload { Text = "ready" }
                }
            });
            _hydrator.HandleGameSync(new MsgGameSync
            {
                Turn = 2,
                Phase = "settlement",
                NextPhase = "planning"
            });
            _hydrator.HandleGameOver(new MsgGameOver
            {
                WinnerId = "player-1",
                Reason = "conquest",
                Narrative = "Victory"
            });

            Assert.That(chatStore.Snapshot.Entries, Has.Count.EqualTo(2));
            Assert.That(chatStore.Snapshot.Entries[0].Payload.Emote, Is.EqualTo(GameChatEmoteKind.Warning));
            Assert.That(chatStore.Snapshot.Entries[1].Payload.Text, Is.EqualTo("ready"));
            Assert.That(gameOverStore.Snapshot.IsGameOver, Is.True);
            Assert.That(gameOverStore.Snapshot.IsWinner, Is.True);
            Assert.That(gameOverStore.Snapshot.WinnerId, Is.EqualTo("player-1"));
            Assert.That(gameOverStore.Snapshot.Reason, Is.EqualTo("conquest"));
            Assert.That(settlementStore.Snapshot.Settlement.NextPhase, Is.EqualTo("planning"));
            Assert.That(turnStore.Snapshot.IsGameOver, Is.True);
            Assert.That(gameStore.Snapshot.IsGameOver, Is.True);
        }

        [Test]
        public void HandleFailures_ShouldHydrateGameplayFeedbackStore()
        {
            var gameStore = new GameStateStore();
            var draftStore = new PlanningDraftStore();
            var catalogStore = new StaticCatalogStore();
            var turnStore = new TurnStore();
            var feedbackStore = new GameplayFeedbackStore();
            var dispatcher = new GameObject("MessageDispatcher").AddComponent<MessageDispatcher>();
            _dispatcherObject = dispatcher.gameObject;
            _hydrator = new StoreMessageHydrator(
                dispatcher,
                new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore),
                gameStore,
                draftStore,
                turnStore,
                new GameChatStore(),
                new GameOverStore(),
                new SettlementStore(),
                feedbackStore);

            _hydrator.HandleTokenResult(new MsgTokenResult { Success = false, ErrorCode = "no_tokens_left" });
            Assert.That(feedbackStore.Snapshot.Source, Is.EqualTo("token"));
            Assert.That(feedbackStore.Snapshot.Code, Is.EqualTo("no_tokens_left"));

            _hydrator.HandleProblem(new Problem
            {
                Code = "phase_mismatch",
                Message = "wrong phase",
                Details = { new ProblemDetail { Path = "phase", Detail = "settlement" } }
            });
            Assert.That(feedbackStore.Snapshot.Source, Is.EqualTo("problem"));
            Assert.That(feedbackStore.Snapshot.Message, Is.EqualTo("wrong phase"));
            Assert.That(feedbackStore.Snapshot.Details["phase"], Is.EqualTo("settlement"));

            _hydrator.HandleBuildStructureResult(new MsgBuildStructureResult
            {
                Success = false,
                ErrorCode = "building_exists",
                FeedbackMessage = "occupied"
            });
            Assert.That(feedbackStore.Snapshot.Source, Is.EqualTo("build"));
            Assert.That(feedbackStore.Snapshot.Code, Is.EqualTo("building_exists"));
            Assert.That(feedbackStore.Snapshot.Message, Is.EqualTo("occupied"));
        }

        private StoreMessageHydrator CreateHydrator(
            GameStateStore gameStore,
            PlanningDraftStore draftStore,
            StaticCatalogStore catalogStore,
            TurnStore turnStore,
            MessageDispatcher dispatcher = null)
        {
            dispatcher ??= new GameObject("MessageDispatcher").AddComponent<MessageDispatcher>();
            if (_dispatcherObject == null)
            {
                _dispatcherObject = dispatcher.gameObject;
            }

            return new StoreMessageHydrator(
                dispatcher,
                new StoreHydrationHelper(gameStore, draftStore, catalogStore, turnStore),
                gameStore,
                draftStore,
                turnStore,
                new GameChatStore(),
                new GameOverStore(),
                new SettlementStore(),
                new GameplayFeedbackStore());
        }

        private static ServerFrame GameFrame(MsgTokenResult msg)
        {
            return new ServerFrame
            {
                Meta = new EventMeta { GameSessionId = "game-1" },
                Game = new GameEvent { TokenResult = msg }
            };
        }
    }
}
