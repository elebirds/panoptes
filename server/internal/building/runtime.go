package building

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/yohamta/donburi"
)

func RuntimeState(entry *donburi.Entry, currentTurn int) (string, int, int) {
	if entry == nil || !entry.HasComponent(domain.BuildingC) {
		return domain.BuildingStatusEmpty, 0, 0
	}

	progress := 0
	required := 0
	if entry.HasComponent(domain.FacilityTakeoverC) {
		takeover := domain.FacilityTakeoverC.Get(entry)
		progress = takeover.Progress
		required = takeover.Required
	}

	status, _ := domain.BuildingLifecycleStateAtTurn(entry, currentTurn)
	switch status {
	case domain.BuildingStatusDisabled,
		domain.BuildingStatusContested,
		domain.BuildingStatusTakeover,
		domain.BuildingStatusRuined:
		return status, progress, required
	case domain.BuildingStatusBlocked:
		return domain.BuildingStatusBlocked, progress, required
	case domain.BuildingStatusActive:
		return domain.BuildingStatusActive, progress, required
	}

	if entry.HasComponent(domain.BuildingOperationC) {
		operation := domain.BuildingOperationC.Get(entry)
		if strings.TrimSpace(operation.BlockedReason) != "" {
			return domain.BuildingStatusBlocked, progress, required
		}
		if strings.TrimSpace(operation.SelectedRecipeID) != "" {
			return domain.BuildingStatusActive, progress, required
		}
	}
	return domain.BuildingStatusIdle, progress, required
}

func IsOperationalAtTurn(entry *donburi.Entry, currentTurn int) bool {
	return domain.BuildingOperationalAtTurn(entry, currentTurn)
}
