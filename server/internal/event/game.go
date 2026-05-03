// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现事件模型的对局事件与状态辅助。

package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type TurnStartedEvent struct {
	Turn  int
	Phase string
}

func (e TurnStartedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.Turn = e.Turn
	state.Phase = e.Phase
	state.RefreshStructuredModel()
}

func (e TurnStartedEvent) Kind() string { return "turn_started" }

func (e TurnStartedEvent) String() string {
	return fmt.Sprintf("TurnStartedEvent turn=%d phase=%s", e.Turn, e.Phase)
}

type PhaseChangedEvent struct {
	From string
	To   string
}

func (e PhaseChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.Phase = e.To
	state.RefreshStructuredModel()
}

func (e PhaseChangedEvent) Kind() string { return "phase_changed" }

func (e PhaseChangedEvent) String() string {
	return fmt.Sprintf("PhaseChangedEvent from=%s to=%s", e.From, e.To)
}

type GameOverEvent struct {
	WinnerID  string
	Reason    string
	Narrative string
}

func (e GameOverEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.IsOver = true
	state.WinnerID = e.WinnerID
	state.OverReason = e.Reason
	state.Narrative = e.Narrative
	state.RefreshStructuredModel()
}

func (e GameOverEvent) Kind() string { return "game_over" }

func (e GameOverEvent) String() string {
	return fmt.Sprintf("GameOverEvent winner=%s reason=%s", e.WinnerID, e.Reason)
}

type PlayerReconnectedEvent struct {
	PlayerID string
}

func (e PlayerReconnectedEvent) Apply(donburi.World, *domain.GameState) {}

func (e PlayerReconnectedEvent) Kind() string { return "player_reconnected" }

func (e PlayerReconnectedEvent) String() string {
	return fmt.Sprintf("PlayerReconnectedEvent player=%s", e.PlayerID)
}
