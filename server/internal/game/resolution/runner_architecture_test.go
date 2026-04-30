package resolution

import (
	"go/parser"
	"go/token"
	"path/filepath"
	"reflect"
	"strings"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
)

func TestResolutionPackageDoesNotImportGamePackage(t *testing.T) {
	files, err := filepath.Glob("*.go")
	if err != nil {
		t.Fatalf("glob resolution files: %v", err)
	}
	for _, file := range files {
		if strings.HasSuffix(file, "_test.go") {
			continue
		}
		parsed, err := parser.ParseFile(token.NewFileSet(), file, nil, parser.ImportsOnly)
		if err != nil {
			t.Fatalf("parse imports for %s: %v", file, err)
		}
		for _, spec := range parsed.Imports {
			path := strings.Trim(spec.Path.Value, `"`)
			if path == "github.com/elebirds/panoptes/internal/game" {
				t.Fatalf("%s imports forbidden package %s", file, path)
			}
		}
	}
}

func TestTurnResolutionRunnerCanRunWithoutGameRoom(t *testing.T) {
	collector := NewTurnResolutionRunner().Run(nil, RunnerHooks{})
	if collector == nil {
		t.Fatalf("collector is nil")
	}
}

func TestTurnResolutionRunnerStageOrderIsContract(t *testing.T) {
	runner := NewTurnResolutionRunner()
	got := make([]string, 0, len(runner.stages))
	for _, stage := range runner.stages {
		got = append(got, reflect.TypeOf(stage).Name())
	}
	want := []string{
		"PlanningCommitStage",
		"OrderFreezeStage",
		"UnitResolutionStage",
		"MapActionStage",
		"BuildingStage",
		"EconomyStage",
	}
	if !reflect.DeepEqual(got, want) {
		t.Fatalf("stage order = %#v, want %#v", got, want)
	}
}

func TestTurnResolutionRunnerStopsAfterStageOutcomeStop(t *testing.T) {
	var calls []string
	runner := &TurnResolutionRunner{
		stages: []ResolutionStage{
			recordingStage{name: "planning", calls: &calls},
			recordingStage{name: "unit-fatal", calls: &calls, stop: true},
			recordingStage{name: "map", calls: &calls},
		},
	}

	runner.Run(&domain.GameState{}, RunnerHooks{})

	want := []string{"planning", "unit-fatal"}
	if !reflect.DeepEqual(calls, want) {
		t.Fatalf("stage calls = %#v, want %#v", calls, want)
	}
}

type recordingStage struct {
	name  string
	stop  bool
	calls *[]string
}

func (s recordingStage) Run(*RunnerContext) StageOutcome {
	*s.calls = append(*s.calls, s.name)
	return StageOutcome{Stop: s.stop}
}
