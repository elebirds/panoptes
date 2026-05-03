// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载领域事件写入逻辑拆分后的事件族或事件辅助逻辑。

package event

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func TestCityCapturedEventApplyDestroysCapitalCoreViaSharedMutation(t *testing.T) {
	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "A1", Q: 0, R: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.CreateBuilding(world, "city_core", "player-1", "A1", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"A1": nodeEntity},
	})
	state.World = world
	state.NodeIndex = state.Map.NodeIndex
	state.Players["player-1"].CapitalCityID = "A1"

	CityCapturedEvent{
		NodeID:     "A1",
		CityID:     "A1",
		OldOwnerID: "player-1",
		NewOwnerID: "player-2",
	}.Apply(world, state)

	if !state.IsOver || !state.Outcome.IsOver {
		t.Fatalf("state should be over after capital capture")
	}
	if state.WinnerID != "player-2" || state.Outcome.WinnerID != "player-2" {
		t.Fatalf("winner = (%q, %q), want player-2", state.WinnerID, state.Outcome.WinnerID)
	}
	if state.OverReason != "city_core_destroyed" || state.Outcome.Reason != "city_core_destroyed" {
		t.Fatalf("over reason = (%q, %q), want city_core_destroyed", state.OverReason, state.Outcome.Reason)
	}
	if got := ecs.BuildingC.Get(nodeEntry).Owner; got != "player-2" {
		t.Fatalf("core building owner = %q, want player-2", got)
	}
	if got := ecs.NodeC.Get(nodeEntry).Owner; got != "player-2" {
		t.Fatalf("node owner = %q, want player-2", got)
	}
}
