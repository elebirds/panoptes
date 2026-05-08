// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 21:15:00 +0800
// Description: 验证规划输入模块的路径预览与移动快照行为。

package planning

import (
	"encoding/json"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestBuildPlanningPathPreviewResponse_StartSettlerCanPreviewInitialRegionTargets(t *testing.T) {
	state, unitID := newPreviewState(t)

	for _, targetNodeID := range []string{"N3_0", "N4_0"} {
		resp := buildPlanningPathPreviewResponse(state, "player-1", &pb.MsgPlanningPathPreviewRequest{
			RequestId:    "req-" + targetNodeID,
			UnitId:       unitID,
			Action:       "move",
			TargetNodeId: targetNodeID,
		})
		if !resp.GetValid() {
			t.Fatalf("preview valid for %s = false, error=%s", targetNodeID, resp.GetErrorCode())
		}
		if got := len(resp.GetPathNodeIds()); got < 2 {
			t.Fatalf("preview path len for %s = %d, want >= 2", resp.GetTargetNodeId(), got)
		}
		if resp.GetFirstTurnNodeId() == "" {
			t.Fatalf("preview first turn node for %s is empty", resp.GetTargetNodeId())
		}
	}
}

func TestBuildPlanningSnapshot_UsesActiveMarchPreviewForMoveOrders(t *testing.T) {
	state, unitID := newPreviewState(t)
	resp := buildPlanningPathPreviewResponse(state, "player-1", &pb.MsgPlanningPathPreviewRequest{
		RequestId:    "req-N4_0",
		UnitId:       unitID,
		Action:       "move",
		TargetNodeId: "N4_0",
	})
	if !resp.GetValid() {
		t.Fatalf("preview valid = false, error=%s", resp.GetErrorCode())
	}

	state.TurnRuntime.Planning.UnitOrders[unitID] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       unitID,
		Action:       "move",
		TargetNodeID: "N4_0",
	}
	state.TurnRuntime.Resolving.ActiveMarches[unitID] = domain.ActiveMarch{
		PlayerID:          "player-1",
		UnitID:            unitID,
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "N4_0",
		LastPreview: domain.RoutePreview{
			PathNodeIDs:     append([]string(nil), resp.GetPathNodeIds()...),
			FirstTurnNodeID: resp.GetFirstTurnNodeId(),
			TotalTurns:      int(resp.GetTotalTurns()),
			TurnStops: []domain.MarchTurnStop{
				{TurnIndex: 1, NodeID: resp.GetFirstTurnNodeId()},
			},
		},
	}

	snapshot := gamequery.BuildPlanningSnapshot(state, "player-1")
	if len(snapshot.GetUnitOrders()) != 1 {
		t.Fatalf("snapshot unit order count = %d, want 1", len(snapshot.GetUnitOrders()))
	}
	order := snapshot.GetUnitOrders()[0]
	if order.GetAction() != "move" {
		t.Fatalf("snapshot action = %q, want move", order.GetAction())
	}
	if got := len(order.GetPathNodeIds()); got < 2 {
		t.Fatalf("snapshot path len = %d, want >= 2", got)
	}
}

