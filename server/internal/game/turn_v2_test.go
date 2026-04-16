// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证回合协调模块的Turn V2 回合约束。

package game

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/config"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
	gameturn "github.com/elebirds/panoptes/internal/game/turn"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type stubTransport struct {
	sent     map[string][]proto.Message
	sentMeta map[string][]*pb.EventMeta
}

func newStubTransport() *stubTransport {
	return &stubTransport{
		sent:     make(map[string][]proto.Message),
		sentMeta: make(map[string][]*pb.EventMeta),
	}
}

func (t *stubTransport) Send(ctx context.Context, playerID string, msg proto.Message) error {
	t.sent[playerID] = append(t.sent[playerID], msg)
	t.sentMeta[playerID] = append(t.sentMeta[playerID], coretransport.EventMetaFromContext(ctx))
	return nil
}

func (t *stubTransport) Broadcast(context.Context, string, proto.Message) error { return nil }

func (t *stubTransport) Stream(ctx context.Context, playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(ctx, playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func TestHumanPlayerNotifyTurnSendsPlanningStartWithSnapshot(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TurnTimeLimitPlanning: 30, TokensPerTurn: 3},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = nil
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.runtime.State().Turn = 7
	room.runtime.State().Phase = domain.PhasePlanning.String()
	room.runtime.State().Players["player-1"].TokensLeft = 1

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, domain.PhasePlanning.String())

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1", len(msgs))
	}

	start, ok := msgs[0].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", msgs[0])
	}
	if start.GetTimeout() != 30 || start.GetTurn() != 7 || start.GetTokens() != 1 {
		t.Fatalf("planning payload = %#v", start)
	}
	if start.GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("phase = %q, want %q", start.GetPhase(), domain.PhasePlanning.String())
	}
	if start.GetMyPlayer() == nil || start.GetMyPlayer().GetTokensLeft() != 1 {
		t.Fatalf("my_player = %#v, want tokens_left=1", start.GetMyPlayer())
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
	if start.GetSnapshot().GetTurn() != 7 || start.GetSnapshot().GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("snapshot payload = %#v", start.GetSnapshot())
	}
	meta := tp.sentMeta["player-1"][0]
	if got := turnMetaGameSessionID(meta); got != "game-1" {
		t.Fatalf("meta.game_session_id = %q, want game-1", got)
	}
}

func TestHumanPlayerNotifyTurnPrefersUnifiedPlanningTimeout(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TurnTimeLimitPlanning: 21,
			TokensPerTurn:         3,
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.runtime.State().Turn = 3
	room.runtime.State().Phase = domain.PhasePlanning.String()

	player := NewHumanPlayer("player-1", "alice", tp)
	player.NotifyTurn(context.Background(), room, domain.PhasePlanning.String())

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1", len(msgs))
	}

	start, ok := msgs[0].(*pb.MsgPlanningStart)
	if !ok {
		t.Fatalf("message type = %T, want MsgPlanningStart", msgs[0])
	}
	if start.GetTimeout() != 21 {
		t.Fatalf("timeout = %d, want 21", start.GetTimeout())
	}
}

func TestGameRoomRejectsActionsOutsidePlanning(t *testing.T) {
	room := NewRoom("game-1", nil, newStubTransport(), &config.Config{})
	room.runtime = newTestRuntime("game-1", newStubTransport())
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhaseResolving.String()

	if err := room.HandleGameCommand(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SubmitTurn{
					SubmitTurn: &pb.MsgSubmitTurn{},
				},
			},
		},
	}); err != ErrPhaseMismatch {
		t.Fatalf("submit error = %v, want %v", err, ErrPhaseMismatch)
	}
	if err := room.HandleGameCommand(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SetPolicy{
					SetPolicy: &pb.MsgSetPolicy{},
				},
			},
		},
	}); err != ErrPhaseMismatch {
		t.Fatalf("message error = %v, want %v", err, ErrPhaseMismatch)
	}
}

