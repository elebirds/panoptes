package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

type TurnStartedEvent struct {
	Turn  int
	Phase string
}

func (e TurnStartedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.Turn = e.Turn
	state.Phase = e.Phase
}

func (e TurnStartedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e TurnStartedEvent) String() string {
	return fmt.Sprintf("TurnStartedEvent turn=%d phase=%s", e.Turn, e.Phase)
}

type PhaseChangedEvent struct {
	From string
	To   string
}

func (e PhaseChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	state.Phase = e.To
}

func (e PhaseChangedEvent) ClientPayload() *pb.CombatEvent { return nil }

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
}

func (e GameOverEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e GameOverEvent) String() string {
	return fmt.Sprintf("GameOverEvent winner=%s reason=%s", e.WinnerID, e.Reason)
}

type PlayerReconnectedEvent struct {
	PlayerID string
}

func (e PlayerReconnectedEvent) Apply(donburi.World, *domain.GameState) {}

func (e PlayerReconnectedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e PlayerReconnectedEvent) String() string {
	return fmt.Sprintf("PlayerReconnectedEvent player=%s", e.PlayerID)
}
