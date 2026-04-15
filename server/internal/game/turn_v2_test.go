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
	if start.GetTimeout() != 30 || start.GetTurn() != 7 || start.GetTokens() != 3 {
		t.Fatalf("planning payload = %#v", start)
	}
	if start.GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("phase = %q, want %q", start.GetPhase(), domain.PhasePlanning.String())
	}
	if start.GetSnapshot() == nil {
		t.Fatalf("snapshot is nil")
	}
	if start.GetSnapshot().GetTurn() != 7 || start.GetSnapshot().GetPhase() != domain.PhasePlanning.String() {
		t.Fatalf("snapshot payload = %#v", start.GetSnapshot())
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

func newTestRuntime(gameID string, tp *stubTransport) *gamesession.Runtime {
	players := []gamesession.Player{
		NewHumanPlayer("player-1", "alice", tp),
	}
	runtime := gamesession.NewRuntime(gameID, players, tp, &config.Config{})
	state := domain.NewGameState(gameID, []string{"player-1"}, []string{"alice"}, &domain.MapData{})
	runtime.SetState(state)
	return runtime
}