func TestHandleGameCommandPropagatesRequestMetaToOutboundResponses(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TokensPerTurn: 3, CityCoreMaxHP: 100, BaseResearchOutputPerTurn: 1, BaseIndustryOutputPerTurn: 2},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national"},
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhasePlanning.String()
	room.State().Players["player-1"].TokensLeft = 3

	err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "req-123",
		TraceID:   "trace-456",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SetPolicy{
					SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "expansion"},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleGameCommand() error = %v", err)
	}

	msgs := tp.sent["player-1"]
	if len(msgs) != 2 {
		t.Fatalf("send count = %d, want 2", len(msgs))
	}
	result, ok := msgs[0].(*pb.MsgSetPolicyResult)
	if !ok {
		t.Fatalf("message type = %T, want MsgSetPolicyResult", msgs[0])
	}
	if !result.GetSuccess() || result.GetNationalPolicyId() != "expansion" {
		t.Fatalf("policy result = %#v", result)
	}
	if _, ok := msgs[1].(*pb.MsgPlanningSnapshot); !ok {
		t.Fatalf("message type = %T, want MsgPlanningSnapshot", msgs[1])
	}
	if got := string(room.State().Players["player-1"].Policy); got != "" {
		t.Fatalf("active policy = %q, want empty before lock-in", got)
	}

	metas := tp.sentMeta["player-1"]
	if len(metas) != 2 {
		t.Fatalf("meta count = %d, want 2", len(metas))
	}
	for idx, meta := range metas {
		if meta == nil {
			t.Fatalf("event meta[%d] = nil, want request correlation", idx)
		}
		if meta.GetRequestId() != "req-123" {
			t.Fatalf("request_id[%d] = %q, want req-123", idx, meta.GetRequestId())
		}
		if meta.GetTraceId() != "trace-456" {
			t.Fatalf("trace_id[%d] = %q, want trace-456", idx, meta.GetTraceId())
		}
	}
}

func TestHandleGameCommandSetResearchTargetQueuesDraftWithoutUpdatingActiveState(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{TokensPerTurn: 3, CityCoreMaxHP: 100, BaseResearchOutputPerTurn: 1, BaseIndustryOutputPerTurn: 2},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Branch: "agriculture", Tier: 1, ResearchCost: 4},
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhasePlanning.String()

	err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID: "player-1",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_SetResearchTarget{
					SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agrarian_foundations"},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleGameCommand() error = %v", err)
	}

	msgs := tp.sent["player-1"]
	if len(msgs) != 2 {
		t.Fatalf("send count = %d, want 2", len(msgs))
	}
	result, ok := msgs[0].(*pb.MsgResearchResult)
	if !ok {
		t.Fatalf("message type = %T, want MsgResearchResult", msgs[0])
	}
	if !result.GetSuccess() || result.GetTechnologyId() != "agrarian_foundations" {
		t.Fatalf("research result = %#v", result)
	}
	if _, ok := msgs[1].(*pb.MsgPlanningSnapshot); !ok {
		t.Fatalf("message type = %T, want MsgPlanningSnapshot", msgs[1])
	}

	player := room.State().Players["player-1"]
	if got := player.Research.CurrentTargetTechnologyID; got != "" {
		t.Fatalf("current target = %q, want empty until lock-in", got)
	}
	if got := room.State().TurnRuntime.Planning.PendingResearchTarget("player-1"); got != "agrarian_foundations" {
		t.Fatalf("pending research target = %q, want agrarian_foundations", got)
	}
	view := room.BuildNodeViewForPlayer("missing", "player-1")
	if view != nil {
		t.Fatalf("unexpected node view for missing node")
	}
	playerView := room.State().Players["player-1"]
	if playerView == nil {
		t.Fatalf("player state missing")
	}
	researchView := gamequery.BuildPlayerView(room.State(), "player-1").GetResearch()
	if got := researchView.GetCurrentTargetTechnologyId(); got != "" {
		t.Fatalf("research view current target = %q, want empty before lock-in", got)
	}
	if got := researchView.GetRequiredProgress(); got != 0 {
		t.Fatalf("research view required_progress = %d, want 0 before lock-in", got)
	}
}

