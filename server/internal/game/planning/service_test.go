// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 21:15:00 +0800
// Description: 验证规划输入模块的路径预览与移动快照行为。

package planning

import (
	"path/filepath"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestBuildPlanningPathPreviewResponse_StartSettlerCanPreviewInitialRegionTargets(t *testing.T) {
	t.Parallel()

	state, unitID := newInitialRegionPreviewState(t)

	for _, targetNodeID := range []string{"Q17", "P18"} {
		resp := buildPlanningPathPreviewResponse(state, "player-4", &pb.MsgPlanningPathPreviewRequest{
			RequestId:    "req-" + targetNodeID,
			UnitId:       unitID,
			Action:       "move",
			TargetNodeId: targetNodeID,
		})

		if !resp.GetValid() {
			t.Fatalf("preview valid for %s = false, error=%s", targetNodeID, resp.GetErrorCode())
		}
		if got := len(resp.GetPathNodeIds()); got < 2 {
			t.Fatalf("preview path len for %s = %d, want >= 2", targetNodeID, got)
		}
		if resp.GetFirstTurnNodeId() == "" {
			t.Fatalf("preview first turn node for %s is empty", targetNodeID)
		}
	}
}

func TestBuildPlanningSnapshot_UsesActiveMarchPreviewForMoveOrders(t *testing.T) {
	t.Parallel()

	state, unitID := newInitialRegionPreviewState(t)
	resp := buildPlanningPathPreviewResponse(state, "player-4", &pb.MsgPlanningPathPreviewRequest{
		RequestId:    "req-Q17",
		UnitId:       unitID,
		Action:       "move",
		TargetNodeId: "Q17",
	})
	if !resp.GetValid() {
		t.Fatalf("preview valid = false, error=%s", resp.GetErrorCode())
	}

	state.TurnRuntime.Planning.UnitOrders[unitID] = domain.UnitDirective{
		PlayerID:     "player-4",
		UnitID:       unitID,
		Action:       "move",
		TargetNodeID: "Q17",
	}
	state.TurnRuntime.Resolving.ActiveMarches[unitID] = domain.ActiveMarch{
		PlayerID:          "player-4",
		UnitID:            unitID,
		Action:            domain.UnitResolutionActionMove,
		DestinationNodeID: "Q17",
		LastPreview: domain.RoutePreview{
			PathNodeIDs:     append([]string(nil), resp.GetPathNodeIds()...),
			FirstTurnNodeID: resp.GetFirstTurnNodeId(),
			TotalTurns:      int(resp.GetTotalTurns()),
			TurnStops: []domain.MarchTurnStop{
				{TurnIndex: 1, NodeID: resp.GetFirstTurnNodeId()},
			},
		},
	}

	snapshot := gamequery.BuildPlanningSnapshot(state, "player-4")
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

func newInitialRegionPreviewState(t *testing.T) (*domain.GameState, string) {
	t.Helper()

	catalog, err := staticdata.LoadDir(filepath.Join("..", "..", "..", "..", "data", "generated", "server"))
	if err != nil {
		t.Fatalf("LoadDir() error = %v", err)
	}
	staticdata.SetDefault(catalog)

	mapFile, err := maploader.LoadMap(catalog, "initial_4_regions_20x20")
	if err != nil {
		t.Fatalf("LoadMap() error = %v", err)
	}

	playerIDs := []string{"player-1", "player-2", "player-3", "player-4"}
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, mapFile, playerIDs)
	state := domain.NewGameState("game-preview", playerIDs, playerIDs, mapData)
	state.World = world

	spawnPos, ok := mapData.PlayerSpawns["player-4"]
	if !ok {
		t.Fatalf("player-4 spawn missing")
	}

	unitEntry := world.Entry(ecs.CreateUnit(world, string(domain.UnitTypeSettler), "player-4", spawnPos))
	return state, ecs.UnitStatsC.Get(unitEntry).ID
}
