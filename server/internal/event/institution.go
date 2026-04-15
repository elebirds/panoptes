package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type InstitutionLoadoutChangedEvent struct {
	PlayerID       string
	PolicyIDs      []string
	ActivationTurn int
}

func (e InstitutionLoadoutChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	playerState.Institutions.PendingPolicyIDs = append([]string(nil), e.PolicyIDs...)
	playerState.Institutions.PendingActivationTurn = e.ActivationTurn
}

func (e InstitutionLoadoutChangedEvent) Kind() string { return "institution_loadout_changed" }

func (e InstitutionLoadoutChangedEvent) String() string {
	return fmt.Sprintf("InstitutionLoadoutChangedEvent player=%s activation_turn=%d", e.PlayerID, e.ActivationTurn)
}
