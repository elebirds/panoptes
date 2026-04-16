package domain

import (
	"strings"

	"github.com/yohamta/donburi"
)

const (
	BuildingStatusEmpty      = "empty"
	BuildingStatusIdle       = "idle"
	BuildingStatusActive     = "active"
	BuildingStatusBlocked    = "blocked"
	BuildingStatusDisabled   = "disabled"
	BuildingStatusContested  = "contested"
	BuildingStatusTakeover   = "takeover"
	BuildingStatusRuined     = "ruined"
	pendingActivationReason  = "pending_activation"
)

func NormalizeBuildingStatus(status string) string {
	switch strings.ToLower(strings.TrimSpace(status)) {
	case BuildingStatusActive:
		return BuildingStatusActive
	case BuildingStatusBlocked:
		return BuildingStatusBlocked
	case BuildingStatusDisabled:
		return BuildingStatusDisabled
	case BuildingStatusContested:
		return BuildingStatusContested
	case BuildingStatusTakeover:
		return BuildingStatusTakeover
	case BuildingStatusRuined:
		return BuildingStatusRuined
	case BuildingStatusEmpty:
		return BuildingStatusEmpty
	default:
		return BuildingStatusIdle
	}
}

func SetBuildingLifecycleState(entry *donburi.Entry, status string, reason string, onlineOnTurn int) {
	if entry == nil {
		return
	}
	if !entry.HasComponent(BuildingStateC) {
		entry.AddComponent(BuildingStateC)
	}
	current := BuildingStateComp{}
	if entry.HasComponent(BuildingStateC) {
		current = *BuildingStateC.Get(entry)
	}
	current.Status = NormalizeBuildingStatus(status)
	current.Reason = strings.TrimSpace(reason)
	current.Disabled = buildingStatusDisables(current.Status)
	current.DisabledReason = current.Reason
	if onlineOnTurn > 0 {
		current.OnlineOnTurn = onlineOnTurn
	} else if current.OnlineOnTurn < 0 {
		current.OnlineOnTurn = 0
	}
	BuildingStateC.SetValue(entry, current)
}

func IsCityOnline(state *GameState, city *CityState) bool {
	if state == nil || city == nil {
		return false
	}
	return city.OnlineOnTurn <= 0 || state.Turn >= city.OnlineOnTurn
}

func (s *GameState) FindCityState(cityID string) (*PlayerState, *CityState) {
	if s == nil || strings.TrimSpace(cityID) == "" {
		return nil, nil
	}
	for _, playerState := range s.Players {
		if playerState == nil {
			continue
		}
		if city, ok := playerState.Cities[cityID]; ok && city != nil {
			return playerState, city
		}
	}
	return nil, nil
}

func (s *GameState) IsCityOnlineForPlayer(playerID string, cityID string) bool {
	if s == nil {
		return false
	}
	playerState, ok := s.Players[playerID]
	if !ok || playerState == nil {
		return false
	}
	city := playerState.Cities[cityID]
	return IsCityOnline(s, city)
}

func BuildingLifecycleStateAtTurn(entry *donburi.Entry, currentTurn int) (string, string) {
	if entry == nil {
		return BuildingStatusEmpty, ""
	}
	if !entry.HasComponent(BuildingStateC) {
		return BuildingStatusIdle, ""
	}
	state := BuildingStateC.Get(entry)
	status := NormalizeBuildingStatus(state.Status)
	reason := strings.TrimSpace(state.Reason)
	if state.Disabled && strings.TrimSpace(state.Status) == "" {
		status = BuildingStatusDisabled
		if reason == "" {
			reason = strings.TrimSpace(state.DisabledReason)
		}
	}
	if state.OnlineOnTurn > 0 && currentTurn > 0 && currentTurn < state.OnlineOnTurn {
		return BuildingStatusDisabled, pendingActivationReason
	}
	if status == BuildingStatusDisabled && reason == pendingActivationReason && (state.OnlineOnTurn <= 0 || currentTurn >= state.OnlineOnTurn) {
		return BuildingStatusIdle, ""
	}
	if status == "" {
		status = BuildingStatusIdle
	}
	if reason == "" {
		reason = strings.TrimSpace(state.DisabledReason)
	}
	return status, reason
}

func BuildingOperationalAtTurn(entry *donburi.Entry, currentTurn int) bool {
	status, _ := BuildingLifecycleStateAtTurn(entry, currentTurn)
	switch status {
	case BuildingStatusDisabled, BuildingStatusContested, BuildingStatusTakeover, BuildingStatusRuined:
		return false
	default:
		return true
	}
}

func buildingStatusDisables(status string) bool {
	switch NormalizeBuildingStatus(status) {
	case BuildingStatusDisabled, BuildingStatusContested, BuildingStatusTakeover, BuildingStatusRuined:
		return true
	default:
		return false
	}
}
