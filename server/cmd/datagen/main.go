// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 提供 Panoptes 数据生成命令入口。

package main

import (
	"flag"
	"log"
	"path/filepath"

	"github.com/elebirds/panoptes/internal/datagen"
)

func main() {
	validateOnly := flag.Bool("validate", false, "validate source data without writing any files")
	flag.Parse()

	repoRoot, err := filepath.Abs(filepath.Join(".."))
	if err != nil {
		log.Fatalf("resolve repo root: %v", err)
	}

	opts := datagen.Options{RepoRoot: repoRoot}
	if *validateOnly {
		if err := datagen.Validate(opts); err != nil {
			log.Fatalf("data validation failed: %v", err)
		}
		log.Printf("data validation passed: %s", repoRoot)
		return
	}

	if err := datagen.Generate(opts); err != nil {
		log.Fatalf("data generation failed: %v", err)
	}
	log.Printf("data generation completed: %s", repoRoot)
}
