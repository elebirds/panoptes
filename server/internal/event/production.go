package event

import (
	"fmt"
	"strconv"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type BuildingBuiltEvent struct {
	NodeID       string
	BuildingType string
	Owner        string
	Cost         domain.ResourceBag
}

func (e BuildingBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	ecs.CreateBuilding(world, e.BuildingType, e.Owner, nodeEntry)
	if playerState, ok := state.Players[e.Owner]; ok {
		playerState.Resources = playerState.Resources.Sub(e.Cost)
	}
}

func (e BuildingBuiltEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e BuildingBuiltEvent) String() string {
	return fmt.Sprintf("BuildingBuiltEvent node=%s type=%s owner=%s", e.NodeID, e.BuildingType, e.Owner)
}

type ResourceProducedEvent struct {
	NodeID       string
	ResourceType string
	Amount       int
	Owner        string
}

func (e ResourceProducedEvent) Apply(_ donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.Owner]
	if !ok {
		return
	}
	playerState.Resources.AddAmount(domain.ResourceKey(e.ResourceType), e.Amount)
}

func (e ResourceProducedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e ResourceProducedEvent) String() string {
	return fmt.Sprintf("ResourceProducedEvent node=%s owner=%s %s=+%d", e.NodeID, e.Owner, e.ResourceType, e.Amount)
}

type ResourceFlowedEvent struct {
	FromNodeID string
	ToNodeID   string
	Resources  domain.ResourceBag
}

func (e ResourceFlowedEvent) Apply(donburi.World, *domain.GameState) {}

func (e ResourceFlowedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e ResourceFlowedEvent) String() string {
	return fmt.Sprintf("ResourceFlowedEvent from=%s to=%s", e.FromNodeID, e.ToNodeID)
}

type RoadBuiltEvent struct {
	FromNode string
	ToNode   string
	Owner    string
	Cost     int
}

func (e RoadBuiltEvent) Apply(world donburi.World, state *domain.GameState) {
	fromEntry, okFrom := findNodeByID(world, state, e.FromNode)
	toEntry, okTo := findNodeByID(world, state, e.ToNode)
	if okFrom {
		n := ecs.NodeC.Get(fromEntry)
		n.HasRoad = true
	}
	if okTo {
		n := ecs.NodeC.Get(toEntry)
		n.HasRoad = true
	}
	if okFrom && okTo {
		fromPos := ecs.PositionC.Get(fromEntry)
		toPos := ecs.PositionC.Get(toEntry)
		x, y := fromPos.X, fromPos.Y
		for x != toPos.X {
			if toPos.X > x {
				x++
			} else {
				x--
			}
			markRoadAt(world, domain.Position{X: x, Y: y})
		}
		for y != toPos.Y {
			if toPos.Y > y {
				y++
			} else {
				y--
			}
			markRoadAt(world, domain.Position{X: x, Y: y})
		}
	}
	if playerState, ok := state.Players[e.Owner]; ok {
		playerState.Resources.AddAmount(domain.ResourceBuildPoints, -e.Cost)
	}
}

func (e RoadBuiltEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e RoadBuiltEvent) String() string {
	return fmt.Sprintf("RoadBuiltEvent %s->%s owner=%s cost=%d", e.FromNode, e.ToNode, e.Owner, e.Cost)
}

type UnitProducedEvent struct {
	NodeID   string
	UnitType string
	Faction  string
	Count    int
	Cost     domain.ResourceBag
}

func (e UnitProducedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok {
		return
	}
	if len(e.Cost) > 0 {
		if playerState, ok := state.Players[e.Faction]; ok {
			playerState.Resources = playerState.Resources.Sub(e.Cost)
		}
	}
	pos := ecs.PositionC.Get(nodeEntry)
	for i := 0; i < e.Count; i++ {
		ecs.CreateUnit(world, e.UnitType, e.Faction, domain.Position{X: pos.X, Y: pos.Y})
	}
}

func (e UnitProducedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e UnitProducedEvent) String() string {
	return fmt.Sprintf("UnitProducedEvent node=%s type=%s count=%d", e.NodeID, e.UnitType, e.Count)
}

