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

	for playerID, player := range r.state.Players {
		if player == nil {
			continue
		}
		r.state.AddResource(playerID, domain.ResourceOre, 200)
		r.state.AddResource(playerID, domain.ResourceWood, 200)
		r.state.AddResource(playerID, domain.ResourceFood, 200)
	}
}
