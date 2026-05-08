package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

type InstitutionLoadoutChangedEvent struct {
	PlayerID       string
	InstitutionIDs []string
	ActivationTurn int
}

type InstitutionLoadoutActivatedEvent struct {
	PlayerID       string
	InstitutionIDs []string
}

func (e InstitutionLoadoutChangedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	playerState.Institutions.PendingInstitutionIDs = append([]string(nil), e.InstitutionIDs...)
	playerState.Institutions.PendingActivationTurn = e.ActivationTurn
}

func (e InstitutionLoadoutChangedEvent) Kind() string { return "institution_loadout_changed" }

func (e InstitutionLoadoutChangedEvent) String() string {
	return fmt.Sprintf("InstitutionLoadoutChangedEvent player=%s activation_turn=%d", e.PlayerID, e.ActivationTurn)
}

func (e InstitutionLoadoutActivatedEvent) Apply(_ donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	playerState, ok := state.Players[e.PlayerID]
	if !ok || playerState == nil {
		return
	}
	playerState.Institutions.EnsureMaps()
	playerState.Institutions.ActiveInstitutionIDs = append([]string(nil), e.InstitutionIDs...)
	playerState.Institutions.PendingInstitutionIDs = nil
	playerState.Institutions.PendingActivationTurn = 0
	state.RefreshBuildingMaxHPForPlayer(e.PlayerID)
}

func (e InstitutionLoadoutActivatedEvent) Kind() string { return "institution_loadout_activated" }

func (e InstitutionLoadoutActivatedEvent) String() string {
	return fmt.Sprintf("InstitutionLoadoutActivatedEvent player=%s institutions=%v", e.PlayerID, e.InstitutionIDs)
}