func TestBuildPlanningSnapshot_IncludesDraftPlanningFields(t *testing.T) {
	state, unitID := newPreviewState(t)
	state.TurnRuntime.Planning.SetPendingResearchTarget("player-1", "agrarian_foundations")
	state.TurnRuntime.Planning.SetPendingPolicy("player-1", domain.PolicyExpansion)
	state.TurnRuntime.Planning.SetPendingInstitutionLoadout("player-1", []string{"academy_charter"})
	state.TurnRuntime.Planning.SetMinisterDrafts("player-1", []domain.MinisterDraft{
		{
			DraftID:      "draft-research-1",
			PlayerID:     "player-1",
			MinisterRole: "domestic",
			Kind:         domain.MinisterDraftKindResearch,
			TargetID:     "agrarian_foundations",
			TargetLabel:  "Agrarian Foundations",
			Title:        "建议优先推进农业根基",
			Summary:      "粮食与开局扩张更稳。",
			Rationale:    "当前局势适合优先补足基础生产。",
			RiskNote:     "会推迟军事科技。",
			Status:       domain.MinisterDraftStatusPending,
			Available:    true,
			Turn:         1,
			Source:       domain.MinisterDraftSourceRuleOnly,
		},
	})
	state.TurnRuntime.Planning.BuildOrders = []domain.BuildOrder{
		{PlayerID: "player-1", NodeID: "N1_0", BuildingType: "farm", CityID: "C1"},
		{PlayerID: "player-2", NodeID: "N2_0", BuildingType: "mine", CityID: "C2"},
	}
	state.TurnRuntime.Planning.RecipeSelections = []domain.RecipeSelectionOrder{
		{PlayerID: "player-1", NodeID: "N3_0", RecipeID: "farm_food"},
		{PlayerID: "player-2", NodeID: "N4_0", RecipeID: "mine_ore"},
	}
	state.TurnRuntime.Planning.UnitOrders[unitID] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       unitID,
		Action:       "hold",
		TargetNodeID: "N1_0",
	}

	snapshot := gamequery.BuildPlanningSnapshot(state, "player-1")

	if got := snapshot.GetPlannedResearchTargetTechnologyId(); got != "agrarian_foundations" {
		t.Fatalf("planned research target = %q, want agrarian_foundations", got)
	}
	if got := snapshot.GetPlannedNationalPolicyId(); got != "expansion" {
		t.Fatalf("planned national policy = %q, want expansion", got)
	}
	if got := snapshot.GetPlannedInstitutionIds(); len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("planned institution ids = %#v, want [academy_charter]", got)
	}
	if got := len(snapshot.GetBuildOrders()); got != 1 {
		t.Fatalf("build order count = %d, want 1", got)
	}
	if got := snapshot.GetBuildOrders()[0].GetNodeId(); got != "N1_0" {
		t.Fatalf("build order node_id = %q, want N1_0", got)
	}
	if got := len(snapshot.GetRecipeSelections()); got != 1 {
		t.Fatalf("recipe selection count = %d, want 1", got)
	}
	if got := snapshot.GetRecipeSelections()[0].GetRecipeId(); got != "farm_food" {
		t.Fatalf("recipe selection recipe_id = %q, want farm_food", got)
	}
	if got := len(snapshot.GetWarZoneDirectives()); got != 0 {
		t.Fatalf("war zone directive count = %d, want 0 in MVP snapshot", got)
	}
	if got := len(snapshot.GetWarZones()); got != 0 {
		t.Fatalf("war zone count = %d, want 0 in MVP snapshot", got)
	}
	if got := len(snapshot.GetMinisterDrafts()); got != 1 {
		t.Fatalf("minister draft count = %d, want 1", got)
	}
	draft := snapshot.GetMinisterDrafts()[0]
	if draft.GetMinisterRole() != "domestic" || !draft.GetAvailable() {
		t.Fatalf("minister draft header = %#v, want domestic available", draft)
	}
	var payload struct {
		DraftID string `json:"draft_id"`
		Kind    string `json:"kind"`
		Status  string `json:"status"`
	}
	if err := json.Unmarshal([]byte(draft.GetJsonPayload()), &payload); err != nil {
		t.Fatalf("unmarshal minister draft payload: %v", err)
	}
	if payload.DraftID != "draft-research-1" || payload.Kind != "research" || payload.Status != "pending" {
		t.Fatalf("minister draft payload = %#v, want draft-research-1/research/pending", payload)
	}
}

func newPreviewState(t *testing.T) (*domain.GameState, string) {
	t.Helper()

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "preview",
		Width:        5,
		Height:       1,
		SpawnPoints:  map[int]domain.Position{0: {Q: 0, R: 0}},
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for x := 0; x < 5; x++ {
		nodeID := nodeID(x)
		entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, Q: x, R: 0, Terrain: "plain"})
		mapData.NodeIndex[nodeID] = entity
	}

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", MoveCostNoRoad: 2, Passable: true, Buildable: true},
		},
	}))

	state := domain.NewGameState("game-preview", []string{"player-1"}, []string{"player-1"}, mapData)
	state.World = world

	spawnPos := mapData.PlayerSpawns["player-1"]
	unitEntry := world.Entry(ecs.CreateUnit(world, string(domain.UnitTypeSettler), "player-1", spawnPos))
	return state, ecs.UnitStatsC.Get(unitEntry).ID
}

func nodeID(x int) string {
	return "N" + string(rune('0'+x)) + "_0"
}
