package event

import (
	"go/ast"
	"go/parser"
	"go/token"
	"path/filepath"
	"reflect"
	"runtime"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

type auditCategory string

const (
	auditAuthoritativeState auditCategory = "authoritative_state"
	auditReportOnly         auditCategory = "report_only"
	auditInternalStateOnly  auditCategory = "internal_state_only"
)

type eventAuditCase struct {
	name     string
	kind     string
	category auditCategory
	ev       Event
}

func currentEventAuditContract() []eventAuditCase {
	return []eventAuditCase{
		{name: "turn started", kind: "turn_started", category: auditAuthoritativeState, ev: TurnStartedEvent{}},
		{name: "phase changed", kind: "phase_changed", category: auditAuthoritativeState, ev: PhaseChangedEvent{}},
		{name: "game over", kind: "game_over", category: auditAuthoritativeState, ev: GameOverEvent{}},
		{name: "player reconnected", kind: "player_reconnected", category: auditReportOnly, ev: PlayerReconnectedEvent{}},

		{name: "minister acted", kind: "minister_acted", category: auditReportOnly, ev: MinisterActedEvent{}},
		{name: "policy changed", kind: "national_policy_changed", category: auditAuthoritativeState, ev: PolicyChangedEvent{}},
		{name: "token used", kind: "token_used", category: auditAuthoritativeState, ev: TokenUsedEvent{}},
		{name: "institution loadout changed", kind: "institution_loadout_changed", category: auditAuthoritativeState, ev: InstitutionLoadoutChangedEvent{}},
		{name: "institution loadout activated", kind: "institution_loadout_activated", category: auditAuthoritativeState, ev: InstitutionLoadoutActivatedEvent{}},

		{name: "research target changed", kind: "research_target_changed", category: auditAuthoritativeState, ev: ResearchTargetChangedEvent{}},
		{name: "technology completed", kind: "technology_completed", category: auditAuthoritativeState, ev: TechnologyCompletedEvent{}},
		{name: "technology activated", kind: "technology_activated", category: auditAuthoritativeState, ev: TechnologyActivatedEvent{}},
		{name: "research progress applied", kind: "technology_progressed", category: auditAuthoritativeState, ev: ResearchProgressAppliedEvent{}},
		{name: "technology grant applied", kind: "technology_grant_applied", category: auditAuthoritativeState, ev: TechnologyGrantAppliedEvent{}},

		{name: "unit moved", kind: "unit_moved", category: auditAuthoritativeState, ev: UnitMovedEvent{}},
		{name: "unit damaged", kind: "unit_damaged", category: auditAuthoritativeState, ev: UnitDamagedEvent{}},
		{name: "unit died", kind: "unit_died", category: auditAuthoritativeState, ev: UnitDiedEvent{}},
		{name: "city core damaged", kind: "city_core_damaged", category: auditAuthoritativeState, ev: CityCoreDamagedEvent{}},
		{name: "city core destroyed", kind: "city_core_destroyed", category: auditAuthoritativeState, ev: CityCoreDestroyedEvent{}},
		{name: "road destroyed", kind: "road_destroyed", category: auditAuthoritativeState, ev: RoadDestroyedEvent{}},
		{name: "building damaged", kind: "building_damaged", category: auditAuthoritativeState, ev: BuildingDamagedEvent{}},
		{name: "conflict resolved", kind: "conflict", category: auditReportOnly, ev: ConflictResolvedEvent{Location: domain.Position{}}},

		{name: "city founded", kind: "city_founded", category: auditAuthoritativeState, ev: CityFoundedEvent{}},
		{name: "city founding failed", kind: "settle_city_failed", category: auditReportOnly, ev: CityFoundingFailedEvent{}},
		{name: "city captured", kind: "city_captured", category: auditAuthoritativeState, ev: CityCapturedEvent{}},
		{name: "facility takeover progressed", kind: "facility_takeover_progressed", category: auditAuthoritativeState, ev: FacilityTakeoverProgressedEvent{}},
		{name: "facility takeover completed", kind: "facility_takeover_completed", category: auditAuthoritativeState, ev: FacilityTakeoverCompletedEvent{}},
		{name: "building ruined", kind: "building_ruined", category: auditAuthoritativeState, ev: BuildingRuinedEvent{}},

		{name: "building built", kind: "building_built", category: auditAuthoritativeState, ev: BuildingBuiltEvent{}},
		{name: "building repaired", kind: "building_repaired", category: auditAuthoritativeState, ev: BuildingRepairedEvent{}},
		{name: "building skipped", kind: "building_skipped", category: auditReportOnly, ev: BuildSkippedEvent{}},
		{name: "resource produced", kind: "resource_produced", category: auditAuthoritativeState, ev: ResourceProducedEvent{}},
		{name: "resource flowed", kind: "resource_flowed", category: auditAuthoritativeState, ev: ResourceFlowedEvent{}},
		{name: "storage raided", kind: "storage_raided", category: auditAuthoritativeState, ev: StorageRaidedEvent{}},
		{name: "road built", kind: "road_built", category: auditAuthoritativeState, ev: RoadBuiltEvent{}},
		{name: "road repaired", kind: "road_repaired", category: auditAuthoritativeState, ev: RoadRepairedEvent{}},
		{name: "unit produced", kind: "unit_produced", category: auditAuthoritativeState, ev: UnitProducedEvent{}},
		{name: "point budget refreshed", kind: "point_budget_refreshed", category: auditAuthoritativeState, ev: PointBudgetRefreshedEvent{}},
		{name: "point spent", kind: "point_spent", category: auditAuthoritativeState, ev: PointSpentEvent{}},
		{name: "industry output refreshed", kind: "industry_output_refreshed", category: auditAuthoritativeState, ev: IndustryOutputRefreshedEvent{}},
		{name: "upkeep paid", kind: "upkeep_paid", category: auditAuthoritativeState, ev: UpkeepPaidEvent{}},
		{name: "unit starving", kind: "unit_starving", category: auditAuthoritativeState, ev: UnitStarvingEvent{}},

		{name: "recipe selected", kind: "recipe_selected", category: auditInternalStateOnly, ev: RecipeSelectionChangedEvent{}},
		{name: "recipe skipped", kind: "recipe_skipped", category: auditReportOnly, ev: RecipeSkippedEvent{}},
		{name: "building status changed", kind: "building_status_changed", category: auditAuthoritativeState, ev: BuildingStatusChangedEvent{}},
		{name: "recipe delayed", kind: "recipe_delayed", category: auditAuthoritativeState, ev: RecipeDelayedEvent{}},
		{name: "recipe progressed", kind: "recipe_progressed", category: auditAuthoritativeState, ev: RecipeProgressedEvent{}},
		{name: "recipe completed", kind: "recipe_completed", category: auditAuthoritativeState, ev: RecipeCompletedEvent{}},
	}
}

func TestEventAuditContractClassifiesCurrentEventKinds(t *testing.T) {
	t.Parallel()

	seenKinds := map[string]string{}
	categoryCounts := map[auditCategory]int{}
	for _, tc := range currentEventAuditContract() {
		tc := tc
		t.Run(tc.name, func(t *testing.T) {
			t.Parallel()
			if got := tc.ev.Kind(); got != tc.kind {
				t.Fatalf("Kind() = %q, want %q", got, tc.kind)
			}
			if strings.TrimSpace(tc.ev.String()) == "" {
				t.Fatalf("String() must be non-empty for audit/debug output")
			}
			switch tc.category {
			case auditAuthoritativeState, auditReportOnly, auditInternalStateOnly:
			default:
				t.Fatalf("unknown audit category %q", tc.category)
			}
		})
		if previous, ok := seenKinds[tc.kind]; ok {
			t.Fatalf("kind %q is shared by %s and %s", tc.kind, previous, tc.name)
		}
		seenKinds[tc.kind] = tc.name
		categoryCounts[tc.category]++
	}
	for _, category := range []auditCategory{auditAuthoritativeState, auditReportOnly, auditInternalStateOnly} {
		if categoryCounts[category] == 0 {
			t.Fatalf("audit category %q has no events", category)
		}
	}
}

func TestEventAuditContractCoversAllEventTypes(t *testing.T) {
	t.Parallel()

	contractTypes := map[string]struct{}{}
	for _, tc := range currentEventAuditContract() {
		contractTypes[eventTypeName(tc.ev)] = struct{}{}
	}

	sourceTypes := eventTypeNamesFromSource(t)
	for name := range sourceTypes {
		if _, ok := contractTypes[name]; !ok {
			t.Fatalf("event type %s is missing from currentEventAuditContract", name)
		}
	}
	for name := range contractTypes {
		if _, ok := sourceTypes[name]; !ok {
			t.Fatalf("event type %s is listed in currentEventAuditContract but not declared", name)
		}
	}
}

func eventTypeName(ev Event) string {
	eventType := reflect.TypeOf(ev)
	if eventType.Kind() == reflect.Pointer {
		eventType = eventType.Elem()
	}
	return eventType.Name()
}

func eventTypeNamesFromSource(t *testing.T) map[string]struct{} {
	t.Helper()

	_, currentFile, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatal("runtime.Caller failed")
	}
	dir := filepath.Dir(currentFile)
	files, err := filepath.Glob(filepath.Join(dir, "*.go"))
	if err != nil {
		t.Fatalf("glob event files: %v", err)
	}

	names := map[string]struct{}{}
	fset := token.NewFileSet()
	for _, file := range files {
		if strings.HasSuffix(file, "_test.go") {
			continue
		}
		parsed, err := parser.ParseFile(fset, file, nil, 0)
		if err != nil {
			t.Fatalf("parse %s: %v", file, err)
		}
		for _, decl := range parsed.Decls {
			gen, ok := decl.(*ast.GenDecl)
			if !ok || gen.Tok != token.TYPE {
				continue
			}
			for _, spec := range gen.Specs {
				typeSpec, ok := spec.(*ast.TypeSpec)
				if !ok || typeSpec.Name.Name == "Event" || !strings.HasSuffix(typeSpec.Name.Name, "Event") {
					continue
				}
				names[typeSpec.Name.Name] = struct{}{}
			}
		}
	}
	if len(names) == 0 {
		t.Fatal("no event types discovered")
	}
	return names
}
