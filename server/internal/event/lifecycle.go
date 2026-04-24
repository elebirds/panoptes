package event

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/algo/geometry"
	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type FacilityTakeoverProgressedEvent struct {
	NodeID             string
	ControllerPlayerID string
	Progress           int
	Required           int
	Status             string
	Reason             string
}

func (e FacilityTakeoverProgressedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	if !entry.HasComponent(ecs.FacilityTakeoverC) {
		entry.AddComponent(ecs.FacilityTakeoverC)
		ecs.FacilityTakeoverC.SetValue(entry, ecs.FacilityTakeoverComp{})
	}
	takeover := ecs.FacilityTakeoverC.Get(entry)
	takeover.Progress = e.Progress
	if e.Required > 0 {
		takeover.Required = e.Required
	}
	takeover.Completed = false
	takeover.ControllerPlayerID = strings.TrimSpace(e.ControllerPlayerID)

	node := ecs.NodeC.Get(entry)
	switch {
	case strings.TrimSpace(e.ControllerPlayerID) != "":
		node.Owner = strings.TrimSpace(e.ControllerPlayerID)
	case domain.NormalizeBuildingStatus(e.Status) == domain.BuildingStatusContested:
		node.Owner = ""
	default:
		node.Owner = ecs.BuildingC.Get(entry).Owner
	}

	status := domain.NormalizeBuildingStatus(e.Status)
	if status == domain.BuildingStatusIdle && strings.TrimSpace(e.ControllerPlayerID) != "" {
		status = domain.BuildingStatusTakeover
	}
	domain.SetBuildingLifecycleState(entry, status, e.Reason, 0)
}

func (e FacilityTakeoverProgressedEvent) Kind() string { return "facility_takeover_progressed" }

func (e FacilityTakeoverProgressedEvent) String() string {
	return fmt.Sprintf("FacilityTakeoverProgressedEvent node=%s controller=%s progress=%d", e.NodeID, e.ControllerPlayerID, e.Progress)
}

type FacilityTakeoverCompletedEvent struct {
	NodeID        string
	NewOwnerID    string
	ServiceCityID string
	OnlineOnTurn  int
}

func (e FacilityTakeoverCompletedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	currentBuilding := ecs.BuildingC.Get(entry)
	currentBuilding.Owner = e.NewOwnerID

	node := ecs.NodeC.Get(entry)
	node.Owner = e.NewOwnerID
	node.TerritoryOwner = e.NewOwnerID

	building.Rebind(entry, e.ServiceCityID, e.ServiceCityID)
	if entry.HasComponent(ecs.FacilityTakeoverC) {
		takeover := ecs.FacilityTakeoverC.Get(entry)
		takeover.Progress = 0
		takeover.ControllerPlayerID = e.NewOwnerID
		takeover.Completed = true
	}
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusDisabled, "pending_activation", e.OnlineOnTurn)
}

func (e FacilityTakeoverCompletedEvent) Kind() string { return "facility_takeover_completed" }

func (e FacilityTakeoverCompletedEvent) String() string {
	return fmt.Sprintf("FacilityTakeoverCompletedEvent node=%s owner=%s city=%s", e.NodeID, e.NewOwnerID, e.ServiceCityID)
}

type BuildingRuinedEvent struct {
	NodeID     string
	NewOwnerID string
	Reason     string
}

func (e BuildingRuinedEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	if strings.TrimSpace(e.NewOwnerID) != "" {
		building := ecs.BuildingC.Get(entry)
		building.Owner = e.NewOwnerID
		node := ecs.NodeC.Get(entry)
		node.Owner = e.NewOwnerID
		node.TerritoryOwner = e.NewOwnerID
	}
	reason := strings.TrimSpace(e.Reason)
	if reason == "" {
		reason = "city_captured"
	}
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusRuined, reason, 0)
}

func (e BuildingRuinedEvent) Kind() string { return "building_ruined" }

func (e BuildingRuinedEvent) String() string {
	return fmt.Sprintf("BuildingRuinedEvent node=%s owner=%s", e.NodeID, e.NewOwnerID)
}

type CityCapturedEvent struct {
	NodeID       string
	CityID       string
	OldOwnerID   string
	NewOwnerID   string
	OnlineOnTurn int
}

