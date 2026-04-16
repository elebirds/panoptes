package event

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

type CityFoundedEvent struct {
	PlayerID     string
	UnitID       string
	CityID       string
	CenterNodeID string
	TerritoryIDs []string
	OnlineOnTurn int
}

func (e CityFoundedEvent) Apply(world donburi.World, state *domain.GameState) {
	if world == nil || state == nil {
		return
	}
	for _, nodeID := range e.TerritoryIDs {
		entry, ok := state.GetNode(strings.TrimSpace(nodeID))
		if !ok || entry == nil {
			continue
		}
		node := ecs.NodeC.Get(entry)
		node.Owner = e.PlayerID
		node.TerritoryOwner = e.PlayerID
	}

	centerEntry, ok := state.GetNode(strings.TrimSpace(e.CenterNodeID))
	if !ok || centerEntry == nil {
		return
	}
	ecs.CreateBuilding(world, "city_core", e.PlayerID, e.CityID, centerEntry)
	domain.SetBuildingLifecycleState(centerEntry, domain.BuildingStatusDisabled, "pending_activation", e.OnlineOnTurn)
	centerNode := ecs.NodeC.Get(centerEntry)
	centerNode.Owner = e.PlayerID
	centerNode.TerritoryOwner = e.PlayerID

	cityState := state.EnsureCityState(e.PlayerID, e.CityID)
	if cityState != nil {
		cityState.CoreNodeID = e.CenterNodeID
		cityState.OwnerID = e.PlayerID
		cityState.OnlineOnTurn = e.OnlineOnTurn
	}

	if unitEntry, ok := findUnitEntryByID(world, e.UnitID); ok {
		world.Remove(unitEntry.Entity())
	}
}

func (e CityFoundedEvent) Kind() string { return "city_founded" }

func (e CityFoundedEvent) String() string {
	return fmt.Sprintf("CityFoundedEvent player=%s city=%s center=%s", e.PlayerID, e.CityID, e.CenterNodeID)
}

type CityFoundingFailedEvent struct {
	PlayerID string
	UnitID   string
	Reason   string
}

func (e CityFoundingFailedEvent) Apply(world donburi.World, state *domain.GameState) {
	_ = world
	_ = state
}

func (e CityFoundingFailedEvent) Kind() string { return "settle_city_failed" }

func (e CityFoundingFailedEvent) String() string {
	return fmt.Sprintf("CityFoundingFailedEvent player=%s unit=%s reason=%s", e.PlayerID, e.UnitID, e.Reason)
}

func findUnitEntryByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	if world == nil || strings.TrimSpace(unitID) == "" {
		return nil, false
	}
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}
