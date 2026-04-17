// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局模块的回合结算编排逻辑。

package game

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
)

// RunTurnResolution executes the unified Turn V2 resolving pipeline.
func RunTurnResolution(room *GameRoom) {
	if room == nil || room.State() == nil {
		return
	}

	collector := NewTurnResolutionRunner().Run(room)
	room.broadcastTurnSettlement(collector)
	if room.IsDevMode() {
		if hooks := currentDebugHooks(); hooks.DumpStateSummary != nil {
			hooks.DumpStateSummary(room.State())
		}
	}
	room.checkGameOver()

	state := room.State()
	clear(state.TurnRuntime.Resolving.UnitOrders)
	state.TurnRuntime.Planning.BuildOrders = state.TurnRuntime.Planning.BuildOrders[:0]
	state.TurnRuntime.Planning.RecipeSelections = state.TurnRuntime.Planning.RecipeSelections[:0]
	state.TurnRuntime.Planning.MinisterBuilds = state.TurnRuntime.Planning.MinisterBuilds[:0]
	state.TurnRuntime.Planning.MinisterMoves = state.TurnRuntime.Planning.MinisterMoves[:0]
	clear(state.TurnRuntime.Planning.MinisterDrafts)
	clear(state.TurnRuntime.Planning.UnitOrders)
	clear(state.TurnRuntime.Planning.MinisterDirectives)
	clear(state.TurnRuntime.Planning.PendingPolicies)
	clear(state.TurnRuntime.Planning.PendingResearch)
	clear(state.TurnRuntime.Planning.PendingInstitutions)
	clear(state.TurnRuntime.Planning.WarDirectives)
}

func (r *GameRoom) planningCommitEvents() []event.Event {
	state := r.State()
	if state == nil {
		return nil
	}
	events := make([]event.Event, 0, len(state.TurnRuntime.Planning.PendingPolicies)+len(state.TurnRuntime.Planning.PendingResearch)+len(state.TurnRuntime.Planning.PendingInstitutions))
	for playerID, policyID := range state.TurnRuntime.Planning.PendingPolicies {
		playerState, ok := state.Players[playerID]
		if !ok || playerState == nil || playerState.Policy == policyID {
			continue
		}
		evt := event.PolicyChangedEvent{
			PlayerID:  playerID,
			OldPolicy: string(playerState.Policy),
			NewPolicy: string(policyID),
		}
		events = append(events, evt)
	}
	for playerID, technologyID := range state.TurnRuntime.Planning.PendingResearch {
		playerState, ok := state.Players[playerID]
		if !ok || playerState == nil || playerState.Research.CurrentTargetTechnologyID == technologyID {
			continue
		}
		evt := event.ResearchTargetChangedEvent{
			PlayerID:     playerID,
			TechnologyID: technologyID,
		}
		events = append(events, evt)
	}
	for playerID := range state.Players {
		playerState := state.Players[playerID]
		if playerState == nil || !state.TurnRuntime.Planning.HasPendingInstitutionLoadout(playerID) {
			continue
		}
		policyIDs := state.TurnRuntime.Planning.PendingInstitutionLoadout(playerID)
		if policySlicesEqual(playerState.Institutions.PendingPolicyIDs, policyIDs) && playerState.Institutions.PendingActivationTurn == state.Turn+1 {
			continue
		}
		evt := event.InstitutionLoadoutChangedEvent{
			PlayerID:       playerID,
			PolicyIDs:      policyIDs,
			ActivationTurn: state.Turn + 1,
		}
		events = append(events, evt)
	}
	return events
}

func policySlicesEqual(a []string, b []string) bool {
	if len(a) != len(b) {
		return false
	}
	for idx := range a {
		if a[idx] != b[idx] {
			return false
		}
	}
	return true
}

func (r *GameRoom) lockUnitResolutionOrders() {
	state := r.State()
	if state == nil {
		return
	}
	if state.TurnRuntime.Resolving.UnitOrders == nil {
		state.TurnRuntime.Resolving.UnitOrders = make(map[string]domain.UnitResolutionOrder)
	}
	clear(state.TurnRuntime.Resolving.UnitOrders)

	for unitID, march := range state.TurnRuntime.Resolving.ActiveMarches {
		state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
			PlayerID:     march.PlayerID,
			UnitID:       unitID,
			Action:       domain.UnitResolutionActionMove,
			TargetNodeID: march.DestinationNodeID,
			PathNodeIDs:  append([]string(nil), march.LastPreview.PathNodeIDs...),
		}
	}

	for unitID, directive := range state.TurnRuntime.Planning.UnitOrders {
		order := gameorders.FromDirective(directive)
		if resolutionOrder, ok := order.ToResolutionOrder(); ok {
			if resolutionOrder.Action == domain.UnitResolutionActionMove {
				if march, ok := state.TurnRuntime.Resolving.ActiveMarches[unitID]; ok && len(march.LastPreview.PathNodeIDs) > 0 {
					resolutionOrder.TargetNodeID = march.DestinationNodeID
					resolutionOrder.PathNodeIDs = append([]string(nil), march.LastPreview.PathNodeIDs...)
				} else if preview, ok := r.buildRoutePreview(unitID, resolutionOrder.TargetNodeID); ok {
					resolutionOrder.PathNodeIDs = append([]string(nil), preview.PathNodeIDs...)
				}
			}
			state.TurnRuntime.Resolving.UnitOrders[unitID] = resolutionOrder.Normalized()
			continue
		}

		if gameorders.UnitAction(order.Action) == gameorders.ActionSettleCity && strings.TrimSpace(order.TargetNodeID) != "" {
			state.TurnRuntime.Resolving.UnitOrders[unitID] = domain.UnitResolutionOrder{
				PlayerID:     order.PlayerID,
				UnitID:       order.UnitID,
				Action:       domain.UnitResolutionActionMove,
				TargetNodeID: order.TargetNodeID,
			}
		}
	}
}
