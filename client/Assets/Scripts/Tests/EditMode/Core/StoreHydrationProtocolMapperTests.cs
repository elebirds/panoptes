using System.Collections.Generic;
using NUnit.Framework;
using Panoptes.Core.Application.Stores;
using Panoptes.Core.Domain;
using Panoptes.Protocol.V1;

namespace Panoptes.Tests.EditMode.Core
{
    public sealed class StoreHydrationProtocolMapperTests
    {
        [Test]
        public void MergeGameSync_ShouldPreserveExistingIdentityAndFallbackValues()
        {
            var current = new GameStateStoreState(
                gameId: "game-1",
                activeGameSessionId: "session-1",
                myPlayerId: "player-1",
                turn: 4,
                phase: "planning",
                mapWidth: 8,
                mapHeight: 7,
                tokensLeft: 3,
                nodes: new Dictionary<string, NodeDto>
                {
                    ["n1"] = new NodeDto { Id = "n1", BuildingMaxHp = 12 }
                },
                units: new Dictionary<string, UnitDto>
                {
                    ["u1"] = new UnitDto { Id = "u1", MaxHp = 6 }
                },
                myResources: new ResourceDto { Food = 2 });

            var mapped = StoreHydrationProtocolMapper.MergeGameSync(current, new MsgGameSync
            {
                Turn = 5,
                Phase = "resolving",
                MyPlayer = new PlayerView
                {
                    Id = "player-1",
                    TokensLeft = 1,
                    Resources = new ResourceBag
                    {
                        Items =
                        {
                            new ResourceValue { Key = ResourceKeys.ResourceFood, Amount = 9 },
                            new ResourceValue { Key = " coal ", Amount = 6 }
                        }
                    },
                    Points = new PointBag
                    {
                        Items =
                        {
                            new PointValue { Key = "industry_output", Amount = 4 },
                            new PointValue { Key = " logistics_capacity ", Amount = 11 }
                        }
                    },
                    Research = new ResearchStateView
                    {
                        ActiveTechnologyIds = { " organized_labor " },
                        PendingActivationTechnologyIds = { "metallurgy" }
                    }
                },
                Nodes =
                {
                    new NodeView
                    {
                        Id = "n1",
                        Pos = new Position { Q = 2, R = -2 },
                        BuildingTypeId = "farm",
                        BuildingHp = 5
                    }
                },
                Units =
                {
                    new UnitView
                    {
                        Id = "u1",
                        UnitType = "scout",
                        Faction = "player-1",
                        Pos = new Position { Q = 2, R = -2 },
                        Hp = 4
                    }
                }
            });

            Assert.That(mapped.GameId, Is.EqualTo("game-1"));
            Assert.That(mapped.ActiveGameSessionId, Is.EqualTo("session-1"));
            Assert.That(mapped.MapWidth, Is.EqualTo(8));
            Assert.That(mapped.Turn, Is.EqualTo(5));
            Assert.That(mapped.Phase, Is.EqualTo("resolving"));
            Assert.That(mapped.TokensLeft, Is.EqualTo(1));
            Assert.That(mapped.MyResources.Food, Is.EqualTo(9));
            Assert.That(mapped.MyResources.IndustryOutput, Is.EqualTo(4));
            Assert.That(mapped.MyResources.ResourceAmounts["coal"], Is.EqualTo(6));
            Assert.That(mapped.MyResources.PointAmounts["logistics_capacity"], Is.EqualTo(11));
            Assert.That(mapped.ResearchState.ActiveTechnologyIds, Is.EqualTo(new[] { "organized_labor" }));
            Assert.That(mapped.ResearchState.PendingActivationTechnologyIds, Is.EqualTo(new[] { "metallurgy" }));
            Assert.That(mapped.Nodes["n1"].BuildingMaxHp, Is.EqualTo(12));
            Assert.That(mapped.Units["u1"].MaxHp, Is.EqualTo(6));
        }

        [Test]
        public void MergeGameSync_WithoutPhase_ShouldDefaultToResolving()
        {
            var current = new GameStateStoreState(
                turn: 4,
                phase: "planning",
                tokensLeft: 3);

            var mapped = StoreHydrationProtocolMapper.MergeGameSync(current, new MsgGameSync
            {
                Turn = 5
            });

            Assert.That(mapped.Turn, Is.EqualTo(5));
            Assert.That(mapped.Phase, Is.EqualTo(GamePhases.Resolving));
        }