func TestHandleGameCommandBuildStructureSendsBuildStructureResult(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{
				ID:              "city_core",
				PlacementKind:   "city_foundation_center",
				BuildingScope:   "city_core",
				DefaultRecipeID: "city_core_settler",
				MaxHP:           100,
				TakeoverMode:    "disabled",
			},
			{
				ID:              "farm",
				PlacementKind:   "city_territory",
				BuildingScope:   "out_of_city",
				ResourceCosts:   staticdata.ResourceAmounts{"wood": 1},
				DefaultRecipeID: "farm_food",
				MaxHP:           80,
				TakeoverMode:    "delayed",
			},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "city_core_settler", BuildingID: "city_core", WorkAmount: 1, BaseProgress: 1},
			{ID: "farm_food", BuildingID: "farm", WorkAmount: 1, BaseProgress: 1},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhasePlanning.String()
	room.State().Players["player-1"].TokensLeft = 3
	room.State().Players["player-1"].Research.UnlockBuilding("farm")

	world := donburi.NewWorld()
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	targetEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N1", X: 1, Y: 0, Terrain: "plain"})
	cityEntry := world.Entry(cityEntity)
	targetEntry := world.Entry(targetEntity)
	for _, entry := range []*donburi.Entry{cityEntry, targetEntry} {
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)
	room.State().World = world
	room.State().Map = &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"C1": cityEntity, "N1": targetEntity},
	}
	room.State().NodeIndex = room.State().Map.NodeIndex
	room.State().Players["player-1"].Resources.Set(domain.ResourceWood, 2)

	err := room.HandleGameCommand(cmddispatch.InboundContext{
		PlayerID: "player-1",
	}, &pb.GameCommand{
		Body: &pb.GameCommand_Planning{
			Planning: &pb.PlanningCommand{
				Body: &pb.PlanningCommand_BuildStructure{
					BuildStructure: &pb.MsgBuildStructure{NodeId: "N1", BuildingTypeId: "farm", CityId: "C1"},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleGameCommand() error = %v", err)
	}

	msgs := tp.sent["player-1"]
	if len(msgs) == 0 {
		t.Fatalf("send count = 0, want >= 1")
	}
	result, ok := msgs[0].(*pb.MsgBuildStructureResult)
	if !ok {
		t.Fatalf("message type = %T, want MsgBuildStructureResult", msgs[0])
	}
	if !result.GetSuccess() || result.GetNodeId() != "N1" || result.GetBuildingTypeId() != "farm" || result.GetCityId() != "C1" {
		t.Fatalf("build result = %#v", result)
	}
}

func TestRunTurnResolutionLocksPendingPolicyAndResearchIntoActiveState(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Branch: "agriculture", Tier: 1, ResearchCost: 4},
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhaseResolving.String()
	room.State().TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	room.State().TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")

	room.RunTurnResolution()

	if got := string(room.State().Players["player-1"].Policy); got != "expansion" {
		t.Fatalf("active policy after lock-in = %q, want expansion", got)
	}
	if got := room.State().Players["player-1"].Research.CurrentTargetTechnologyID; got != "agrarian_foundations" {
		t.Fatalf("current research target after lock-in = %q, want agrarian_foundations", got)
	}
}

func TestRunTurnResolutionIncludesPlanningLockInEventsInEconomySection(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Branch: "agriculture", Tier: 1, ResearchCost: 4},
		},
	}))

	tp := newStubTransport()
	player := NewHumanPlayer("player-1", "alice", tp)
	room := NewRoom("game-1", []Player{player}, tp, &config.Config{})
	room.runtime = gamesession.NewRuntime("game-1", []gamesession.Player{player}, tp, &config.Config{})
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.runtime.SetState(domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{}))
	room.State().Phase = domain.PhaseResolving.String()
	room.State().TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	room.State().TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")

	room.RunTurnResolution()

	msgs := tp.sent["player-1"]
	if len(msgs) != 1 {
		t.Fatalf("send count = %d, want 1 settlement", len(msgs))
	}
	settlement, ok := msgs[0].(*pb.MsgTurnSettlement)
	if !ok {
		t.Fatalf("message type = %T, want MsgTurnSettlement", msgs[0])
	}
	if len(settlement.GetSections()) == 0 {
		t.Fatalf("settlement sections empty")
	}
	var economy *pb.SettlementSection
	for _, section := range settlement.GetSections() {
		if section.GetSection() == "economy" {
			economy = section
			break
		}
	}
	if economy == nil {
		t.Fatalf("economy section missing: %#v", settlement.GetSections())
	}
	if len(economy.GetEvents()) < 2 {
		t.Fatalf("economy events len = %d, want at least 2", len(economy.GetEvents()))
	}
	if got := economy.GetEvents()[0].GetType(); got != "national_policy_changed" {
		t.Fatalf("economy first event = %q, want national_policy_changed", got)
	}
	if got := economy.GetEvents()[1].GetType(); got != "research_target_changed" {
		t.Fatalf("economy second event = %q, want research_target_changed", got)
	}
}

