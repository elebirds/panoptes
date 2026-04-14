package domain

type TurnPhase string

const (
	PhasePlanning  TurnPhase = "planning"
	PhaseResolving TurnPhase = "resolving"
)

func (p TurnPhase) String() string {
	return string(p)
}

func (p TurnPhase) IsPlanning() bool {
	return p == PhasePlanning
}