type BuildPointsRechargedEvent struct {
	PlayerID string
	Amount   int
}

func (e BuildPointsRechargedEvent) Apply(_ donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	maxVal := staticdata.Default().Rules().BuildPointsMax
	next := playerState.Resources.Get(domain.ResourceBuildPoints) + e.Amount
	if next > maxVal {
		next = maxVal
	}
	playerState.Resources.Set(domain.ResourceBuildPoints, next)
}

func (e BuildPointsRechargedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e BuildPointsRechargedEvent) String() string {
	return fmt.Sprintf("BuildPointsRechargedEvent player=%s amount=%d", e.PlayerID, e.Amount)
}

type UpkeepPaidEvent struct {
	PlayerID      string
	FoodConsumed  int
}

func (e UpkeepPaidEvent) Apply(world donburi.World, state *domain.GameState) {
	playerState, ok := state.Players[e.PlayerID]
	if !ok {
		return
	}
	foodAfter := playerState.Resources.Get(domain.ResourceFood) - e.FoodConsumed
	if foodAfter < 0 {
		foodAfter = 0
	}
	playerState.Resources.Set(domain.ResourceFood, foodAfter)
	if playerState.Resources.Get(domain.ResourceFood) == 0 {
		markFactionStarving(world, e.PlayerID)
	}
}

func (e UpkeepPaidEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e UpkeepPaidEvent) String() string {
	return fmt.Sprintf("UpkeepPaidEvent player=%s food=%d", e.PlayerID, e.FoodConsumed)
}

type UnitStarvingEvent struct {
	UnitID         string
	DamagePerTurn  int
}

func (e UnitStarvingEvent) Apply(world donburi.World, state *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	if !entry.HasComponent(ecs.StarvingC) {
		entry.AddComponent(ecs.StarvingC)
		ecs.StarvingC.SetValue(entry, ecs.StarvingComp{TurnsStarving: 1})
	} else {
		s := ecs.StarvingC.Get(entry)
		s.TurnsStarving++
	}
	stats := ecs.UnitStatsC.Get(entry)
	stats.HP -= e.DamagePerTurn
	if stats.HP <= 0 {
		pos := ecs.PositionC.Get(entry)
		UnitDiedEvent{UnitID: e.UnitID, KillerID: "starvation", Pos: domain.Position{X: pos.X, Y: pos.Y}}.Apply(world, state)
	}
}

func (e UnitStarvingEvent) ClientPayload() *pb.CombatEvent {
	return UnitDamagedEvent{UnitID: e.UnitID, Damage: e.DamagePerTurn, HPAfter: 0, Source: "upkeep"}.ClientPayload()
}

func (e UnitStarvingEvent) String() string {
	return fmt.Sprintf("UnitStarvingEvent unit=%s damage=%d", e.UnitID, e.DamagePerTurn)
}

type BuildingDeactivatedEvent struct {
	NodeID string
	Reason string
}

func (e BuildingDeactivatedEvent) Apply(donburi.World, *domain.GameState) {}

func (e BuildingDeactivatedEvent) ClientPayload() *pb.CombatEvent { return nil }

func (e BuildingDeactivatedEvent) String() string {
	return fmt.Sprintf("BuildingDeactivatedEvent node=%s reason=%s", e.NodeID, e.Reason)
}

func markRoadAt(world donburi.World, pos domain.Position) {
	entry, ok := domain.GetNodeAt(world, pos)
	if !ok {
		return
	}
	n := ecs.NodeC.Get(entry)
	n.HasRoad = true
}

func markFactionStarving(world donburi.World, faction string) {
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction != faction {
			return
		}
		if !entry.HasComponent(ecs.StarvingC) {
			entry.AddComponent(ecs.StarvingC)
			ecs.StarvingC.SetValue(entry, ecs.StarvingComp{TurnsStarving: 1})
			return
		}
		s := ecs.StarvingC.Get(entry)
		s.TurnsStarving++
	})
}

func formatResourceBagData(bag domain.ResourceBag) map[string]string {
	out := make(map[string]string, len(bag))
	for _, key := range bag.Keys() {
		out[string(key)] = strconv.Itoa(bag.Get(key))
	}
	return out
}
