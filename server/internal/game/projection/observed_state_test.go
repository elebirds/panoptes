// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载服务端权威状态到客户端观察视图的投影组合逻辑。

package projection

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestProjectObservedStateKeepsPlanningStartObservationCollectionsFinal(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:              100,
			BaseResearchOutputPerTurn:  1,
			BaseIndustryOutputPerTurn:  2,
			FacilityTakeoverTurns:      2,
			InitialCityTerritoryRadius: 1,
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "observed-state",
		Width:        1,
		Height:       1,
		PlayerSpawns: map[string]domain.Position{"player-1": {Q: 0, R: 0}},
		NodeIndex:    map[string]donburi.Entity{},
	}
	entity := ecs.CreateNode(world, ecs.MapNode{ID: "N0", Q: 0, R: 0, Terrain: "plain"})
	mapData.NodeIndex["N0"] = entity
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world

	observation := &gamequery.ObservationSnapshot{
		ViewerID: "player-1",
		MyPlayer: &pb.PlayerView{Id: "player-1"},
	}

	syncObserved := ProjectObservedState(state, observation, ObservedStateOptions{})
	if len(syncObserved.Nodes) != 1 {
		t.Fatalf("game-sync observed nodes len = %d, want fallback node view", len(syncObserved.Nodes))
	}

	planningStartObserved := ProjectObservedState(state, observation, ObservedStateOptions{
		ObservationCollectionsAreFinal: true,
	})
	if planningStartObserved.Nodes != nil {
		t.Fatalf("planning-start observed nodes = %#v, want nil from observation", planningStartObserved.Nodes)
	}
}

func TestProjectObservedStateSinglePlayerFallbackForPlanningStart(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "observed-state"})

	observed := ProjectObservedState(state, nil, ObservedStateOptions{
		UseSinglePlayerFallback: true,
	})
	if observed.PlayerID != "player-1" {
		t.Fatalf("observed player id = %q, want player-1", observed.PlayerID)
	}
	if observed.MyPlayer.GetId() != "player-1" {
		t.Fatalf("observed my player id = %q, want player-1", observed.MyPlayer.GetId())
	}
}
