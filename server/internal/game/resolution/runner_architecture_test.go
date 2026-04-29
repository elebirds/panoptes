package resolution

import (
	"go/parser"
	"go/token"
	"path/filepath"
	"strings"
	"testing"
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
