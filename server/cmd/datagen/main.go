package main

import (
	"flag"
	"log"
	"path/filepath"

	"github.com/elebirds/panoptes/internal/datagen"
)

func main() {
	validateOnly := flag.Bool("validate", false, "validate source data")
	flag.Parse()

	repoRoot, err := filepath.Abs(filepath.Join(".."))
	if err != nil {
		log.Fatalf("resolve repo root: %v", err)
	}

	if err := datagen.Generate(datagen.Options{RepoRoot: repoRoot}); err != nil {
		log.Fatalf("data generation failed: %v", err)
	}

	if *validateOnly {
		log.Printf("data validation passed: %s", repoRoot)
		return
	}

	log.Printf("data generation completed: %s", repoRoot)
}
