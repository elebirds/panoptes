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

	if got := unitPosition(t, state.World, attackerID); got != (domain.Position{Q: 0, R: 0}) {
		t.Fatalf("attacker position = %#v, want stay at start", got)
	}
	if got := unitPosition(t, state.World, enemyID); got != (domain.Position{Q: 3, R: 0}) {
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
	if got := unitPosition(t, state.World, leftID); got != (domain.Position{Q: 0, R: 0}) {
		t.Fatalf("left position = %#v, want unchanged", got)
	}
	if got := unitPosition(t, state.World, rightID); got != (domain.Position{Q: 1, R: 0}) {
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

	if got := unitPosition(t, state.World, targetID); got != (domain.Position{Q: 2, R: 0}) {
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

	if got := unitPosition(t, state.World, cavalryID); got != (domain.Position{Q: 1, R: 0}) {
		t.Fatalf("cavalry position = %#v, want stop before first contact", got)
	}
	if got := countDamageEventsForUnit(events, blockerID); got == 0 {
		t.Fatalf("blocker should receive charge damage")
	}
	if got := countDamageEventsForUnit(events, rearID); got != 0 {
		t.Fatalf("rear target damage count = %d, want 0", got)
	}
}

func TestSingleStepResolver_ChargeStillTargetsUnitOnBuildingNode(t *testing.T) {
	state := newCombatTestState(t, 4)
	cavalryID := spawnTestUnit(state.World, "cavalry", "player-a", 0, 0)
	blockerID := spawnTestUnit(state.World, "infantry", "player-b", 2, 0)
	rearID := spawnTestUnit(state.World, "archer", "player-b", 3, 0)
	spawnTestBuilding(state.World, state, "N2_0", "farm", "player-b", "city-b")

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		cavalryID: {PlayerID: "player-a", UnitID: cavalryID, Action: domain.UnitResolutionActionCharge, TargetNodeID: "N3_0", TargetUnitID: rearID},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, cavalryID); got != (domain.Position{Q: 1, R: 0}) {
		t.Fatalf("cavalry position = %#v, want stop before first contact", got)
	}
	if got := countDamageEventsForUnit(events, blockerID); got == 0 {
		t.Fatalf("blocker should still receive charge damage on building node")
	}
	if got := countDamageEventsForUnit(events, rearID); got != 0 {
		t.Fatalf("rear target damage count = %d, want 0", got)
	}
}

func TestSingleStepResolver_NodeConflictGroupResolvesThreeHostileFactions(t *testing.T) {
	state := newCombatTestStateWithPlayers(t, 3, 3, []string{"player-a", "player-b", "player-c"})
	aID := spawnTestUnit(state.World, "infantry", "player-a", 0, 1)
	bID := spawnTestUnit(state.World, "infantry", "player-b", 2, 1)
	cID := spawnTestUnit(state.World, "infantry", "player-c", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		aID: {PlayerID: "player-a", UnitID: aID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		bID: {PlayerID: "player-b", UnitID: bID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		cID: {PlayerID: "player-c", UnitID: cID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, aID); got != (domain.Position{Q: 0, R: 1}) {
		t.Fatalf("player-a position = %#v, want fallback to start", got)
	}
	if got := unitPosition(t, state.World, bID); got != (domain.Position{Q: 2, R: 1}) {
		t.Fatalf("player-b position = %#v, want fallback to start", got)
	}
	if got := unitPosition(t, state.World, cID); got != (domain.Position{Q: 1, R: 0}) {
		t.Fatalf("player-c position = %#v, want fallback to start", got)
	}
	if got := countConflicts(events, "node"); got != 3 {
		t.Fatalf("node conflict count = %d, want 3 hostile pairs", got)
	}
	if got := countDamageEventsForUnit(events, aID); got != 2 {
		t.Fatalf("player-a damage events = %d, want 2", got)
	}
	if got := countDamageEventsForUnit(events, bID); got != 2 {
		t.Fatalf("player-b damage events = %d, want 2", got)
	}
	if got := countDamageEventsForUnit(events, cID); got != 2 {
		t.Fatalf("player-c damage events = %d, want 2", got)
	}
}

func TestSingleStepResolver_NodeConflictGroupSkipsFriendlyPairs(t *testing.T) {
	state := newCombatTestStateWithPlayers(t, 3, 3, []string{"player-a", "player-b"})
	a1ID := spawnTestUnit(state.World, "infantry", "player-a", 0, 1)
	a2ID := spawnTestUnit(state.World, "infantry", "player-a", 1, 0)
	bID := spawnTestUnit(state.World, "infantry", "player-b", 2, 1)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		a1ID: {PlayerID: "player-a", UnitID: a1ID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		a2ID: {PlayerID: "player-a", UnitID: a2ID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		bID:  {PlayerID: "player-b", UnitID: bID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countConflicts(events, "node"); got != 2 {
		t.Fatalf("node conflict count = %d, want 2 hostile pairs", got)
	}
	if got := countDamageEventsForUnit(events, a1ID); got != 1 {
		t.Fatalf("player-a unit1 damage events = %d, want 1", got)
	}
	if got := countDamageEventsForUnit(events, a2ID); got != 1 {
		t.Fatalf("player-a unit2 damage events = %d, want 1", got)
	}
	if got := countDamageEventsForUnit(events, bID); got != 2 {
		t.Fatalf("player-b damage events = %d, want 2", got)
	}
}

func TestSingleStepResolver_FriendlySameDestinationFallsBackWithoutDamage(t *testing.T) {
	state := newCombatTestStateWithPlayers(t, 3, 3, []string{"player-a"})
	a1ID := spawnTestUnit(state.World, "infantry", "player-a", 0, 1)
	a2ID := spawnTestUnit(state.World, "infantry", "player-a", 2, 1)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		a1ID: {PlayerID: "player-a", UnitID: a1ID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		a2ID: {PlayerID: "player-a", UnitID: a2ID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countConflicts(events, "node"); got != 0 {
		t.Fatalf("friendly-only node collision emitted combat conflicts = %d, want 0", got)
	}
	if got := countDamageEventsForUnit(events, a1ID) + countDamageEventsForUnit(events, a2ID); got != 0 {
		t.Fatalf("friendly-only node collision damage events = %d, want 0", got)
	}
	if got := unitPosition(t, state.World, a1ID); got != (domain.Position{Q: 0, R: 1}) {
		t.Fatalf("unit a1 position = %#v, want fallback to start", got)
	}
	if got := unitPosition(t, state.World, a2ID); got != (domain.Position{Q: 2, R: 1}) {
		t.Fatalf("unit a2 position = %#v, want fallback to start", got)
	}
	assertNoUnitStacks(t, state.World)
}

func TestSingleStepResolver_FriendlyStartPositionBlocksMovement(t *testing.T) {
	state := newCombatTestState(t, 3)
	moverID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	blockerID := spawnTestUnit(state.World, "infantry", "player-a", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		moverID:   {PlayerID: "player-a", UnitID: moverID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
		blockerID: {PlayerID: "player-a", UnitID: blockerID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, moverID); got != (domain.Position{Q: 0, R: 0}) {
		t.Fatalf("mover position = %#v, want blocked by friendly start position", got)
	}
	if got := unitPosition(t, state.World, blockerID); got != (domain.Position{Q: 2, R: 0}) {
		t.Fatalf("blocker position = %#v, want move to target", got)
	}
	assertNoUnitStacks(t, state.World)
}

func TestSingleStepResolver_EngineerBuildsRoadTrailOnActualPath(t *testing.T) {
	state := newCombatTestState(t, 3)
	engineerID := spawnTestUnit(state.World, "engineer", "player-a", 0, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		engineerID: {PlayerID: "player-a", UnitID: engineerID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countEventKind(events, "engineer_road_trail_built"); got != 1 {
		t.Fatalf("engineer road trail events = %d, want 1", got)
	}
	for _, nodeID := range []string{"N0_0", "N1_0", "N2_0"} {
		if !nodeHasRoad(t, state, nodeID) {
			t.Fatalf("node %s HasRoad = false, want true", nodeID)
		}
	}
}

func TestSingleStepResolver_NonEngineerDoesNotBuildRoadTrail(t *testing.T) {
	state := newCombatTestState(t, 3)
	infantryID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		infantryID: {PlayerID: "player-a", UnitID: infantryID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countEventKind(events, "engineer_road_trail_built"); got != 0 {
		t.Fatalf("engineer road trail events = %d, want 0", got)
	}
	for _, nodeID := range []string{"N0_0", "N1_0", "N2_0"} {
		if nodeHasRoad(t, state, nodeID) {
			t.Fatalf("node %s HasRoad = true, want false", nodeID)
		}
	}
}

func TestSingleStepResolver_EngineerRoadTrailStopsAtBlockedActualPosition(t *testing.T) {
	state := newCombatTestState(t, 3)
	engineerID := spawnTestUnit(state.World, "engineer", "player-a", 0, 0)
	spawnTestUnit(state.World, "infantry", "player-b", 2, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		engineerID: {PlayerID: "player-a", UnitID: engineerID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N2_0"},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := unitPosition(t, state.World, engineerID); got != (domain.Position{Q: 1, R: 0}) {
		t.Fatalf("engineer position = %#v, want stop before blocker", got)
	}
	for _, nodeID := range []string{"N0_0", "N1_0"} {
		if !nodeHasRoad(t, state, nodeID) {
			t.Fatalf("node %s HasRoad = false, want true", nodeID)
		}
	}
	if nodeHasRoad(t, state, "N2_0") {
		t.Fatalf("node N2_0 HasRoad = true, want false")
	}
}

func TestSingleStepResolver_NodeConflictGroupEmitsStableConflictOrder(t *testing.T) {
	buildState := func() *domain.GameState {
		state := newCombatTestStateWithPlayers(t, 3, 3, []string{"player-a", "player-b", "player-c"})
		aID := spawnTestUnitWithID(state.World, "infantry", "player-a", 0, 1, "unit-a")
		bID := spawnTestUnitWithID(state.World, "infantry", "player-b", 2, 1, "unit-b")
		cID := spawnTestUnitWithID(state.World, "infantry", "player-c", 1, 0, "unit-c")
		state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
			aID: {PlayerID: "player-a", UnitID: aID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
			bID: {PlayerID: "player-b", UnitID: bID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
			cID: {PlayerID: "player-c", UnitID: cID, Action: domain.UnitResolutionActionMove, TargetNodeID: "N1_1"},
		}
		return state
	}

	resolver := NewSingleStepResolver()
	firstState := buildState()
	secondState := buildState()
	first := collectConflictPairs(resolver.Run(firstState.World, firstState))
	second := collectConflictPairs(resolver.Run(secondState.World, secondState))

	want := []string{"unit-a|unit-b|node", "unit-a|unit-c|node", "unit-b|unit-c|node"}
	if len(first) != len(want) {
		t.Fatalf("first conflict pair count = %d, want %d", len(first), len(want))
	}
	for idx := range want {
		if first[idx] != want[idx] {
			t.Fatalf("first conflict[%d] = %q, want %q", idx, first[idx], want[idx])
		}
		if second[idx] != want[idx] {
			t.Fatalf("second conflict[%d] = %q, want %q", idx, second[idx], want[idx])
		}
	}
}

func TestSingleStepResolver_SettlerIsRemovedWhenCaughtByMelee(t *testing.T) {
	state := newCombatTestState(t, 2)
	infantryID := spawnTestUnit(state.World, "infantry", "player-a", 0, 0)
	settlerID := spawnTestUnit(state.World, "settler", "player-b", 1, 0)

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		infantryID: {PlayerID: "player-a", UnitID: infantryID, Action: domain.UnitResolutionActionAttack, TargetUnitID: settlerID},
		settlerID:  {PlayerID: "player-b", UnitID: settlerID, Action: domain.UnitResolutionActionHold},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if _, ok := findUnitEntry(state.World, settlerID); ok {
		t.Fatalf("settler should be removed after melee contact")
	}
}

func TestSingleStepResolver_AttackDamagesHostileBuildingByNodeTarget(t *testing.T) {
	state := newCombatTestState(t, 3)
	attackerID := spawnTestUnitWithID(state.World, "infantry", "player-a", 0, 0, "attacker-1")
	spawnTestBuilding(state.World, state, "N1_0", "farm", "player-b", "city-b")

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		attackerID: {
			PlayerID:     "player-a",
			UnitID:       attackerID,
			Action:       domain.UnitResolutionActionAttack,
			TargetNodeID: "N1_0",
		},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countBuildingDamageEvents(events, "N1_0"); got != 1 {
		t.Fatalf("building damage events = %d, want 1", got)
	}
	nodeEntry, ok := state.GetNode("N1_0")
	if !ok {
		t.Fatalf("missing node N1_0")
	}
	if got := ecs.BuildingC.Get(nodeEntry).HP; got >= 20 {
		t.Fatalf("building HP = %d, want reduced below 20", got)
	}
}

func TestSingleStepResolver_AttackDestroysCapitalCityCoreByNodeTarget(t *testing.T) {
	state := newCombatTestState(t, 2)
	attackerID := spawnTestUnitWithID(state.World, "infantry", "player-a", 1, 0, "attacker-1")
	spawnTestBuilding(state.World, state, "N0_0", "city_core", "player-b", "N0_0")
	state.EnsureCityState("player-b", "N0_0")
	state.Players["player-b"].CapitalCityID = "N0_0"
	state.Players["player-b"].CapitalCityCoreHP = 10

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		attackerID: {
			PlayerID:     "player-a",
			UnitID:       attackerID,
			Action:       domain.UnitResolutionActionAttack,
			TargetNodeID: "N0_0",
		},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countEventKind(events, "city_core_destroyed"); got != 1 {
		t.Fatalf("city_core_destroyed events = %d, want 1", got)
	}
	if !state.IsOver {
		t.Fatalf("state.IsOver = false, want true")
	}
	if state.WinnerID != "player-a" {
		t.Fatalf("winner_id = %q, want player-a", state.WinnerID)
	}
}

func TestSingleStepResolver_DeadUnitCannotAttackStructureLaterThisTurn(t *testing.T) {
	state := newCombatTestState(t, 3)
	killerID := spawnTestUnitWithID(state.World, "infantry", "player-b", 0, 0, "a-killer")
	attackerID := spawnTestUnitWithID(state.World, "infantry", "player-a", 1, 0, "z-attacker")
	spawnTestBuilding(state.World, state, "N2_0", "farm", "player-b", "city-b")

	attackerEntry, ok := findUnitEntry(state.World, attackerID)
	if !ok {
		t.Fatalf("attacker not found")
	}
	ecs.UnitStatsC.Get(attackerEntry).HP = 1

	state.TurnRuntime.Resolving.UnitOrders = map[string]domain.UnitResolutionOrder{
		killerID: {
			PlayerID:     "player-b",
			UnitID:       killerID,
			Action:       domain.UnitResolutionActionAttack,
			TargetUnitID: attackerID,
		},
		attackerID: {
			PlayerID:     "player-a",
			UnitID:       attackerID,
			Action:       domain.UnitResolutionActionAttack,
			TargetNodeID: "N2_0",
		},
	}

	resolver := NewSingleStepResolver()
	events := resolver.Run(state.World, state)
	applyCombatEvents(state, events)

	if got := countBuildingDamageEvents(events, "N2_0"); got != 0 {
		t.Fatalf("building damage events = %d, want 0 after attacker dies first", got)
	}
	nodeEntry, ok := state.GetNode("N2_0")
	if !ok {
		t.Fatalf("missing node N2_0")
	}
	if got := ecs.BuildingC.Get(nodeEntry).HP; got != 20 {
		t.Fatalf("building HP = %d, want unchanged 20", got)
	}
}

func newCombatTestState(t *testing.T, width int) *domain.GameState {
	return newCombatTestStateWithPlayers(t, width, 1, []string{"player-a", "player-b"})
}

func newCombatTestStateWithPlayers(t *testing.T, width, height int, playerIDs []string) *domain.GameState {
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
			{ID: "engineer", Class: "civilian", MaxHP: 16, Attack: 0, AttackRange: 0, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Tags: []string{"civilian", "engineer"}},
			{ID: "infantry", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true, CanAttackStructures: true}, Tags: []string{"melee"}},
			{ID: "archer", Class: "ranged", MaxHP: 20, Attack: 8, AttackRange: 2, MoveRange: 2, VisionRange: 4, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"ranged"}},
			{ID: "cavalry", Class: "mobile", MaxHP: 25, Attack: 12, AttackRange: 1, MoveRange: 3, VisionRange: 4, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 2}, Multipliers: map[string]float64{}, ChargeBonus: 1.5, Flags: staticdata.UnitFlags{CanCapture: true}, Tags: []string{"charge"}},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 10, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 20, TakeoverMode: "city_capture"},
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
		Height:       height,
		SpawnPoints:  map[int]domain.Position{},
		PlayerSpawns: map[string]domain.Position{},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for idx, playerID := range playerIDs {
		mapData.SpawnPoints[idx] = domain.Position{Q: idx % width, R: idx / width}
		mapData.PlayerSpawns[playerID] = mapData.SpawnPoints[idx]
	}
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID(x, y), Q: x, R: y, Terrain: "plain"})
			mapData.NodeIndex[nodeID(x, y)] = entity
		}
	}

	usernames := make([]string, len(playerIDs))
	for idx, playerID := range playerIDs {
		usernames[idx] = playerID
	}
	state := domain.NewGameState("combat-test", playerIDs, usernames, mapData)
	state.World = world
	return state
}

func spawnTestUnit(world donburi.World, unitType string, faction string, x, y int) string {
	entry := world.Entry(ecs.CreateUnit(world, unitType, faction, domain.Position{Q: x, R: y}))
	return ecs.UnitStatsC.Get(entry).ID
}

func spawnTestUnitWithID(world donburi.World, unitType string, faction string, x, y int, unitID string) string {
	entry := world.Entry(ecs.CreateUnit(world, unitType, faction, domain.Position{Q: x, R: y}))
	ecs.UnitStatsC.Get(entry).ID = unitID
	return unitID
}

func spawnTestBuilding(world donburi.World, state *domain.GameState, nodeID string, buildingType string, owner string, cityID string) {
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok {
		panic("missing test node: " + nodeID)
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = owner
	node.TerritoryOwner = owner
	ecs.CreateBuilding(world, buildingType, owner, cityID, nodeEntry)
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
	return domain.Position{Q: pos.Q, R: pos.R}
}

func nodeHasRoad(t *testing.T, state *domain.GameState, nodeID string) bool {
	t.Helper()
	entry, ok := state.GetNode(nodeID)
	if !ok {
		t.Fatalf("missing node %s", nodeID)
	}
	return ecs.NodeC.Get(entry).HasRoad
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

func countBuildingDamageEvents(events []event.Event, nodeID string) int {
	count := 0
	for _, evt := range events {
		if dmg, ok := evt.(event.BuildingDamagedEvent); ok && dmg.NodeID == nodeID {
			count++
		}
	}
	return count
}

func countEventKind(events []event.Event, kind string) int {
	count := 0
	for _, evt := range events {
		if evt != nil && evt.Kind() == kind {
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

func collectConflictPairs(events []event.Event) []string {
	pairs := make([]string, 0)
	for _, evt := range events {
		conflict, ok := evt.(event.ConflictResolvedEvent)
		if !ok {
			continue
		}
		pairs = append(pairs, conflict.UnitAID+"|"+conflict.UnitBID+"|"+conflict.ConflictType)
	}
	return pairs
}

func assertNoUnitStacks(t *testing.T, world donburi.World) {
	t.Helper()
	if violations := domain.UnitOccupancyViolations(world); len(violations) != 0 {
		t.Fatalf("unit occupancy violations = %d, want 0", len(violations))
	}
}

func nodeID(x, y int) string {
	return "N" + string(rune('0'+x)) + "_" + string(rune('0'+y))
}
