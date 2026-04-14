package combat

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func TestSnapshotPhaseAppliesUnitAttackModifier(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "weapon_mastery", Branch: "military", Tier: 1, TechPointCost: 1,
				Effects: []staticdata.TechnologyEffect{
					{Type: "modifier", Trigger: "unit.attack", TargetID: "warrior", ModifierType: "flat", Value: 3},
				},
			},
		},
	}))

	world := donburi.NewWorld()
	entity := ecs.CreateUnit(world, "warrior", "player-1", domain.Position{X: 0, Y: 0})
	entry := world.Entry(entity)
	unitID := ecs.UnitStatsC.Get(entry).ID

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.World = world
	state.Players["player-1"].Research.UnlockTechnology("weapon_mastery")
	ctx := &ResolutionContext{
		World:     world,
		State:     state,
		CurrentHP: make(map[string]int),
	}

	(SnapshotPhase{}).Apply(ctx)

	if got := ctx.Snapshot.Units[unitID].Attack; got != 13 {
		t.Fatalf("snapshot attack = %d, want 13", got)
	}
}
