package websocket

import (
	"go/parser"
	"go/token"
	"path/filepath"
	"strings"
	"testing"
)

func TestWebsocketPackageDoesNotImportBusinessPackages(t *testing.T) {
	files, err := filepath.Glob("*.go")
	if err != nil {
		t.Fatalf("glob websocket files: %v", err)
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
			switch path {
			case "github.com/elebirds/panoptes/internal/auth",
				"github.com/elebirds/panoptes/internal/game",
				"github.com/elebirds/panoptes/internal/lobby":
				t.Fatalf("%s imports forbidden business package %s", file, path)
			}
		}
	}
}
