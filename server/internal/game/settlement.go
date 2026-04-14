package game

import (
	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine"
)

// RunTurnResolution executes the unified Turn V2 resolving pipeline.
func RunTurnResolution(room *GameRoom) {
	if room == nil || room.state == nil {
		return
	}

	room.syncPendingBuildOrders()
	room.prepareCombatOrders()
	combatPipeline := engine.NewCombatPipeline()
	unitEvents := combatPipeline.Run(room.state.World, room.state)
	room.refreshActiveMarchesAfterSettlement()
	mapEvents := room.applyPlannedMapActions()
	economyPipeline := engine.NewDomesticPipeline()
	economyEvents := economyPipeline.Run(room.state.World, room.state)

	room.broadcastTurnSettlement(unitEvents, mapEvents, economyEvents)
	if room.cfg != nil && room.cfg.DevMode {
		debug.DumpGameStateSummary(room.state)
	}
	room.checkGameOver()

	clear(room.state.PendingCombatOrders)
	room.state.PendingBuilds = room.state.PendingBuilds[:0]
	room.state.PendingResearchOrders = room.state.PendingResearchOrders[:0]
	room.state.PendingRecipeSelections = room.state.PendingRecipeSelections[:0]
	room.state.MinisterBuildOrders = room.state.MinisterBuildOrders[:0]
	room.state.PendingConflicts = room.state.PendingConflicts[:0]
	room.state.MinisterMoveOrders = room.state.MinisterMoveOrders[:0]
	clear(room.plannedUnitOrders)
}

func (r *GameRoom) syncPendingBuildOrders() {
	if r == nil || r.state == nil {
		return
	}
	if r.state.PendingBuilds == nil {
		r.state.PendingBuilds = make([]domain.BuildOrder, 0, len(r.pendingBuilds))
	} else {
		r.state.PendingBuilds = r.state.PendingBuilds[:0]
	}
	r.state.PendingBuilds = append(r.state.PendingBuilds, r.pendingBuilds...)
	r.pendingBuilds = r.pendingBuilds[:0]
}
