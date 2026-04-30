// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
)

func LoadDir(dir string) (*Catalog, error) {
	raw, err := os.ReadFile(filepath.Join(dir, "catalog.bundle.json"))
	if err != nil {
		return nil, fmt.Errorf("read catalog bundle: %w", err)
	}

	var bundle CatalogBundle
	if err := json.Unmarshal(raw, &bundle); err != nil {
		return nil, fmt.Errorf("unmarshal catalog bundle: %w", err)
	}

	catalog := NewCatalog(bundle)
	for _, entry := range bundle.Maps {
		path := filepath.Join(dir, "maps", entry.ID+".runtime.json")
		mapRaw, err := os.ReadFile(path)
		if err != nil {
			return nil, fmt.Errorf("read map bundle %q: %w", entry.ID, err)
		}
		var runtime MapRuntimeBundle
		if err := json.Unmarshal(mapRaw, &runtime); err != nil {
			return nil, fmt.Errorf("unmarshal map bundle %q: %w", entry.ID, err)
		}
		runtimeCopy := runtime
		catalog.maps[entry.ID] = &runtimeCopy
	}

	return catalog, nil
}
