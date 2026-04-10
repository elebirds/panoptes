package event

import (
	"fmt"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/yohamta/donburi"
)

type UnitMovedEvent struct {
	UnitID     string
	From       domain.Position
	To         domain.Position
	Timestamp  int
}

func (e UnitMovedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	pos := ecs.PositionC.Get(entry)
	pos.X = e.To.X
	pos.Y = e.To.Y
}

func (e UnitMovedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "unit_move",
		Data: &pb.CombatEvent_UnitMove{UnitMove: &pb.UnitMoveEvent{
			UnitId:    e.UnitID,
			From:      toProtoPosition(e.From),
			To:        toProtoPosition(e.To),
			Timestamp: int32(e.Timestamp),
		}},
	}
}

func (e UnitMovedEvent) String() string {
	return fmt.Sprintf("UnitMovedEvent unit=%s from=(%d,%d) to=(%d,%d)", e.UnitID, e.From.X, e.From.Y, e.To.X, e.To.Y)
}

type UnitDamagedEvent struct {
	UnitID   string
	Damage   int
	HPAfter  int
	Source   string
}

func (e UnitDamagedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	stats := ecs.UnitStatsC.Get(entry)
	stats.HP = e.HPAfter
}

func (e UnitDamagedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "unit_damaged",
		Data: &pb.CombatEvent_UnitDamaged{UnitDamaged: &pb.UnitDamagedEvent{
			UnitId:  e.UnitID,
			Damage:  int32(e.Damage),
			HpAfter: int32(e.HPAfter),
			Source:  e.Source,
		}},
	}
}

func (e UnitDamagedEvent) String() string {
	return fmt.Sprintf("UnitDamagedEvent unit=%s dmg=%d hp_after=%d source=%s", e.UnitID, e.Damage, e.HPAfter, e.Source)
}

type UnitDiedEvent struct {
	UnitID    string
	KillerID  string
	Pos       domain.Position
}

func (e UnitDiedEvent) Apply(world donburi.World, _ *domain.GameState) {
	entry, ok := findUnitByID(world, e.UnitID)
	if !ok {
		return
	}
	world.Remove(entry.Entity())
}

func (e UnitDiedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "unit_died",
		Data: &pb.CombatEvent_UnitDied{UnitDied: &pb.UnitDiedEvent{
			UnitId:   e.UnitID,
			KillerId: e.KillerID,
			Pos:      toProtoPosition(e.Pos),
		}},
	}
}

func (e UnitDiedEvent) String() string {
	return fmt.Sprintf("UnitDiedEvent unit=%s killer=%s", e.UnitID, e.KillerID)
}

type CastleDamagedEvent struct {
	NodeID      string
	Damage      int
	HPAfter     int
	AttackerID  string
}

func (e CastleDamagedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	building.HP = e.HPAfter
	if ownerState, ok := state.Players[building.Owner]; ok {
		ownerState.MainCastleHP = e.HPAfter
	}
}

func (e CastleDamagedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "castle_damaged",
		Data: &pb.CombatEvent_CastleDamaged{CastleDamaged: &pb.CastleDamagedEvent{
			NodeId:     e.NodeID,
			Damage:     int32(e.Damage),
			HpAfter:    int32(e.HPAfter),
			AttackerId: e.AttackerID,
		}},
	}
}

func (e CastleDamagedEvent) String() string {
	return fmt.Sprintf("CastleDamagedEvent node=%s dmg=%d hp_after=%d", e.NodeID, e.Damage, e.HPAfter)
}

type CastleDestroyedEvent struct {
	NodeID            string
	ConquerorFaction  string
}

func (e CastleDestroyedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if ok {
		node := ecs.NodeC.Get(nodeEntry)
		node.Owner = e.ConquerorFaction
		if nodeEntry.HasComponent(ecs.BuildingC) {
			building := ecs.BuildingC.Get(nodeEntry)
			building.Owner = e.ConquerorFaction
		}
	}
	state.IsOver = true
	state.WinnerID = e.ConquerorFaction
	state.OverReason = "castle_destroyed"
}

