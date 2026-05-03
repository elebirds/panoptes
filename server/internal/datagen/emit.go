// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Emits generated static data, schema, protocol, and code files.

package datagen

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"sort"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func emitGeneratedFiles(repoRoot string, bundle staticdata.CatalogBundle, maps map[string]*staticdata.MapRuntimeBundle, schemas schemaSet) error {
	serverGen := filepath.Join(repoRoot, "data/generated/server")
	clientData := filepath.Join(repoRoot, "client/Assets/Resources/Data")
	schemaDir := filepath.Join(repoRoot, "data/schema")
	serverGoGen := filepath.Join(repoRoot, "server/internal/staticdata/generated")
	clientCodeGen := filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Core/Foundation/Domain")
	protocolDir := filepath.Join(repoRoot, "protocol/panoptes/proto/v1")

	dirs := []string{
		serverGen,
		filepath.Join(serverGen, "maps"),
		filepath.Join(serverGen, "sections"),
		clientData,
		filepath.Join(clientData, "maps"),
		filepath.Join(clientData, "sections"),
		filepath.Join(schemaDir, "registry"),
		filepath.Join(schemaDir, "content"),
		filepath.Join(schemaDir, "content", "maps"),
		filepath.Join(schemaDir, "ui"),
		filepath.Join(schemaDir, "ui", "maps"),
		serverGoGen,
		clientCodeGen,
		protocolDir,
	}
	for _, dir := range dirs {
		if err := os.MkdirAll(dir, 0o755); err != nil {
			return fmt.Errorf("mkdir %q: %w", dir, err)
		}
	}
	if err := removeLegacyGeneratedFiles(repoRoot, schemaDir); err != nil {
		return err
	}

	if err := writePrettyJSON(filepath.Join(serverGen, "catalog.bundle.json"), bundle); err != nil {
		return err
	}
	if err := writePrettyJSON(filepath.Join(clientData, "catalog.bundle.json"), bundle); err != nil {
		return err
	}
	for id, runtime := range maps {
		if err := writePrettyJSON(filepath.Join(serverGen, "maps", id+".runtime.json"), runtime); err != nil {
			return err
		}
		if err := writePrettyJSON(filepath.Join(clientData, "maps", id+".runtime.json"), runtime); err != nil {
			return err
		}
	}
	for _, section := range staticdata.CatalogSectionValues(bundle) {
		if err := writePrettyJSON(filepath.Join(serverGen, "sections", section.Name+".json"), section.Value); err != nil {
			return err
		}
		if err := writePrettyJSON(filepath.Join(clientData, "sections", section.Name+".json"), section.Value); err != nil {
			return err
		}
	}

	if err := emitSchemas(schemaDir, schemas); err != nil {
		return err
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "data_types.proto"), []byte(renderDataTypesProto()), 0o600); err != nil {
		return fmt.Errorf("write data_types.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "data_catalog.proto"), []byte(renderDataCatalogProto()), 0o600); err != nil {
		return fmt.Errorf("write data_catalog.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(protocolDir, "map_catalog.proto"), []byte(renderMapCatalogProto()), 0o600); err != nil {
		return fmt.Errorf("write map_catalog.proto: %w", err)
	}
	if err := os.WriteFile(filepath.Join(serverGoGen, "resource_keys_gen.go"), []byte(renderGoResourceKeys(bundle)), 0o600); err != nil {
		return fmt.Errorf("write resource_keys_gen.go: %w", err)
	}
	if err := os.WriteFile(filepath.Join(clientCodeGen, "ResourceKeys.g.cs"), []byte(renderCSharpResourceKeys(bundle)), 0o600); err != nil {
		return fmt.Errorf("write ResourceKeys.g.cs: %w", err)
	}

	return nil
}

func removeLegacyGeneratedFiles(repoRoot string, schemaDir string) error {
	legacyFiles := []string{
		filepath.Join(schemaDir, "ui", "resource_catalog.schema.json"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated/ResourceKeys.g.cs"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated/ResourceKeys.g.cs.meta"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated.meta"),
		filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data.meta"),
		filepath.Join(repoRoot, "protocol/data_types.proto"),
		filepath.Join(repoRoot, "protocol/data_catalog.proto"),
		filepath.Join(repoRoot, "protocol/map_catalog.proto"),
	}
	for _, path := range legacyFiles {
		err := os.Remove(path)
		if err != nil && !os.IsNotExist(err) {
			return fmt.Errorf("remove legacy generated file %q: %w", path, err)
		}
	}
	_ = os.Remove(filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data/Generated"))
	_ = os.Remove(filepath.Join(repoRoot, "client/Assets/Scripts/Runtime/Data"))
	return nil
}

func emitSchemas(schemaDir string, schemas schemaSet) error {
	for rel, value := range schemas {
		if err := writePrettyJSON(filepath.Join(schemaDir, rel), value); err != nil {
			return err
		}
	}
	return nil
}

func computeBundleHash(bundle staticdata.CatalogBundle, maps map[string]*staticdata.MapRuntimeBundle) (string, error) {
	h := sha256.New()
	raw, err := json.Marshal(bundle)
	if err != nil {
		return "", fmt.Errorf("marshal bundle hash input: %w", err)
	}
	_, _ = h.Write(raw)
	ids := make([]string, 0, len(maps))
	for id := range maps {
		ids = append(ids, id)
	}
	sort.Strings(ids)
	for _, id := range ids {
		mapRaw, err := json.Marshal(maps[id])
		if err != nil {
			return "", fmt.Errorf("marshal map hash input %q: %w", id, err)
		}
		_, _ = h.Write(mapRaw)
	}
	return fmt.Sprintf("%x", h.Sum(nil)), nil
}
