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

	military := buildMinisterObservationSummary(nil, observation, "military")
	if !strings.Contains(military, "role_focus=military") ||
		!strings.Contains(military, "military_visible_units=u1:player-1:infantry") ||
		!strings.Contains(military, "military_enemy_pressure_nodes=C3") {
		t.Fatalf("military summary = %q, want military focus", military)
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

	summary := buildMinisterObservationSummary(nil, observation, "military")
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

	summary := buildMinisterActionCandidateSummaryFromDrafts(3, drafts, "domestic")
	if !strings.Contains(summary, "candidate_id=domestic:research:bronze_working:3") ||
		!strings.Contains(summary, "kind=research") ||
		!strings.Contains(summary, "target_id=bronze_working") {
		t.Fatalf("summary = %q, want domestic current candidate", summary)
	}
	if strings.Contains(summary, "military:unit_order") || strings.Contains(summary, "stale") {
		t.Fatalf("summary = %q, want only current pending domestic candidates", summary)
	}
}
