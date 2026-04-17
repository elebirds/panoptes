package debug

import "testing"

func TestRealContentCatalogSupportsExpandedMVPContent(t *testing.T) {
	catalog := loadRealContentCatalog(t)

	defaultMap, ok := catalog.GetMap("default")
	if !ok {
		t.Fatalf("default map missing")
	}
	if defaultMap.Width != 24 || defaultMap.Height != 24 {
		t.Fatalf("default map size = %dx%d, want 24x24", defaultMap.Width, defaultMap.Height)
	}
	resourcePoints := 0
	for _, node := range defaultMap.Nodes {
		if node.IsResourcePoint {
			resourcePoints++
		}
	}
	if resourcePoints != 12 {
		t.Fatalf("default map resource points = %d, want 12", resourcePoints)
	}

	duelLarge, ok := catalog.GetMap("duel_large")
	if !ok {
		t.Fatalf("duel_large map missing")
	}
	if duelLarge.Width != 36 || duelLarge.Height != 36 {
		t.Fatalf("duel_large size = %dx%d, want 36x36", duelLarge.Width, duelLarge.Height)
	}

	workshop, ok := catalog.GetBuilding("workshop")
	if !ok {
		t.Fatalf("workshop missing")
	}
	if workshop.DefaultRecipeID != "" || len(workshop.ModifierEffects) != 1 || workshop.ModifierEffects[0].PointKey != "industry_output" {
		t.Fatalf("workshop = %#v, want industry modifier and no default recipe", workshop)
	}

	archery, ok := catalog.GetBuilding("archery")
	if !ok {
		t.Fatalf("archery missing")
	}
	if archery.DefaultRecipeID != "archery_archer" {
		t.Fatalf("archery default recipe = %q, want archery_archer", archery.DefaultRecipeID)
	}

	archer, ok := catalog.GetUnit("archer")
	if !ok {
		t.Fatalf("archer missing")
	}
	if archer.Class != "ranged" || archer.AttackRange != 2 || archer.Flags.CanAttackStructures {
		t.Fatalf("archer = %#v, want ranged non-structure attacker", archer)
	}

	fortifications, ok := catalog.GetTechnology("fortifications")
	if !ok {
		t.Fatalf("fortifications missing")
	}
	if len(fortifications.Prerequisites) != 1 || fortifications.Prerequisites[0].TargetID != "militia_mobilization" {
		t.Fatalf("fortifications prerequisites = %#v, want militia_mobilization", fortifications.Prerequisites)
	}

	reorganization, ok := catalog.GetPolicy("reorganization")
	if !ok {
		t.Fatalf("reorganization missing")
	}
	if len(reorganization.ModifierEffects) != 1 || reorganization.ModifierEffects[0].PointKey != "industry_output" {
		t.Fatalf("reorganization = %#v, want industry output modifier", reorganization)
	}
}