func (e CityCapturedEvent) Apply(world donburi.World, state *domain.GameState) {
	if state == nil {
		return
	}
	entry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !entry.HasComponent(ecs.BuildingC) {
		return
	}
	oldPlayer := state.Players[e.OldOwnerID]
	if oldPlayer != nil && oldPlayer.CapitalCityID == e.CityID {
		CityCoreDestroyedEvent{NodeID: e.NodeID, ConquerorFaction: e.NewOwnerID}.Apply(world, state)
		return
	}
	newPlayer := state.Players[e.NewOwnerID]
	if newPlayer == nil {
		return
	}

	var cityState *domain.CityState
	if oldPlayer != nil {
		cityState = oldPlayer.Cities[e.CityID]
		delete(oldPlayer.Cities, e.CityID)
	}
	if cityState == nil {
		cityState = &domain.CityState{
			CityID:              e.CityID,
			CoreNodeID:          e.CityID,
			TerritoryBaseRadius: staticdata.Default().Rules().InitialCityTerritoryRadius,
		}
	}
	cityState.OwnerID = e.NewOwnerID
	cityState.CoreNodeID = e.CityID
	cityState.OnlineOnTurn = e.OnlineOnTurn
	if newPlayer.Cities == nil {
		newPlayer.Cities = make(map[string]*domain.CityState)
	}
	newPlayer.Cities[e.CityID] = cityState

	coreBuilding := ecs.BuildingC.Get(entry)
	coreBuilding.Owner = e.NewOwnerID
	if coreBuilding.MaxHP > 0 {
		coreBuilding.HP = coreBuilding.MaxHP
	}
	coreNode := ecs.NodeC.Get(entry)
	coreNode.Owner = e.NewOwnerID
	coreNode.TerritoryOwner = e.NewOwnerID
	building.SetBinding(entry, domain.BuildingScopeCityCore, e.CityID, e.CityID)
	domain.SetBuildingLifecycleState(entry, domain.BuildingStatusDisabled, "pending_activation", e.OnlineOnTurn)

	footprintEntries, _, reason := building.TerritoryFootprint(state, entry)
	if reason == "" {
		for _, footprintEntry := range footprintEntries {
			if footprintEntry == nil {
				continue
			}
			node := ecs.NodeC.Get(footprintEntry)
			node.Owner = e.NewOwnerID
			node.TerritoryOwner = e.NewOwnerID
		}
	}

	ecs.NodesWithBuilding(world).Each(world, func(candidate *donburi.Entry) {
		if candidate == nil || candidate == entry || !candidate.HasComponent(ecs.BuildingC) {
			return
		}
		currentBuilding := ecs.BuildingC.Get(candidate)
		if strings.TrimSpace(currentBuilding.Owner) != strings.TrimSpace(e.OldOwnerID) {
			return
		}
		if building.ResolveCityID(candidate) != e.CityID {
			return
		}
		cfg, ok := staticdata.Default().GetBuilding(string(currentBuilding.Type))
		if !ok {
			return
		}
		if domain.NormalizeBuildingScope(cfg.BuildingScope) == domain.BuildingScopeOutOfCity {
			return
		}

		currentBuilding.Owner = e.NewOwnerID
		building.Rebind(candidate, e.CityID, e.CityID)
		candidateNode := ecs.NodeC.Get(candidate)
		candidateNode.Owner = e.NewOwnerID
		candidateNode.TerritoryOwner = e.NewOwnerID
		status, reason := building.CapturedLifecycleForBuilding(cfg)
		onlineOnTurn := 0
		if status == domain.BuildingStatusDisabled {
			onlineOnTurn = e.OnlineOnTurn
		}
		domain.SetBuildingLifecycleState(candidate, status, reason, onlineOnTurn)
	})
}

func (e CityCapturedEvent) Kind() string { return "city_captured" }

func (e CityCapturedEvent) String() string {
	return fmt.Sprintf("CityCapturedEvent city=%s old_owner=%s new_owner=%s", e.CityID, e.OldOwnerID, e.NewOwnerID)
}

func NearestOwnedCityID(state *domain.GameState, playerID string, pos domain.Position, fallback string) string {
	if state == nil {
		return strings.TrimSpace(fallback)
	}
	playerState := state.Players[playerID]
	if playerState == nil || len(playerState.Cities) == 0 {
		return strings.TrimSpace(fallback)
	}
	bestID := strings.TrimSpace(fallback)
	bestDistance := -1
	for cityID, city := range playerState.Cities {
		if city == nil || strings.TrimSpace(city.CoreNodeID) == "" {
			continue
		}
		entry, ok := state.GetNode(city.CoreNodeID)
		if !ok {
			continue
		}
		cityPos := ecs.PositionC.Get(entry)
		distance := geometry.AxialDistance(pos, domain.Position{Q: cityPos.Q, R: cityPos.R})
		if bestDistance == -1 || distance < bestDistance {
			bestDistance = distance
			bestID = cityID
		}
	}
	return strings.TrimSpace(bestID)
}
