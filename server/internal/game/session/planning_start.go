// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载对局运行时会话生命周期拆分后的子职责逻辑。

package session

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/event"
	gameprojection "github.com/elebirds/panoptes/internal/game/projection"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildPlanningStartMessage(state *domain.GameState, playerID string, phase string, planningStartEvents []event.Event) *pb.MsgPlanningStart {
	return BuildPlanningStartMessageFromObservation(state, gamequery.NewObservationStore().BuildObservation(state, playerID), phase, planningStartEvents)
}

func BuildPlanningStartMessageFromObservation(state *domain.GameState, observation *gamequery.ObservationSnapshot, phase string, planningStartEvents []event.Event) *pb.MsgPlanningStart {
	if state == nil || phase != domain.PhasePlanning.String() {
		return nil
	}
	observed := gameprojection.ProjectObservedState(state, observation, gameprojection.ObservedStateOptions{
		UseSinglePlayerFallback:        true,
		ObservationCollectionsAreFinal: true,
	})
	playerID := observed.PlayerID

	rules := staticdata.Default().Rules()
	currentPolicy := ""
	tokens := int32(rules.TokensPerTurn)
	if playerState := state.Players[playerID]; playerState != nil {
		currentPolicy = string(playerState.Policy)
		tokens = int32(playerState.TokensLeft)
	}

	msg := &pb.MsgPlanningStart{
		Timeout:                int32(rules.TurnTimeLimitPlanning),
		Turn:                   int32(state.Turn),
		Tokens:                 tokens,
		Phase:                  phase,
		ActiveNationalPolicyId: currentPolicy,
		MinisterDrafts:         gamequery.BuildMinisterDraftViews(state, playerID),
		MyPlayer:               observed.MyPlayer,
		Nodes:                  observed.Nodes,
		Units:                  observed.Units,
		InformationReport:      gamequery.BuildInformationReport(observation),
		// planning_start_events 是本轮改造新增的正式事件面。
		// 它只承载“开回合才正式生效”的事件，例如 technology_activated。
		PlanningStartEvents: gameprojection.ProjectPlanningStartEvents(int32(state.Turn), planningStartEvents),
	}
	msg.Snapshot = observed.PlanningSnapshot(phase)
	return msg
}
