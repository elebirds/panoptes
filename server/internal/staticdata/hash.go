// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

import (
	"crypto/sha256"
	"encoding/json"
	"fmt"
	"sort"
)

func computeCatalogBundleHash(bundle CatalogBundle, maps []*MapRuntimeBundle) (string, error) {
	hash := sha256.New()
	raw, err := json.Marshal(bundle)
	if err != nil {
		return "", err
	}
	_, _ = hash.Write(raw)

	mapByID := make(map[string]*MapRuntimeBundle, len(maps))
	ids := make([]string, 0, len(maps))
	for _, runtime := range maps {
		if runtime == nil || runtime.ID == "" {
			continue
		}
		mapByID[runtime.ID] = runtime
		ids = append(ids, runtime.ID)
	}
	sort.Strings(ids)
	for _, id := range ids {
		raw, err := json.Marshal(mapByID[id])
		if err != nil {
			return "", err
		}
		_, _ = hash.Write(raw)
	}
	return fmt.Sprintf("%x", hash.Sum(nil)), nil
}
