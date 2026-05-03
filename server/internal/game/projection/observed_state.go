// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载服务端权威状态到客户端观察视图的投影组合逻辑。

package projection

import (
	"github.com/elebirds/panoptes/internal/domain"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type ObservedStateOptions struct {
	UseSinglePlayerFallback        bool
	ObservationCollectionsAreFinal bool
}

type ObservedState struct {
	State       *domain.GameState
	Observation *gamequery.ObservationSnapshot
	PlayerID    string
	MyPlayer    *pb.PlayerView
	Nodes       []*pb.NodeView
	Units       []*pb.UnitView
}

// ProjectObservedState 是 planning-start 与 game-sync 共用的观察视图组合点。
// 两种消息的 wire shape 不同，但 MyPlayer/Nodes/Units/snapshot 的 fallback 规则必须保持一致。
func ProjectObservedState(state *domain.GameState, observation *gamequery.ObservationSnapshot, options ObservedStateOptions) ObservedState {
	playerID := observedPlayerID(state, observation, options.UseSinglePlayerFallback)
	view := ObservedState{
		State:       state,
		Observation: observation,
		PlayerID:    playerID,
	}

	if observation != nil {
		view.MyPlayer = observation.MyPlayer
		view.Nodes = observation.Nodes
		view.Units = observation.Units
	}
	if view.MyPlayer == nil {
		view.MyPlayer = gamequery.BuildPlayerView(state, playerID)
	}
	if observation == nil || (!options.ObservationCollectionsAreFinal && view.Nodes == nil) {
		view.Nodes = gamequery.BuildNodeViews(state, playerID)
	}
	if observation == nil || (!options.ObservationCollectionsAreFinal && view.Units == nil) {
		view.Units = gamequery.BuildUnitViews(state)
	}
	return view
}

func (o ObservedState) PlanningSnapshot(phaseOverride string) *pb.MsgPlanningSnapshot {
	snapshot := gamequery.BuildPlanningSnapshot(o.State, o.PlayerID)
	if snapshot != nil && phaseOverride != "" {
		snapshot.Phase = phaseOverride
	}
	return snapshot
}

func observedPlayerID(state *domain.GameState, observation *gamequery.ObservationSnapshot, useSinglePlayerFallback bool) string {
	if observation != nil && observation.ViewerID != "" {
		return observation.ViewerID
	}
	if !useSinglePlayerFallback || state == nil || len(state.Players) != 1 {
		return ""
	}
	for playerID := range state.Players {
		return playerID
	}
	return ""
}