func (e CastleDestroyedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "castle_destroyed",
		Data: &pb.CombatEvent_CastleDestroyed{CastleDestroyed: &pb.CastleDestroyedEvent{
			NodeId:           e.NodeID,
			ConquerorFaction: e.ConquerorFaction,
		}},
	}
}

func (e CastleDestroyedEvent) String() string {
	return fmt.Sprintf("CastleDestroyedEvent node=%s conqueror=%s", e.NodeID, e.ConquerorFaction)
}

type RoadDestroyedEvent struct {
	FromNode    string
	ToNode      string
	DestroyerID string
}

func (e RoadDestroyedEvent) Apply(world donburi.World, state *domain.GameState) {
	if fromEntry, ok := findNodeByID(world, state, e.FromNode); ok {
		n := ecs.NodeC.Get(fromEntry)
		n.HasRoad = false
	}
	if toEntry, ok := findNodeByID(world, state, e.ToNode); ok {
		n := ecs.NodeC.Get(toEntry)
		n.HasRoad = false
	}
}

func (e RoadDestroyedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "road_destroyed",
		Data: &pb.CombatEvent_RoadDestroyed{RoadDestroyed: &pb.RoadDestroyedEvent{
			FromNode:    e.FromNode,
			ToNode:      e.ToNode,
			DestroyerId: e.DestroyerID,
		}},
	}
}

func (e RoadDestroyedEvent) String() string {
	return fmt.Sprintf("RoadDestroyedEvent %s->%s by=%s", e.FromNode, e.ToNode, e.DestroyerID)
}

type BuildingDamagedEvent struct {
	NodeID   string
	Damage   int
	HPAfter  int
}

func (e BuildingDamagedEvent) Apply(world donburi.World, state *domain.GameState) {
	nodeEntry, ok := findNodeByID(world, state, e.NodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return
	}
	building := ecs.BuildingC.Get(nodeEntry)
	building.HP = e.HPAfter
}

func (e BuildingDamagedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "building_damaged",
		Data: &pb.CombatEvent_BuildingDamaged{BuildingDamaged: &pb.BuildingDamagedEvent{
			NodeId:  e.NodeID,
			Damage:  int32(e.Damage),
			HpAfter: int32(e.HPAfter),
		}},
	}
}

func (e BuildingDamagedEvent) String() string {
	return fmt.Sprintf("BuildingDamagedEvent node=%s dmg=%d hp_after=%d", e.NodeID, e.Damage, e.HPAfter)
}

type ConflictResolvedEvent struct {
	UnitAID      string
	UnitBID      string
	Location     domain.Position
	ConflictType string
}

func (e ConflictResolvedEvent) Apply(donburi.World, *domain.GameState) {}

func (e ConflictResolvedEvent) ClientPayload() *pb.CombatEvent {
	return &pb.CombatEvent{
		Type: "conflict",
		Data: &pb.CombatEvent_Conflict{Conflict: &pb.ConflictEvent{
			UnitAId:      e.UnitAID,
			UnitBId:      e.UnitBID,
			Location:     toProtoPosition(e.Location),
			ConflictType: e.ConflictType,
		}},
	}
}

func (e ConflictResolvedEvent) String() string {
	return fmt.Sprintf("ConflictResolvedEvent a=%s b=%s type=%s", e.UnitAID, e.UnitBID, e.ConflictType)
}

func toProtoPosition(pos domain.Position) *pb.Position {
	return &pb.Position{X: int32(pos.X), Y: int32(pos.Y)}
}

func findUnitByID(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func findNodeByID(world donburi.World, state *domain.GameState, nodeID string) (*donburi.Entry, bool) {
	if state != nil {
		if entry, ok := state.GetNode(nodeID); ok {
			return entry, true
		}
	}
	return ecs.FindNodeByID(world, nodeID)
}
