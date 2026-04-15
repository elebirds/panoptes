// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 21:15:00 +0800
// Description: 验证规划输入模块的路径预览与移动快照行为。

package planning

import (
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

func newPreviewState(t *testing.T) (*domain.GameState, string) {
	t.Helper()

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "preview",
		Width:        5,
		Height:       1,
		SpawnPoints:  map[int]domain.Position{0: {X: 0, Y: 0}},
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for x := 0; x < 5; x++ {
		nodeID := nodeID(x)
		entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, X: x, Y: 0, Terrain: "plain"})
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
