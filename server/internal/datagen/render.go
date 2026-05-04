// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: Renders generated protocol and resource-key source files.

package datagen

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/staticdata"
)

func renderDataTypesProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

option go_package = "github.com/elebirds/panoptes/internal/gen/proto;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message ResourceValue {
  string key = 1;
  int32 amount = 2;
}

message ResourceBag {
  repeated ResourceValue items = 1;
}

message PointValue {
  string key = 1;
  int32 amount = 2;
}

message PointBag {
  repeated PointValue items = 1;
}

message ResourceDescriptor {
  string key = 1;
  string display_name = 2;
  string description = 3;
  string icon_key = 4;
  int32 sort_order = 5;
  int32 proto_number = 6;
  bool visible_in_hud = 7;
}

message PointDescriptor {
  string key = 1;
  string display_name = 2;
  string description = 3;
  string icon_key = 4;
  int32 sort_order = 5;
  bool visible_in_hud = 6;
}

message CatalogSectionHash {
  string section_name = 1;
  string hash = 2;
}

message StaticCatalogManifest {
  string schema_version = 1;
  string content_version = 2;
  string bundle_hash = 3;
  string default_locale = 4;
  string default_map_id = 5;
  repeated string required_sections = 6;
  repeated CatalogSectionHash section_hashes = 7;
}`
}

func renderDataCatalogProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

import "panoptes/proto/v1/data_types.proto";

option go_package = "github.com/elebirds/panoptes/internal/gen/proto;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message UnitCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string prefab_key = 5;
  repeated string tags = 6;
  bool can_attack_structures = 7;
}

message BuildingCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string prefab_key = 5;
  string placement_kind = 6;
  string building_scope = 7;
  string required_resource_type = 8;
  string takeover_mode = 9;
  repeated string tags = 10;
}

message TechnologyCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string branch = 5;
  int32 tier = 6;
  int32 research_cost = 7;
  repeated string tags = 8;
}

message PolicyCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string layer = 5;
  string activation_timing = 6;
  repeated string tags = 7;
}

message RecipeCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string building_id = 5;
  int32 work_amount = 6;
  int32 base_progress = 7;
  repeated string tags = 8;
}

message TerrainCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string icon_key = 4;
  string material_key = 5;
  repeated string tags = 6;
}

message StaticCatalogSnapshot {
  StaticCatalogManifest manifest = 1;
  repeated ResourceDescriptor resources = 2;
  repeated PointDescriptor points = 3;
  repeated UnitCatalogEntry units = 4;
  repeated BuildingCatalogEntry buildings = 5;
  repeated TechnologyCatalogEntry technologies = 6;
  repeated PolicyCatalogEntry policies = 7;
  repeated RecipeCatalogEntry recipes = 8;
  repeated TerrainCatalogEntry terrains = 9;
}

message MsgStaticCatalogManifest {
  StaticCatalogManifest manifest = 1;
}

message MsgStaticCatalogSyncRequest {
  string bundle_hash = 1;
  repeated string section_names = 2;
  bool force_full_sync = 3;
}

message MsgStaticCatalogSectionChunk {
  string section_name = 1;
  string section_hash = 2;
  uint32 chunk_index = 3;
  uint32 chunk_count = 4;
  string compression = 5;
  bytes payload = 6;
}

message MsgStaticCatalogSyncComplete {
  string applied_bundle_hash = 1;
  bool success = 2;
  string error = 3;
}

message MsgStaticCatalogSnapshot {
  StaticCatalogSnapshot snapshot = 1;
}`
}

func renderMapCatalogProto() string {
	return `syntax = "proto3";

package panoptes.proto.v1;

option go_package = "github.com/elebirds/panoptes/internal/gen/proto;protov1";
option csharp_namespace = "Panoptes.Protocol.V1";

message MapLegendEntry {
  string id = 1;
  string name = 2;
  string icon_key = 3;
}

message MapCatalogEntry {
  string id = 1;
  string name = 2;
  string description = 3;
  string thumbnail_key = 4;
  int32 width = 5;
  int32 height = 6;
}

message MapCatalog {
  repeated MapCatalogEntry maps = 1;
}`
}

func renderGoResourceKeys(bundle staticdata.CatalogBundle) string {
	builder := &strings.Builder{}
	builder.WriteString("package generated\n\nconst (\n")
	for _, resource := range bundle.Resources {
		builder.WriteString(fmt.Sprintf("\tResource%s = %q\n", exportedIdentifier(resource.Key), resource.Key))
	}
	builder.WriteString(")\n")
	return builder.String()
}

func renderCSharpResourceKeys(bundle staticdata.CatalogBundle) string {
	builder := &strings.Builder{}
	builder.WriteString("namespace Panoptes.Core.Domain\n{\n    public static class ResourceKeys\n    {\n")
	for _, resource := range bundle.Resources {
		builder.WriteString(fmt.Sprintf("        public const string Resource%s = \"%s\";\n", exportedIdentifier(resource.Key), resource.Key))
	}
	builder.WriteString("    }\n}\n")
	return builder.String()
}

func exportedIdentifier(key string) string {
	parts := strings.FieldsFunc(key, func(r rune) bool { return r == '_' || r == '-' })
	for i := range parts {
		if parts[i] == "" {
			continue
		}
		parts[i] = strings.ToUpper(parts[i][:1]) + parts[i][1:]
	}
	return strings.Join(parts, "")
}
