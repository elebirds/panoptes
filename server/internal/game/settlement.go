package game

import (
	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine"
)

// RunDomesticSettlement executes all domestic systems and broadcasts results.
func RunDomesticSettlement(room *GameRoom) {
	if room == nil || room.state == nil {
		return
	}

	pipeline := engine.NewDomesticPipeline()
	events := pipeline.Run(room.state.World, room.state)

	room.broadcastSettlement(domain.PhaseDomesticResolving.String(), events)
	if room.cfg != nil && room.cfg.DevMode {
		debug.DumpGameStateSummary(room.state)
	}
	room.checkGameOver()

	room.state.PendingBuilds = room.state.PendingBuilds[:0]
	room.state.PendingResearchOrders = room.state.PendingResearchOrders[:0]
	room.state.PendingRecipeSelections = room.state.PendingRecipeSelections[:0]
	room.state.MinisterBuildOrders = room.state.MinisterBuildOrders[:0]
}

// RunCombatSettlement executes all combat systems and broadcasts results.
func RunCombatSettlement(room *GameRoom) {
	if room == nil || room.state == nil {
		return
	}

	room.prepareCombatOrders()
	pipeline := engine.NewCombatPipeline()
	events := pipeline.Run(room.state.World, room.state)
	room.refreshActiveMarchesAfterSettlement()

	room.broadcastSettlement(domain.PhaseCombatResolving.String(), events)
	if room.cfg != nil && room.cfg.DevMode {
		debug.DumpGameStateSummary(room.state)
	}
	room.checkGameOver()
	room.collectPendingTerritoryDeploysFromCombatOrders()

	room.state.PendingConflicts = room.state.PendingConflicts[:0]
	room.state.MinisterMoveOrders = room.state.MinisterMoveOrders[:0]
	clear(room.state.PendingCombatOrders)
	clear(room.combatOrders)
	clear(room.combatDeployOrders)
}
