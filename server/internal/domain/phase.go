package domain

type TurnPhase string

const (
	PhaseDomesticPlanning  TurnPhase = "domestic_planning"
	PhaseDomesticResolving TurnPhase = "domestic_resolving"
	PhaseCombatPlanning    TurnPhase = "combat_planning"
	PhaseCombatResolving   TurnPhase = "combat_resolving"
)

func (p TurnPhase) String() string {
	return string(p)
}

func (p TurnPhase) IsPlanning() bool {
	return p == PhaseDomesticPlanning || p == PhaseCombatPlanning
}
