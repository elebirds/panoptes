package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

type MinisterActedEvent struct {
	MinisterRole string
	PlayerID     string
	ActionID     string
	Actions      []*pb.MinisterActionItem
	Report       string
}

func (e MinisterActedEvent) Apply(donburi.World, *domain.GameState) {}

func (e MinisterActedEvent) ClientPayload() *pb.CombatEvent { return nil }

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
}

func (e PolicyChangedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e PolicyChangedEvent) String() string {
	return fmt.Sprintf("PolicyChangedEvent player=%s %s->%s", e.PlayerID, e.OldPolicy, e.NewPolicy)
}

type TokenUsedEvent struct {
	PlayerID   string
	Action     string
	TokensLeft int
}

func (e TokenUsedEvent) Apply(_ donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	playerState.TokensLeft = e.TokensLeft
}

func (e TokenUsedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e TokenUsedEvent) String() string {
	return fmt.Sprintf("TokenUsedEvent player=%s action=%s tokens_left=%d", e.PlayerID, e.Action, e.TokensLeft)
}
