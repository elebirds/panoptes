// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的部长相关事件与输入。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type MinisterActedEvent struct {
	MinisterRole string
	PlayerID     string
	ActionID     string
	Report       string
}

func (e MinisterActedEvent) Apply(donburi.World, *domain.GameState) {}

func (e MinisterActedEvent) Kind() string { return "minister_acted" }

func (e MinisterActedEvent) String() string {
	return fmt.Sprintf("MinisterActedEvent player=%s role=%s action_id=%s", e.PlayerID, e.MinisterRole, e.ActionID)
}

type PolicyChangedEvent struct {
	PlayerID  string
	OldPolicy string
	NewPolicy string
}

func (e PolicyChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	playerState.Policy = domain.Policy(e.NewPolicy)
	state.RefreshBuildingMaxHPForPlayer(e.PlayerID)
}

func (e PolicyChangedEvent) Kind() string { return "national_policy_changed" }

func (e PolicyChangedEvent) String() string {
	return fmt.Sprintf("PolicyChangedEvent player=%s %s->%s", e.PlayerID, e.OldPolicy, e.NewPolicy)
}

type TokenUsedEvent struct {
	PlayerID   string
	Action     string // "reveal", "override", "direct_command"
	TokensLeft int
}

func (e TokenUsedEvent) Apply(_ donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	playerState.TokensLeft = e.TokensLeft
}

func (e TokenUsedEvent) Kind() string { return "token_used" }

func (e TokenUsedEvent) String() string {
	return fmt.Sprintf("TokenUsedEvent player=%s action=%s tokens_left=%d", e.PlayerID, e.Action, e.TokensLeft)
}
