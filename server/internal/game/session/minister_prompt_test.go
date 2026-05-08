package session

import (
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestBuildMinisterObservationSummaryAddsRoleSpecificFocus(t *testing.T) {
	observation := &gamequery.ObservationSnapshot{
		ViewerID: "player-1",
		VisibleNodes: []*pb.NodeView{
			{Id: "A1", IsResourcePoint: true, ResourceType: "food"},
			{Id: "B2", BuildingTypeId: "farm"},
			{Id: "C3", EnemyUnitCount: 2},
		},
		Units: []*pb.UnitView{
			{Id: "u1", Faction: "player-1", UnitType: "infantry"},
		},
	}

	domestic := buildMinisterObservationSummary(nil, observation, "domestic")
	if !strings.Contains(domestic, "role_focus=domestic") ||
		!strings.Contains(domestic, "domestic_resource_nodes=A1:food") ||
		!strings.Contains(domestic, "domestic_building_nodes=B2:farm") {
		t.Fatalf("domestic summary = %q, want domestic focus", domestic)
	}

	works := buildMinisterObservationSummary(nil, observation, "works")
	if !strings.Contains(works, "role_focus=works") ||
		!strings.Contains(works, "works_resource_nodes=A1:food") ||
		!strings.Contains(works, "works_building_nodes=B2:farm") {
		t.Fatalf("works summary = %q, want works focus", works)
	}

	defense := buildMinisterObservationSummary(nil, observation, "defense")
	if !strings.Contains(defense, "role_focus=defense") ||
		!strings.Contains(defense, "defense_visible_units=u1:player-1:infantry") ||
		!strings.Contains(defense, "defense_enemy_pressure_nodes=C3") {
		t.Fatalf("defense summary = %q, want defense focus", defense)
	}

	command := buildMinisterObservationSummary(nil, observation, "command")
	if !strings.Contains(command, "role_focus=command") ||
		!strings.Contains(command, "command_visible_units=u1:player-1:infantry") ||
		!strings.Contains(command, "command_enemy_pressure_nodes=C3") {
		t.Fatalf("command summary = %q, want command focus", command)
	}

	frontier := buildMinisterObservationSummary(nil, observation, "frontier")
	if !strings.Contains(frontier, "role_focus=frontier") ||
		!strings.Contains(frontier, "frontier_unknown_nodes=A1,B2") ||
		!strings.Contains(frontier, "frontier_pressure_nodes=C3") {
		t.Fatalf("frontier summary = %q, want frontier focus", frontier)
	}
}

func TestBuildMinisterObservationSummaryAddsDistortionMetadata(t *testing.T) {
	observation := &gamequery.ObservationSnapshot{
		ViewerID:      "player-1",
		ReportingMode: gamequery.ReportingModeHighDistortion,
		Nodes: []*pb.NodeView{
			{Id: "A1", IsCurrentlyVisible: true},
			{Id: "B2"},
			{Id: "C3", IsMemory: true},
		},
		VisibleNodes: []*pb.NodeView{
			{Id: "A1", IsCurrentlyVisible: true},
		},
		MemoryNodes: []*pb.NodeView{
			{Id: "C3", IsMemory: true},
		},
		Units: []*pb.UnitView{
			{Id: "u1", Faction: "player-1", UnitType: "infantry"},
		},
		MemoryUnits: []*gamequery.RememberedUnitView{
			{View: &pb.UnitView{Id: "enemy-1", Faction: "enemy", UnitType: "infantry"}, LastObservedTurn: 3},
		},
	}

	summary := buildMinisterObservationSummary(nil, observation, "command")
	for _, want := range []string{
		"report_mode=high_distortion",
		"report_confidence=low",
		"reported_omitted=1",
		"reported_delayed=2",
		"reported_misread=1",
	} {
		if !strings.Contains(summary, want) {
			t.Fatalf("summary = %q, want %q", summary, want)
		}
	}
}

func TestBuildMinisterActionCandidateSummaryFiltersCurrentRoleCandidates(t *testing.T) {
	drafts := []domain.MinisterDraft{
		{
			DraftID:      "domestic:research:bronze_working:3",
			PlayerID:     "player-1",
			MinisterRole: "domestic",
			Kind:         domain.MinisterDraftKindResearch,
			TargetID:     "bronze_working",
			TargetLabel:  "Bronze Working",
			Status:       domain.MinisterDraftStatusPending,
			Available:    true,
			Turn:         3,
			Source:       domain.MinisterDraftSourceRuleOnly,
		},
		{
			DraftID:      "military:unit_order:u1_move_a2:3",
			PlayerID:     "player-1",
			MinisterRole: "military",
			Kind:         domain.MinisterDraftKindUnitOrder,
			TargetID:     "u1:move:A2:",
			TargetLabel:  "u1 move -> A2",
			Status:       domain.MinisterDraftStatusPending,
			Available:    true,
			Turn:         3,
			Source:       domain.MinisterDraftSourceRuleOnly,
		},
		{
			DraftID:      "domestic:policy:stale:2",
			PlayerID:     "player-1",
			MinisterRole: "domestic",
			Kind:         domain.MinisterDraftKindPolicy,
			TargetID:     "expansion",
			TargetLabel:  "Expansion",
			Status:       domain.MinisterDraftStatusStale,
			Available:    false,
			Turn:         2,
			Source:       domain.MinisterDraftSourceRuleOnly,
		},
	}

	summary := buildMinisterActionCandidateSummaryFromDrafts(3, drafts, "command")
	if !strings.Contains(summary, "candidate_id=military:unit_order:u1_move_a2:3") ||
		!strings.Contains(summary, "kind=unit_order") ||
		!strings.Contains(summary, "target_id=u1:move:A2:") {
		t.Fatalf("summary = %q, want command current candidate", summary)
	}
	if strings.Contains(summary, "domestic:research:bronze_working:3") || strings.Contains(summary, "stale") {
		t.Fatalf("summary = %q, want only current pending command candidates", summary)
	}
}
