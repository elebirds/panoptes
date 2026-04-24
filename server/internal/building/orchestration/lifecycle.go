package orchestration

import (
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type LifecycleSystem struct{}

// LifecycleSystem 是建筑主模块在 resolving 期的唯一运行态阶段。
// 它只负责建筑是否在线、前线设施接管、以及非主城城市陷落后的建筑命运，
// 不再作为 economy runner 的内部子阶段存在。
func (s *LifecycleSystem) Run(world donburi.World, state *domain.GameState) []event.Event {
	events := make([]event.Event, 0)
	if state == nil {
		return events
	}
	ecs.NodesWithBuilding(world).Each(world, func(entry *donburi.Entry) {
		if entry == nil || !entry.HasComponent(ecs.BuildingC) {
			return
		}
		buildingComp := ecs.BuildingC.Get(entry)
		cfg, _ := staticdata.Default().GetBuilding(string(buildingComp.Type))
		if building.IsCityCore(entry) {
			events = append(events, s.captureCityIfNeeded(world, state, entry)...)
			return
		}
		if domain.NormalizeBuildingScope(cfg.BuildingScope) == domain.BuildingScopeOutOfCity {
			events = append(events, s.advanceFacilityTakeover(world, state, entry)...)
		}
	})
	return events
}

func (s *LifecycleSystem) captureCityIfNeeded(world donburi.World, state *domain.GameState, entry *donburi.Entry) []event.Event {
	if state == nil || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return nil
	}
	buildingComp := ecs.BuildingC.Get(entry)
	if buildingComp.HP > 0 {
		return nil
	}
	cityID := building.ResolveCityID(entry)
	if strings.TrimSpace(cityID) == "" {
		return nil
	}
	ownerState := state.Players[buildingComp.Owner]
	if ownerState != nil && ownerState.CapitalCityID == cityID {
		return nil
	}
	controller, contested := exclusiveEnemyController(state, entry, buildingComp.Owner)
	if contested || controller == "" {
		return nil
	}
	// 这里只发一个聚合的 CityCapturedEvent。
	// 城内建筑到底是转 ruined 还是 pending_activation，由 Apply 阶段复用 building 规则统一决定。
	return []event.Event{event.CityCapturedEvent{
		NodeID:       ecs.NodeC.Get(entry).ID,
		CityID:       cityID,
		OldOwnerID:   buildingComp.Owner,
		NewOwnerID:   controller,
		OnlineOnTurn: state.Turn + 1,
	}}
}

func (s *LifecycleSystem) advanceFacilityTakeover(world donburi.World, state *domain.GameState, entry *donburi.Entry) []event.Event {
	if state == nil || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return nil
	}
	buildingComp := ecs.BuildingC.Get(entry)
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
	controller, contested := exclusiveEnemyController(state, entry, buildingComp.Owner)
	switch {
	case contested:
		// 多方争夺时设施不会累计 takeover，只进入 contested。
		return []event.Event{event.FacilityTakeoverProgressedEvent{
			NodeID:   ecs.NodeC.Get(entry).ID,
			Progress: 0,
			Required: required,
			Status:   domain.BuildingStatusContested,
			Reason:   "multiple_controllers",
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
		// takeover 必须由同一控制者连续维持；换人后会从 1 重新开始。
		if nextProgress >= required {
			pos := ecs.PositionC.Get(entry)
			fallback := ""
			if player := state.Players[controller]; player != nil {
				fallback = player.CapitalCityID
			}
			serviceCityID := event.NearestOwnedCityID(state, controller, domain.Position{Q: pos.Q, R: pos.R}, fallback)
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
	unitsByFaction := domain.UnitsByFactionAtNode(state.World, domain.Position{Q: pos.Q, R: pos.R})
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