        [Test]
        public void MergeGameSync_WithSettlementEvents_ShouldDefaultToResolvingEvenIfPhaseIsPlanning()
        {
            var current = new GameStateStoreState(
                turn: 4,
                phase: "planning",
                tokensLeft: 3);
            var msg = new MsgGameSync
            {
                Turn = 5,
                Phase = "planning"
            };
            msg.Events.Add(new DomainEventEnvelope
            {
                Channel = "unit",
                Kind = "unit_moved"
            });

            var mapped = StoreHydrationProtocolMapper.MergeGameSync(current, msg);

            Assert.That(mapped.Turn, Is.EqualTo(5));
            Assert.That(mapped.Phase, Is.EqualTo(GamePhases.Resolving));
        }

        [Test]
        public void ToPlanningDraft_ShouldMapSnapshotWithoutProtocolTypes()
        {
            var mapped = StoreHydrationProtocolMapper.ToPlanningDraft(new MsgPlanningSnapshot
            {
                Turn = 6,
                Phase = "planning",
                PlannedResearchTargetTechnologyId = "irrigation",
                PlannedNationalPolicyId = "mobilize",
                UnitOrders =
                {
                    new QueuedUnitOrder
                    {
                        UnitId = "u1",
                        Action = "move",
                        TargetNodeId = "n2",
                        PathNodeIds = { "n1", "n2" },
                        TurnStops = { new MarchTurnStop { TurnIndex = 1, NodeId = "n2" } }
                    }
                },
                BuildOrders =
                {
                    new QueuedBuildOrder { NodeId = "n3", BuildingTypeId = "farm", CityId = "city-1" }
                },
                RecipeSelections =
                {
                    new QueuedRecipeSelection { NodeId = "n4", RecipeId = "grain" }
                },
                PlannedInstitutionIds = { "labor" }
            });

            Assert.That(mapped.SnapshotTurn, Is.EqualTo(6));
            Assert.That(mapped.UnitOrders[0].PathNodeIds, Is.EqualTo(new[] { "n1", "n2" }));
            Assert.That(mapped.UnitOrders[0].TurnStops[0].NodeId, Is.EqualTo("n2"));
            Assert.That(mapped.BuildOrders[0].BuildingTypeId, Is.EqualTo("farm"));
            Assert.That(mapped.RecipeSelections[0].RecipeId, Is.EqualTo("grain"));
            Assert.That(mapped.PlannedResearchTargetTechnologyId, Is.EqualTo("irrigation"));
            Assert.That(mapped.PlannedNationalPolicyId, Is.EqualTo("mobilize"));
            Assert.That(mapped.PlannedInstitutionIds, Is.EqualTo(new[] { "labor" }));
        }

        [Test]
        public void ToPlanningDraft_ShouldMapMinisterOperationDraftStepsFromJson()
        {
            var mapped = StoreHydrationProtocolMapper.ToPlanningDraft(new MsgPlanningSnapshot
            {
                Turn = 6,
                Phase = "planning",
                MinisterDrafts =
                {
                    new MinisterDraftView
                    {
                        MinisterRole = "military",
                        Available = true,
                        JsonPayload = "{\"draft_id\":\"op-1\",\"player_id\":\"player-1\",\"minister_role\":\"military\",\"kind\":\"operation\",\"target_id\":\"north_front\",\"target_label\":\"北线\",\"title\":\"北线行动\",\"summary\":\"压迫敌军前线。\",\"rationale\":\"敌军补给不足。\",\"risk_note\":\"侧翼会变薄。\",\"status\":\"pending\",\"available\":true,\"turn\":6,\"source\":\"llm_action\",\"operation_id\":\"north-front\",\"objective\":\"夺取北部渡口\",\"operation_steps\":[{\"draft_id\":\"step-1\",\"kind\":\"unit_order\",\"target_label\":\"弓兵前压至 N2\",\"unit_id\":\"u-archer\",\"action\":\"move\",\"target_node_id\":\"N2\"},{\"draft_id\":\"step-2\",\"kind\":\"build\",\"target_label\":\"V3 修筑箭塔\",\"node_id\":\"V3\",\"building_type_id\":\"watchtower\"}]}"
                    }
                }
            });

            Assert.That(mapped.MinisterDrafts[0].OperationId, Is.EqualTo("north-front"));
            Assert.That(mapped.MinisterDrafts[0].Objective, Is.EqualTo("夺取北部渡口"));
            Assert.That(mapped.MinisterDrafts[0].OperationCommands.Count, Is.EqualTo(2));
            Assert.That(mapped.MinisterDrafts[0].OperationCommands[0].Label, Is.EqualTo("弓兵前压至 N2"));
            Assert.That(mapped.MinisterDrafts[0].OperationCommands[0].UnitId, Is.EqualTo("u-archer"));
            Assert.That(mapped.MinisterDrafts[0].OperationCommands[1].BuildingTypeId, Is.EqualTo("watchtower"));
        }

