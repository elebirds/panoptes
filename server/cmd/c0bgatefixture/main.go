// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Description: Generates deterministic C0b backend-client protocol fixtures.

package main

import (
	"flag"
	"fmt"
	"os"
	"strings"
	"time"

	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport/codec"
	"google.golang.org/protobuf/proto"
)

const (
	defaultOutputPath = "../client/Assets/Scripts/Tests/EditMode/Fixtures/C0b/server_frames.jsonl"
	fixturePlayerID   = "player-1"
	fixtureSessionID  = "research_unlock_build"
)

func main() {
	outPath := flag.String("out", defaultOutputPath, "path to write JSONL ServerFrame fixture")
	flag.Parse()

	lines, err := buildFixtureLines()
	if err != nil {
		exitf("build fixture: %v", err)
	}

	if err := os.WriteFile(*outPath, []byte(strings.Join(lines, "\n")+"\n"), 0o644); err != nil {
		exitf("write %s: %v", *outPath, err)
	}
}

func buildFixtureLines() ([]string, error) {
	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		return nil, err
	}

	harness, err := debug.NewHarness(def)
	if err != nil {
		return nil, err
	}
	if err := harness.Start(); err != nil {
		return nil, err
	}

	if _, err := harness.WaitPlanningStart(fixturePlayerID, 1, 2*time.Second); err != nil {
		return nil, err
	}
	if err := harness.InjectPlanningCommand(fixturePlayerID, "c0b-research", &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetResearchTarget{
			SetResearchTarget: &pb.MsgSetResearchTarget{TechnologyId: "agri_unlock_farm"},
		},
	}); err != nil {
		return nil, err
	}
	if err := harness.SubmitTurn(fixturePlayerID); err != nil {
		return nil, err
	}
	if _, err := harness.WaitGameSync(fixturePlayerID, 1, 3*time.Second); err != nil {
		return nil, err
	}
	if _, err := harness.WaitPlanningStart(fixturePlayerID, 2, 2*time.Second); err != nil {
		return nil, err
	}

	messages := []proto.Message{
		buildStaticCatalogSnapshot(),
		firstMessage[*pb.MsgGameInit](harness.Messages(fixturePlayerID), func(msg *pb.MsgGameInit) bool {
			return msg.GetGameId() == fixtureSessionID
		}),
		firstMessage[*pb.MsgPlanningStart](harness.Messages(fixturePlayerID), func(msg *pb.MsgPlanningStart) bool {
			return msg.GetTurn() == 1
		}),
		firstMessage[*pb.MsgPlanningSnapshot](harness.Messages(fixturePlayerID), func(msg *pb.MsgPlanningSnapshot) bool {
			return msg.GetPlannedResearchTargetTechnologyId() == "agri_unlock_farm"
		}),
		firstMessage[*pb.MsgResearchResult](harness.Messages(fixturePlayerID), func(msg *pb.MsgResearchResult) bool {
			return msg.GetSuccess() && msg.GetTechnologyId() == "agri_unlock_farm"
		}),
		firstMessage[*pb.MsgGameSync](harness.Messages(fixturePlayerID), func(msg *pb.MsgGameSync) bool {
			return msg.GetTurn() == 1
		}),
		firstMessage[*pb.MsgPlanningStart](harness.Messages(fixturePlayerID), func(msg *pb.MsgPlanningStart) bool {
			return msg.GetTurn() == 2
		}),
	}

	lines := make([]string, 0, len(messages))
	for _, msg := range messages {
		if msg == nil {
			return nil, fmt.Errorf("selected fixture message is nil")
		}
		line, err := encodeLine(msg)
		if err != nil {
			return nil, err
		}
		lines = append(lines, line)
	}
	return lines, nil
}

func buildStaticCatalogSnapshot() *pb.MsgStaticCatalogSnapshot {
	return &pb.MsgStaticCatalogSnapshot{
		Snapshot: &pb.StaticCatalogSnapshot{
			Manifest: &pb.StaticCatalogManifest{
				SchemaVersion:  "2026-05-04-c0b",
				ContentVersion: "c0b-contract-fixture",
				BundleHash:     "c0b-contract-fixture",
				DefaultLocale:  "en-US",
				DefaultMapId:   "research_unlock_build",
				RequiredSections: []string{
					"buildings",
					"recipes",
					"technologies",
					"policies",
					"units",
				},
			},
			Resources: []*pb.ResourceDescriptor{
				{Key: "food", IconKey: "resource_food", SortOrder: 10, VisibleInHud: true},
				{Key: "wood", IconKey: "resource_wood", SortOrder: 20, VisibleInHud: true},
				{Key: "ore", IconKey: "resource_ore", SortOrder: 30, VisibleInHud: true},
			},
			Points: []*pb.PointDescriptor{
				{Key: "industry_output", IconKey: "point_industry", SortOrder: 10, VisibleInHud: true},
			},
			Buildings: []*pb.BuildingCatalogEntry{
				{
					Id:            "city_core",
					Name:          "City Core",
					Description:   "Capital and city administration center.",
					PlacementKind: "city_foundation_center",
					BuildingScope: "city_core",
					TakeoverMode:  "disabled",
				},
				{
					Id:            "farm",
					Name:          "Farm",
					Description:   "Converts land and labor into food.",
					PlacementKind: "city_territory",
					BuildingScope: "out_of_city",
					TakeoverMode:  "delayed",
				},
			},
			Recipes: []*pb.RecipeCatalogEntry{
				{
					Id:           "farm_food",
					Name:         "Grow Food",
					Description:  "Maintains a basic food supply.",
					BuildingId:   "farm",
					WorkAmount:   1,
					BaseProgress: 1,
				},
			},
			Technologies: []*pb.TechnologyCatalogEntry{
				{
					Id:           "agri_unlock_farm",
					Name:         "Agrarian Foundations",
					Description:  "Unlocks organized farming.",
					Branch:       "agriculture",
					Tier:         1,
					ResearchCost: 1,
				},
			},
			Policies: []*pb.PolicyCatalogEntry{
				{
					Id:               "grain_reserve",
					Name:             "Grain Reserve",
					Description:      "Prioritize resilient food reserves.",
					Layer:            "national_focus",
					ActivationTiming: "next_turn",
				},
				{
					Id:               "field_office",
					Name:             "Field Office",
					Description:      "Improve local administrative response.",
					Layer:            "institution",
					ActivationTiming: "next_turn",
				},
			},
			Units: []*pb.UnitCatalogEntry{
				{
					Id:                  "settler",
					Name:                "Settler",
					Description:         "Founds a new city core.",
					PrefabKey:           "TT_Settler",
					CanAttackStructures: false,
				},
			},
		},
	}
}

func encodeLine(msg proto.Message) (string, error) {
	raw, err := codec.EncodeServerMessage(msg, &pb.EventMeta{
		RequestId:        "c0b-fixture",
		TraceId:          "c0b-fixture",
		ServerUnixMillis: 1,
		GameSessionId:    fixtureSessionID,
	})
	if err != nil {
		return "", fmt.Errorf("encode %s: %w", msg.ProtoReflect().Descriptor().FullName(), err)
	}
	return string(raw), nil
}

func firstMessage[T proto.Message](messages []proto.Message, predicate func(T) bool) T {
	var zero T
	for _, msg := range messages {
		typed, ok := msg.(T)
		if !ok {
			continue
		}
		if predicate == nil || predicate(typed) {
			return typed
		}
	}
	return zero
}

func exitf(format string, args ...any) {
	_, _ = fmt.Fprintf(os.Stderr, format+"\n", args...)
	os.Exit(1)
}
