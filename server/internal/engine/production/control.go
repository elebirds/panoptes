package production

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type BuildingLifecycleSystem struct{}

func (s *BuildingLifecycleSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if entry == nil || !entry.HasComponent(ecs.BuildingC) {
			return
		}
		building := ecs.BuildingC.Get(entry)
		cfg, _ := staticdata.Default().GetBuilding(string(building.Type))
		if strings.EqualFold(string(building.Type), "city_core") {
			events = append(events, s.captureCityIfNeeded(world, state, entry)...)
			return
		}
		if strings.EqualFold(strings.TrimSpace(cfg.BuildingScope), "out_of_city") {
			events = append(events, s.advanceFacilityTakeover(world, state, entry)...)
		}
	})
	return events
}

func (s *BuildingLifecycleSystem) captureCityIfNeeded(world donburi.World, state *domain.GameState, entry *donburi.Entry) []event.Event {
	if state == nil || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return nil
	}
	building := ecs.BuildingC.Get(entry)
	if building.HP > 0 {
		return nil
	}
	cityID := ecs.ResolveCityID(entry)
	if strings.TrimSpace(cityID) == "" {
		return nil
	}
	ownerState := state.Players[building.Owner]
	if ownerState != nil && ownerState.CapitalCityID == cityID {
		return nil
	}
	controller, contested := exclusiveEnemyController(state, entry, building.Owner)
	if contested || controller == "" {
		return nil
	}
	events := []event.Event{event.CityCapturedEvent{
		NodeID:       ecs.NodeC.Get(entry).ID,
		CityID:       cityID,
		OldOwnerID:   building.Owner,
		NewOwnerID:   controller,
		OnlineOnTurn: state.Turn + 1,
	}}
	ecs.NodesWithBuilding(world).Each(world, func(candidate *donburi.Entry) {
		if candidate == nil || candidate == entry || !candidate.HasComponent(ecs.BuildingC) {
			return
		}
		current := ecs.BuildingC.Get(candidate)
		if strings.TrimSpace(current.Owner) != strings.TrimSpace(building.Owner) {
			return
		}
		if ecs.ResolveCityID(candidate) != cityID {
			return
		}
		cfg, ok := staticdata.Default().GetBuilding(string(current.Type))
		if !ok {
			return
		}
		if hasAnyTag(cfg.Tags, "defense", "governance") {
			events = append(events, event.BuildingRuinedEvent{
				NodeID:     ecs.NodeC.Get(candidate).ID,
				NewOwnerID: controller,
				Reason:     "city_captured",
			})
		}
	})
	return events
}

func (s *BuildingLifecycleSystem) advanceFacilityTakeover(world donburi.World, state *domain.GameState, entry *donburi.Entry) []event.Event {
	if state == nil || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return nil
	}
	building := ecs.BuildingC.Get(entry)
	if !entry.HasComponent(ecs.FacilityTakeoverC) {
		return nil
	}
	takeover := ecs.FacilityTakeoverC.Get(entry)
	required := takeover.Required
	if required <= 0 {
		required = staticdata.Default().Rules().FacilityTakeoverTurns
		if required <= 0 {
			required = 1
		}
	}
	controller, contested := exclusiveEnemyController(state, entry, building.Owner)
	switch {
	case contested:
		return []event.Event{event.FacilityTakeoverProgressedEvent{
			NodeID:     ecs.NodeC.Get(entry).ID,
			Progress:   0,
			Required:   required,
			Status:     domain.BuildingStatusContested,
			Reason:     "multiple_controllers",
		}}
	case controller == "":
		status, reason := domain.BuildingLifecycleStateAtTurn(entry, state.Turn)
		if strings.TrimSpace(reason) == "pending_activation" {
			status = domain.BuildingStatusDisabled
			reason = "pending_activation"
		} else {
			status = domain.BuildingStatusIdle
			reason = ""
		}
		return []event.Event{event.FacilityTakeoverProgressedEvent{
			NodeID:   ecs.NodeC.Get(entry).ID,
			Progress: 0,
			Required: required,
			Status:   status,
			Reason:   reason,
		}}
	default:
		nextProgress := 1
		if takeover.ControllerPlayerID == controller {
			nextProgress = takeover.Progress + 1
		}
		if nextProgress >= required {
			pos := ecs.PositionC.Get(entry)
			serviceCityID := event.NearestOwnedCityID(state, controller, domain.Position{X: pos.X, Y: pos.Y}, state.Players[controller].CapitalCityID)
			return []event.Event{event.FacilityTakeoverCompletedEvent{
				NodeID:        ecs.NodeC.Get(entry).ID,
				NewOwnerID:    controller,
				ServiceCityID: serviceCityID,
				OnlineOnTurn:  state.Turn + 1,
			}}
		}
		return []event.Event{event.FacilityTakeoverProgressedEvent{
			NodeID:             ecs.NodeC.Get(entry).ID,
			ControllerPlayerID: controller,
			Progress:           nextProgress,
			Required:           required,
			Status:             domain.BuildingStatusTakeover,
			Reason:             "enemy_control",
		}}
	}
}

func exclusiveEnemyController(state *domain.GameState, entry *donburi.Entry, ownerID string) (string, bool) {
	if state == nil || state.World == nil || entry == nil {
		return "", false
	}
	pos := ecs.PositionC.Get(entry)
	unitsByFaction := domain.UnitsByFactionAtNode(state.World, domain.Position{X: pos.X, Y: pos.Y})
	if len(unitsByFaction) == 0 {
		return "", false
	}
	controller := ""
	for faction := range unitsByFaction {
		if strings.TrimSpace(faction) == strings.TrimSpace(ownerID) {
			if len(unitsByFaction) == 1 {
				return "", false
			}
			return "", true
		}
		if controller == "" {
			controller = faction
			continue
		}
		if controller != faction {
			return "", true
		}
	}
	return controller, false
}

func hasAnyTag(tags []string, expected ...string) bool {
	for _, tag := range tags {
		current := strings.ToLower(strings.TrimSpace(tag))
		for _, want := range expected {
			if current == strings.ToLower(strings.TrimSpace(want)) {
				return true
			}
		}
	}
	return false
}
