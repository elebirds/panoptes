package domain

import (
	"github.com/yohamta/donburi"
)

type GameMeta struct {
	GameID    string
	Narrative string
}

type TurnClock struct {
	Turn  int
	Phase string
}

type GameOutcome struct {
	IsOver   bool
	WinnerID string
	Reason   string
}

type WorldState struct {
	World     donburi.World
	Map       *MapData
	NodeIndex map[string]donburi.Entity
}

type PlayerStore struct {
	Players map[string]*PlayerState
}

type RuntimeState struct {
	Planning  *PlanningInputs
	Resolving *ResolvingState
}

func (s *GameState) RefreshStructuredModel() {
	if s == nil {
		return
	}
	s.Meta = GameMeta{
		GameID:    s.GameID,
		Narrative: s.Narrative,
	}
	s.Clock = TurnClock{
		Turn:  s.Turn,
		Phase: s.Phase,
	}
	s.Outcome = GameOutcome{
		IsOver:   s.IsOver,
		WinnerID: s.WinnerID,
		Reason:   s.OverReason,
	}
	s.WorldState = WorldState{
		World:     s.World,
		Map:       s.Map,
		NodeIndex: s.NodeIndex,
	}
	s.PlayerStore = PlayerStore{
		Players: s.Players,
	}
	s.Runtime = RuntimeState{
		Planning:  &s.TurnRuntime.Planning,
		Resolving: &s.TurnRuntime.Resolving,
	}
}
