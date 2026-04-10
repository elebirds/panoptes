package game

import (
	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/engine"
)

// RunDomesticSettlement executes all domestic systems and broadcasts results.
func RunDomesticSettlement(room *GameRoom) {
	if room == nil || room.state == nil {
		return
	}

	room.state.PendingBuilds = append(room.state.PendingBuilds, room.pendingBuilds...)
	room.pendingBuilds = room.pendingBuilds[:0]

	pipeline := engine.NewDomesticPipeline()
	events := pipeline.Run(room.state.World, room.state)

	room.broadcastSettlement("domestic", events)
	if room.cfg != nil && room.cfg.DevMode {
		debug.DumpGameStateSummary(room.state)
	}
	room.checkGameOver()

	room.state.PendingBuilds = room.state.PendingBuilds[:0]
	room.state.MinisterBuildOrders = room.state.MinisterBuildOrders[:0]
}

// RunCombatSettlement executes all combat systems and broadcasts results.
func RunCombatSettlement(room *GameRoom) {
	if room == nil || room.state == nil {
		return
	}

	room.applyMoveOrdersToWorld()
	pipeline := engine.NewCombatPipeline()
	events := pipeline.Run(room.state.World, room.state)

	room.broadcastSettlement("combat", events)
	if room.cfg != nil && room.cfg.DevMode {
		debug.DumpGameStateSummary(room.state)
	}
	room.checkGameOver()

	room.state.PendingConflicts = room.state.PendingConflicts[:0]
	room.state.MinisterMoveOrders = room.state.MinisterMoveOrders[:0]
}
