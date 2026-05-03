// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

func TestAdaptCommandBatchPreservesContextOverrides(t *testing.T) {
	base := cmddispatch.InboundContext{
		PlayerID:     "player-1",
		ConnectionID: "conn-1",
		RequestID:    "req-base",
		TraceID:      "trace-1",
	}
	batch := &pb.MsgGameCommandBatch{Commands: []*pb.CommandEnvelope{
		{
			CommandId:     "cmd-1",
			ParticipantId: "player-2",
			Body: &pb.CommandEnvelope_SetResearchTarget{
				SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "tech-1"},
			},
		},
		{
			Body: &pb.CommandEnvelope_SetPolicy{
				SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "policy-1"},
			},
		},
	}}

	commands, ok := AdaptCommandBatch(base, batch)
	if !ok {
		t.Fatalf("AdaptCommandBatch() ok = false, want true")
	}
	if len(commands) != 2 {
		t.Fatalf("command count = %d, want 2", len(commands))
	}

	first := commands[0]
	if first.Context.PlayerID != "player-2" {
		t.Fatalf("first player id = %q, want player-2", first.Context.PlayerID)
	}
	if first.Context.RequestID != "cmd-1" {
		t.Fatalf("first request id = %q, want cmd-1", first.Context.RequestID)
	}
	if first.Context.ConnectionID != "conn-1" || first.Context.TraceID != "trace-1" {
		t.Fatalf("first context = %#v, want connection/trace preserved", first.Context)
	}
	if got := first.Command.GetSetResearchTarget().GetTechnologyId(); got != "tech-1" {
		t.Fatalf("first research target = %q, want tech-1", got)
	}

	second := commands[1]
	if second.Context.PlayerID != "player-1" {
		t.Fatalf("second player id = %q, want player-1", second.Context.PlayerID)
	}
	if second.Context.RequestID != "req-base" {
		t.Fatalf("second request id = %q, want req-base", second.Context.RequestID)
	}
	if got := second.Command.GetSetPolicy().GetNationalPolicyId(); got != "policy-1" {
		t.Fatalf("second policy id = %q, want policy-1", got)
	}
}

func TestAdaptCommandEnvelopeMapsEveryBatchBody(t *testing.T) {
	base := cmddispatch.InboundContext{PlayerID: "player-1", RequestID: "req-base"}
	tests := []struct {
		name     string
		envelope *pb.CommandEnvelope
		assert   func(*testing.T, *pb.PlanningCommand)
	}{
		{
			name: "set policy",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetPolicy{
				SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "policy-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetPolicy().GetNationalPolicyId(); got != "policy-1" {
					t.Fatalf("policy id = %q, want policy-1", got)
				}
			},
		},
		{
			name: "set institution loadout",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetInstitutionLoadout{
				SetInstitutionLoadout: &pb.MsgSetInstitutionLoadout{PolicyIds: []string{"academy_charter"}},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetInstitutionLoadout().GetPolicyIds(); len(got) != 1 || got[0] != "academy_charter" {
					t.Fatalf("institution ids = %#v, want [academy_charter]", got)
				}
			},
		},
		{
			name: "set research target",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetResearchTarget{
				SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "tech-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetResearchTarget().GetTechnologyId(); got != "tech-1" {
					t.Fatalf("technology id = %q, want tech-1", got)
				}
			},
		},
		{
			name: "set building recipe",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetBuildingRecipe{
				SetBuildingRecipe: &pb.MsgSetBuildingRecipe{NodeId: "A1", RecipeId: "recipe-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetBuildingRecipe().GetRecipeId(); got != "recipe-1" {
					t.Fatalf("recipe id = %q, want recipe-1", got)
				}
			},
		},
		{
			name: "build structure",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_BuildStructure{
				BuildStructure: &pb.MsgBuildStructure{NodeId: "A1", BuildingTypeId: "farm", CityId: "city-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetBuildStructure().GetBuildingTypeId(); got != "farm" {
					t.Fatalf("building type id = %q, want farm", got)
				}
			},
		},
		{
			name: "reveal node",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_RevealNode{
				RevealNode: &pb.MsgRevealNode{NodeId: "B2"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetRevealNode().GetNodeId(); got != "B2" {
					t.Fatalf("node id = %q, want B2", got)
				}
			},
		},
		{
			name: "set war zone",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetWarZone{
				SetWarZone: &pb.MsgSetWarZone{ZoneId: "war-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetWarZone().GetZoneId(); got != "war-1" {
					t.Fatalf("war zone id = %q, want war-1", got)
				}
			},
		},
		{
			name: "war zone directive",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_WarZoneDirective{
				WarZoneDirective: &pb.MsgWarZoneDirective{ZoneId: "war-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetWarZoneDirective().GetZoneId(); got != "war-1" {
					t.Fatalf("war zone id = %q, want war-1", got)
				}
			},
		},
		{
			name: "set minister directive",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SetMinisterDirective{
				SetMinisterDirective: &pb.MsgSetMinisterDirective{MinisterRole: "domestic", Content: "{}"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetSetMinisterDirective().GetMinisterRole(); got != "domestic" {
					t.Fatalf("minister role = %q, want domestic", got)
				}
			},
		},
		{
			name: "issue unit order",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_IssueUnitOrder{
				IssueUnitOrder: &pb.MsgIssueUnitOrder{UnitId: "unit-1", Action: "move"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetIssueUnitOrder().GetUnitId(); got != "unit-1" {
					t.Fatalf("unit id = %q, want unit-1", got)
				}
			},
		},
		{
			name: "cancel unit order",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_CancelUnitOrder{
				CancelUnitOrder: &pb.MsgCancelUnitOrder{UnitId: "unit-1"},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if got := cmd.GetCancelUnitOrder().GetUnitId(); got != "unit-1" {
					t.Fatalf("unit id = %q, want unit-1", got)
				}
			},
		},
		{
			name: "submit turn",
			envelope: &pb.CommandEnvelope{Body: &pb.CommandEnvelope_SubmitTurn{
				SubmitTurn: &pb.MsgSubmitTurn{},
			}},
			assert: func(t *testing.T, cmd *pb.PlanningCommand) {
				t.Helper()
				if cmd.GetSubmitTurn() == nil {
					t.Fatalf("submit turn command is nil")
				}
			},
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			command, ok := AdaptCommandEnvelope(base, tt.envelope)
			if !ok {
				t.Fatalf("AdaptCommandEnvelope() ok = false, want true")
			}
			tt.assert(t, command.Command)
		})
	}
}

func TestAdaptCommandBatchRejectsInvalidEnvelope(t *testing.T) {
	base := cmddispatch.InboundContext{PlayerID: "player-1", RequestID: "req-base"}
	if _, ok := AdaptCommandBatch(base, nil); ok {
		t.Fatalf("AdaptCommandBatch(nil) ok = true, want false")
	}
	if _, ok := AdaptCommandBatch(base, &pb.MsgGameCommandBatch{Commands: []*pb.CommandEnvelope{{}}}); ok {
		t.Fatalf("AdaptCommandBatch(empty envelope) ok = true, want false")
	}
}
