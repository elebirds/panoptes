// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现静态目录同步负载辅助函数。

package session

import (
	"bytes"
	"compress/gzip"
	"strings"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func resolveRequestedSections(catalog *staticdata.Catalog, req *pb.MsgStaticCatalogSyncRequest) []string {
	if catalog == nil {
		return nil
	}
	manifest := catalog.Manifest()
	if req == nil {
		return append([]string(nil), manifest.RequiredSections...)
	}
	if req.GetForceFullSync() {
		return append([]string(nil), manifest.RequiredSections...)
	}
	if len(req.GetSectionNames()) == 0 {
		if req.GetBundleHash() != "" && req.GetBundleHash() == catalog.BundleHash() {
			return nil
		}
		return append([]string(nil), manifest.RequiredSections...)
	}

	allowed := make(map[string]struct{}, len(manifest.RequiredSections))
	for _, section := range manifest.RequiredSections {
		allowed[section] = struct{}{}
	}

	seen := make(map[string]struct{}, len(req.GetSectionNames()))
	sections := make([]string, 0, len(req.GetSectionNames()))
	for _, section := range req.GetSectionNames() {
		section = strings.TrimSpace(section)
		if section == "" {
			continue
		}
		if _, ok := allowed[section]; !ok {
			continue
		}
		if _, ok := seen[section]; ok {
			continue
		}
		seen[section] = struct{}{}
		sections = append(sections, section)
	}
	return sections
}

func compressCatalogSection(raw []byte) ([]byte, error) {
	var buffer bytes.Buffer
	writer := gzip.NewWriter(&buffer)
	if _, err := writer.Write(raw); err != nil {
		_ = writer.Close()
		return nil, err
	}
	if err := writer.Close(); err != nil {
		return nil, err
	}
	return buffer.Bytes(), nil
}

func splitCatalogSection(raw []byte, chunkSize int) [][]byte {
	if chunkSize <= 0 || len(raw) <= chunkSize {
		return [][]byte{raw}
	}
	chunks := make([][]byte, 0, (len(raw)+chunkSize-1)/chunkSize)
	for start := 0; start < len(raw); start += chunkSize {
		end := start + chunkSize
		if end > len(raw) {
			end = len(raw)
		}
		chunk := make([]byte, end-start)
		copy(chunk, raw[start:end])
		chunks = append(chunks, chunk)
	}
	return chunks
}
