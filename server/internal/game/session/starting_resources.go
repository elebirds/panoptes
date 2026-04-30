// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话启动资源辅助函数。

package session

import "github.com/elebirds/panoptes/internal/domain"

func (r *Runtime) grantDevStartingResources() {
	if r == nil || r.state == nil || !r.IsDevMode() {
		return
	}

	for _, player := range r.state.Players {
		if player == nil {
			continue
		}
		player.Resources.Set(domain.ResourceOre, 200)
		player.Resources.Set(domain.ResourceWood, 200)
		player.Resources.Set(domain.ResourceFood, 200)
	}
}