        [Test]
        public void MergeMinisterProposals_ShouldOverlayTypedOperationCommands()
        {
            var current = new PlanningDraftState(
                snapshotTurn: 6,
                snapshotPhase: "planning",
                ministerDrafts: new[]
                {
                    new MinisterDraftDto
                    {
                        DraftId = "op-1",
                        MinisterRole = "military",
                        Kind = "operation",
                        Status = "pending",
                        Available = true
                    }
                });

            var merged = StoreHydrationProtocolMapper.MergeMinisterProposals(current, new[]
            {
                new MinisterProposalView
                {
                    ProposalId = "op-1",
                    MinisterRole = "military",
                    Kind = "operation",
                    Title = "北线行动",
                    Objective = "夺取北部渡口",
                    OperationCommands =
                    {
                        new MinisterOperationCommandView
                        {
                            Label = "弓兵前压至 N2",
                            Kind = "unit_order",
                            Command = new CommandEnvelope
                            {
                                IssueUnitOrder = new MsgIssueUnitOrder
                                {
                                    UnitId = "u-archer",
                                    Action = "move",
                                    TargetNodeId = "N2"
                                }
                            }
                        }
                    }
                }
            });

            Assert.That(merged.MinisterDrafts.Count, Is.EqualTo(1));
            Assert.That(merged.MinisterDrafts[0].Title, Is.EqualTo("北线行动"));
            Assert.That(merged.MinisterDrafts[0].Objective, Is.EqualTo("夺取北部渡口"));
            Assert.That(merged.MinisterDrafts[0].OperationCommands[0].Label, Is.EqualTo("弓兵前压至 N2"));
            Assert.That(merged.MinisterDrafts[0].OperationCommands[0].UnitId, Is.EqualTo("u-archer"));
            Assert.That(merged.MinisterDrafts[0].OperationCommands[0].TargetNodeId, Is.EqualTo("N2"));
        }

        [Test]
        public void MergeTurn_GameSyncDuringSamePlanningTurn_ShouldPreserveTimeout()
        {
            var current = new TurnState(
                turn: 4,
                phase: "planning",
                timeoutSeconds: 45,
                isInteractive: true);

            var mapped = StoreHydrationProtocolMapper.MergeTurn(current, new MsgGameSync
            {
                Turn = 4,
                Phase = "planning"
            });

            Assert.That(mapped.TimeoutSeconds, Is.EqualTo(45));
            Assert.That(mapped.IsInteractive, Is.True);
        }

        [Test]
        public void MergeTurn_GameSyncLeavingPlanning_ShouldClearTimeout()
        {
            var current = new TurnState(
                turn: 4,
                phase: "planning",
                timeoutSeconds: 45,
                isInteractive: true);

            var mapped = StoreHydrationProtocolMapper.MergeTurn(current, new MsgGameSync
            {
                Turn = 4,
                Phase = "settlement"
            });

            Assert.That(mapped.TimeoutSeconds, Is.Zero);
            Assert.That(mapped.IsInteractive, Is.False);
        }

        [Test]
        public void MergeTurn_GameSyncWithoutPhase_ShouldDefaultToResolving()
        {
            var current = new TurnState(
                turn: 4,
                phase: "planning",
                timeoutSeconds: 45,
                isInteractive: true);

            var mapped = StoreHydrationProtocolMapper.MergeTurn(current, new MsgGameSync
            {
                Turn = 5
            });

            Assert.That(mapped.Phase, Is.EqualTo(GamePhases.Resolving));
            Assert.That(mapped.TimeoutSeconds, Is.Zero);
            Assert.That(mapped.IsInteractive, Is.False);
        }

        [Test]
        public void MergeTurn_GameSyncWithSettlementEvents_ShouldDefaultToResolvingEvenIfPhaseIsPlanning()
        {
            var current = new TurnState(
                turn: 4,
                phase: "planning",
                timeoutSeconds: 45,
                isInteractive: true);
            var msg = new MsgGameSync
            {
                Turn = 5,
                Phase = "planning"
            };
            msg.Events.Add(new DomainEventEnvelope
            {
                Channel = "unit",
                Kind = "unit_moved"
            });

            var mapped = StoreHydrationProtocolMapper.MergeTurn(current, msg);

            Assert.That(mapped.Phase, Is.EqualTo(GamePhases.Resolving));
            Assert.That(mapped.TimeoutSeconds, Is.Zero);
            Assert.That(mapped.IsInteractive, Is.False);
        }
    }
}
