// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 验证单位结算引擎的单步解析逻辑。

package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/event"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestSingleStepResolver_BlockedByEnemyStartPositionEvenIfEnemyMovesAway(t *testing.T) {
	state := newCombatTestState(t, 4)
	attackerID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	enemyID := spawnTestUnit(state.World, "infantry", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		attackerID: {PlayerID: "player-a", UnitID: attackerID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
		enemyID:    {PlayerID: "player-b", UnitID: enemyID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N3_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, attackerID); got != (domain.Position{X: 0, Y: 0}) {
		t.Fatalf("attacker position = %#v, want stay at start", got)
	}
	if got := unitPosition(t, state.World, enemyID); got != (domain.Position{X: 3, Y: 0}) {
		t.Fatalf("enemy position = %#v, want move away", got)
	}
}

func TestSingleStepResolver_EdgeConflictStopsBothUnits(t *testing.T) {
	state := newCombatTestState(t, 2)
	leftID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	rightID := spawnTestUnit(state.World, "infantry", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		leftID:  {PlayerID: "player-a", UnitID: leftID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_0"},
		rightID: {PlayerID: "player-b", UnitID: rightID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N0_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countConflicts(events, "edge"); got != 1 {
		t.Fatalf("edge conflict count = %d, want 1", got)
	}
	if got := unitPosition(t, state.World, leftID); got != (domain.Position{X: 0, Y: 0}) {
		t.Fatalf("left position = %#v, want unchanged", got)
	}
	if got := unitPosition(t, state.World, rightID); got != (domain.Position{X: 1, Y: 0}) {
		t.Fatalf("right position = %#v, want unchanged", got)
	}
}

func TestSingleStepResolver_AttackMissesWhenTargetSuccessfullyMovesAway(t *testing.T) {
	state := newCombatTestState(t, 3)
	attackerID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	targetID := spawnTestUnit(state.World, "infantry", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		attackerID: {PlayerID: "player-a", UnitID: attackerID, Action: domain.UnitResolutionActionAttack, TargetUnitID: targetID},
		targetID:   {PlayerID: "player-b", UnitID: targetID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, targetID); got != (domain.Position{X: 2, Y: 0}) {
		t.Fatalf("target position = %#v, want moved away", got)
	}
	if got := countDamageEventsForUnit(events, targetID); got != 0 {
		t.Fatalf("target damage count = %d, want 0", got)
	}
}

func TestSingleStepResolver_AttackHitsAndRetaliatesWhenMoveBlocked(t *testing.T) {
	state := newCombatTestState(t, 2)
	attackerID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	targetID := spawnTestUnit(state.World, "infantry", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		attackerID: {PlayerID: "player-a", UnitID: attackerID, Action: domain.UnitResolutionActionAttack, TargetUnitID: targetID},
		targetID:   {PlayerID: "player-b", UnitID: targetID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N0_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countDamageEventsForUnit(events, targetID); got == 0 {
		t.Fatalf("target should take attack damage")
	}
	if got := countDamageEventsForUnit(events, attackerID); got == 0 {
		t.Fatalf("attacker should receive retaliation damage")
	}
}

func TestSingleStepResolver_ChargeStopsAtFirstContact(t *testing.T) {
	state := newCombatTestState(t, 4)
	cavalryID := spawnTestUnit(state.World, "cavalry", "player-a", 0, 0)
	blockerID := spawnTestUnit(state.World, "infantry", "player-b", 2, 0)
	rearID := spawnTestUnit(state.World, "archer", "player-b", 3, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		cavalryID: {PlayerID: "player-a", UnitID: cavalryID, Action: domain.UnitResolutionActionCharge, TargetNodeID: "N3_0", TargetUnitID: rearID},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, cavalryID); got != (domain.Position{X: 1, Y: 0}) {
		t.Fatalf("cavalry position = %#v, want stop before first contact", got)
	}
	if got := countDamageEventsForUnit(events, blockerID); got == 0 {
		t.Fatalf("blocker should receive charge damage")
	}
	if got := countDamageEventsForUnit(events, rearID); got != 0 {
		t.Fatalf("rear target damage count = %d, want 0", got)
	}
}

func TestSingleStepResolver_SettlerIsRemovedWhenCaughtByMelee(t *testing.T) {
	state := newCombatTestState(t, 2)
	warriorID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	settlerID := spawnTestUnit(state.World, "settler", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		warriorID: {PlayerID: "player-a", UnitID: warriorID, Action: domain.UnitResolutionActionAttack, TargetUnitID: settlerID},
		settlerID: {PlayerID: "player-b", UnitID: settlerID, Action: domain.UnitResolutionActionHold},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if _, ok := findUnitEntry(state.World, settlerID); ok {
		t.Fatalf("settler should be removed after melee contact")
	}
}

func newCombatTestState(t *testing.T, width int) *domain.GameState {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			CityCoreMaxHP:             100,
			BaseIndustryOutputPerTurn: 10,
			BaseResearchOutputPerTurn: 1,
			TokensPerTurn:             3,
		},
		Units: []staticdata.UnitDefinition{
			{ID: "settler", Class: "civilian", MaxHP: 12, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 2, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Tags: []string{"civilian"}},
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"melee"}},
			{ID: "archer", Class: "ranged", MaxHP: 20, Attack: 8, AttackRange: 2, MoveRange: 2, VisionRange: 4, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"ranged"}},
			{ID: "cavalry", Class: "mobile", MaxHP: 25, Attack: 12, AttackRange: 1, MoveRange: 3, VisionRange: 4, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 2}, Multipliers: map[string]float64{}, ChargeBonus: 1.5, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"charge"}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", MoveCostNoRoad: 2, Passable: true},
			{ID: "forest", MoveCostNoRoad: 3, Passable: true},
			{ID: "mountain", MoveCostNoRoad: 3, Passable: true, BlocksCavalry: true},
			{ID: "river", MoveCostNoRoad: 99, Passable: false, PassableWithRoad: true, BlocksCavalry: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "combat-test",
		Width:        width,
		Height:       1,
		SpawnPoints:  map[int]domain.Position{0: {X: 0, Y: 0}, 1: {X: width - 1, Y: 0}},
		PlayerSpawns: map[string]domain.Position{"player-a": {X: 0, Y: 0}, "player-b": {X: width - 1, Y: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for x := 0; x < width; x++ {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID(x, 0), X: x, Y: 0, Terrain: "plain"})
		mapData.NodeIndex[nodeID(x, 0)] = entity
	}

	state := domain.NewGameState("combat-test", []string{"player-a", "player-b"}, []string{"A", "B"}, mapData)
	state.World = world
	return state
}

func spawnTestUnit(world donburi.World, unitType string, faction string, x, y int) string {
	entry := world.Entry(ecs.CreateUnit(world, unitType, faction, domain.Position{X: x, Y: y}))
	return ecs.UnitStatsC.Get(entry).ID
}

func applyCombatEvents(state *domain.GameState, events []event.Event) {
	for _, evt := range events {
		evt.Apply(state.World, state)
	}
}

func unitPosition(t *testing.T, world donburi.World, unitID string) domain.Position {
	t.Helper()
	entry, ok := findUnitEntry(world, unitID)
	if !ok {
		t.Fatalf("unit %s not found", unitID)
	}
	pos := ecs.PositionC.Get(entry)
	return domain.Position{X: pos.X, Y: pos.Y}
}

func findUnitEntry(world donburi.World, unitID string) (*donburi.Entry, bool) {
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

func countDamageEventsForUnit(events []event.Event, unitID string) int {
	count := 0
	for _, evt := range events {
		if dmg, ok := evt.(event.UnitDamagedEvent); ok && dmg.UnitID == unitID {
			count++
		}
	}
	return count
}

func countConflicts(events []event.Event, conflictType string) int {
	count := 0
	for _, evt := range events {
		if conflict, ok := evt.(event.ConflictResolvedEvent); ok && conflict.ConflictType == conflictType {
			count++
		}
	}
	return count
}

func nodeID(x, y int) string {
	return "N" + string(rune('0'+x)) + "_" + string(rune('0'+y))
}