func TestRunTurnResolutionFatalCapitalDestroySkipsPostCombatSystemsButKeepsLockInEvents(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             10,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national"},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agrarian_foundations", Branch: "agriculture", Tier: 1, ResearchCost: 4},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 10, TakeoverMode: "disabled"},
		},
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}},
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	tp := newStubTransport()
	player1 := NewHumanPlayer("player-1", "alice", tp)
	player2 := NewHumanPlayer("player-2", "bob", tp)
	room := NewRoom("game-1", []Player{player1, player2}, tp, &config.Config{})
	room.runtime = gamesession.NewRuntime("game-1", []gamesession.Player{player1, player2}, tp, &config.Config{})
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{
		"A1": ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"}),
		"A2": ecs.CreateNode(world, ecs.MapNode{ID: "A2", X: 1, Y: 0, Terrain: "plain"}),
		"B1": ecs.CreateNode(world, ecs.MapNode{ID: "B1", X: 0, Y: 1, Terrain: "plain"}),
		"B2": ecs.CreateNode(world, ecs.MapNode{ID: "B2", X: 1, Y: 1, Terrain: "plain"}),
	}
	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "turn-fatal",
		Width:     2,
		Height:    2,
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex
	state.Phase = domain.PhaseResolving.String()
	state.TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	state.TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")

	coreEntry := world.Entry(nodeIndex["A1"])
	coreNode := ecs.NodeC.Get(coreEntry)
	coreNode.Owner = "player-1"
	coreNode.TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", coreEntry)
	state.EnsureCityState("player-1", "A1")
	state.Players["player-1"].CapitalCityID = "A1"
	state.Players["player-1"].CapitalCityCoreHP = 10

	attackerEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-2", domain.Position{X: 1, Y: 0}))
	ecs.UnitStatsC.Get(attackerEntry).ID = "infantry-attack"
	settlerEntry := world.Entry(ecs.CreateUnit(world, "settler", "player-1", domain.Position{X: 0, Y: 1}))
	ecs.UnitStatsC.Get(settlerEntry).ID = "settler-1"
	state.TurnRuntime.Planning.UnitOrders["infantry-attack"] = domain.UnitDirective{
		PlayerID:     "player-2",
		UnitID:       "infantry-attack",
		Action:       "attack",
		TargetNodeID: "A1",
	}
	state.TurnRuntime.Planning.UnitOrders["settler-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "settler-1",
		Action:       "settle_city",
		TargetNodeID: "B2",
	}
	room.runtime.SetState(state)

	room.RunTurnResolution()

	if got := string(room.State().Players["player-1"].Policy); got != "expansion" {
		t.Fatalf("active policy after fatal turn = %q, want expansion", got)
	}
	if got := room.State().Players["player-1"].Research.CurrentTargetTechnologyID; got != "agrarian_foundations" {
		t.Fatalf("research target after fatal turn = %q, want agrarian_foundations", got)
	}

	settlement := firstMessage[*pb.MsgTurnSettlement](tp.sent["player-1"])
	if settlement == nil {
		t.Fatalf("fatal turn settlement not sent")
	}
	if !hasSettlementEvent(settlement, "unit", "city_core_destroyed") {
		t.Fatalf("fatal turn missing city_core_destroyed event")
	}
	if hasSettlementEvent(settlement, "unit", "upkeep_paid") {
		t.Fatalf("fatal turn should skip upkeep_paid")
	}
	if hasSettlementEvent(settlement, "map", "city_founded") {
		t.Fatalf("fatal turn should skip settle_city map action")
	}
	if hasSettlementEvent(settlement, "economy", "point_budget_refreshed") {
		t.Fatalf("fatal turn should skip economy pipeline")
	}
	if !hasSettlementEvent(settlement, "economy", "national_policy_changed") || !hasSettlementEvent(settlement, "economy", "research_target_changed") {
		t.Fatalf("fatal turn should keep planning lock-in events in economy section")
	}
	if _, ok := room.State().GetNode("B2"); !ok {
		t.Fatalf("missing node B2")
	}
	targetNode := world.Entry(nodeIndex["B2"])
	if targetNode.HasComponent(ecs.BuildingC) {
		t.Fatalf("fatal turn should skip settle_city build on B2")
	}
	if _, ok := findUnitEntryByID(room.State().World, "settler-1"); !ok {
		t.Fatalf("settler-1 should remain when map actions are skipped")
	}
}

