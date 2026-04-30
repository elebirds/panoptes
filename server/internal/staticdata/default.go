// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载静态数据运行时模型、目录索引或查询逻辑。

package staticdata

import (
	"sync"
)

var (
	defaultCatalogMu sync.RWMutex
	defaultCatalog   *Catalog
)

func SetDefault(c *Catalog) {
	defaultCatalogMu.Lock()
	defer defaultCatalogMu.Unlock()
	defaultCatalog = c
}

func Default() *Catalog {
	defaultCatalogMu.RLock()
	defer defaultCatalogMu.RUnlock()
	return defaultCatalog
}
