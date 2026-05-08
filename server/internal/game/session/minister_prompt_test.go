package session

import (
	"strings"
	"testing"

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