func TestRunTurnResolutionNonFatalStillIncludesCombatUpkeepInUnitSection(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             10,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	tp := newStubTransport()
	player := NewHumanPlayer("player-1", "alice", tp)
	room := NewRoom("game-1", []Player{player}, tp, &config.Config{})
	room.runtime = gamesession.NewRuntime("game-1", []gamesession.Player{player}, tp, &config.Config{})
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", X: 0, Y: 0, Terrain: "plain"})
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "upkeep-test", Width: 1, Height: 1, NodeIndex: map[string]donburi.Entity{"A1": nodeEntity}})
	state.World = world
	state.NodeIndex = state.Map.NodeIndex
	state.Phase = domain.PhaseResolving.String()
	state.Players["player-1"].Resources.Set(domain.ResourceFood, 0)
	unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{X: 0, Y: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"
	room.runtime.SetState(state)

	room.RunTurnResolution()

	settlement := firstMessage[*pb.MsgTurnSettlement](tp.sent["player-1"])
	if settlement == nil {
		t.Fatalf("non-fatal settlement not sent")
	}
	if !hasSettlementEvent(settlement, "unit", "upkeep_paid") {
		t.Fatalf("non-fatal turn should include upkeep_paid in unit section")
	}
	if !hasSettlementEvent(settlement, "unit", "unit_starving") {
		t.Fatalf("non-fatal turn should include unit_starving in unit section")
	}
}

func TestInstitutionLoadoutActivatesOnNextPlanningStart(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Policies: []staticdata.PolicyDefinition{
			{
				ID:               "academy_charter",
				Layer:            "institutional",
				ActivationTiming: "next_turn",
				ModifierEffects: []staticdata.ModifierEffect{
					{Trigger: "point.output", PointKey: "research_output", ModifierType: "flat", Value: 1},
				},
			},
		},
	}))

	tp := newStubTransport()
	room := NewRoom("game-1", nil, tp, &config.Config{})
	room.runtime = newTestRuntime("game-1", tp)
	room.coordinator = gameturn.NewCoordinator(room.runtime, room)
	room.State().Phase = domain.PhaseResolving.String()
	room.State().Players["player-1"].Institutions.SlotCount = 1
	room.State().Players["player-1"].Institutions.UnlockCandidate("academy_charter")
	room.State().TurnRuntime.Planning.SetPendingInstitutionLoadout("player-1", []string{"academy_charter"})

	room.RunTurnResolution()

	if got := room.State().Players["player-1"].Institutions.ActivePolicyIDs; len(got) != 0 {
		t.Fatalf("active institutions after settlement = %#v, want empty", got)
	}
	if got := room.State().Players["player-1"].Institutions.PendingPolicyIDs; len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("pending institutions after settlement = %#v, want [academy_charter]", got)
	}

	room.State().Turn++
	gamesession.PreparePlanningStartState(room.State())

	if got := room.State().Players["player-1"].Institutions.ActivePolicyIDs; len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("active institutions after planning start = %#v, want [academy_charter]", got)
	}
	if got := room.State().EffectiveResearchOutput("player-1"); got != 2 {
		t.Fatalf("research output with institution active = %d, want 2", got)
	}
}

func newTestRuntime(gameID string, tp *stubTransport) *gamesession.Runtime {
	players := []gamesession.Player{
		NewHumanPlayer("player-1", "alice", tp),
	}
	runtime := gamesession.NewRuntime(gameID, players, tp, &config.Config{})
	state := domain.NewGameState(gameID, []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	runtime.SetState(state)
	return runtime
}

func hasSettlementEvent(msg *pb.MsgTurnSettlement, section string, eventType string) bool {
	if msg == nil {
		return false
	}
	for _, currentSection := range msg.GetSections() {
		if currentSection.GetSection() != section {
			continue
		}
		for _, event := range currentSection.GetEvents() {
			if event.GetType() == eventType {
				return true
			}
		}
	}
	return false
}

func turnMetaGameSessionID(meta *pb.EventMeta) string {
	if meta == nil {
		return ""
	}
	field := meta.ProtoReflect().Descriptor().Fields().ByName("game_session_id")
	if field == nil {
		return ""
	}
	return meta.ProtoReflect().Get(field).String()
}

func firstMessage[T proto.Message](msgs []proto.Message) T {
	var zero T
	for _, msg := range msgs {
		if typed, ok := msg.(T); ok {
			return typed
		}
	}
	return zero
}
