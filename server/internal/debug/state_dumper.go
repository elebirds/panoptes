// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现调试支持模块的状态转储逻辑。

package debug

import (
	"fmt"
	"log/slog"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/yohamta/donburi"
)

func DumpGameStateSummary(state *domain.GameState) {
	if state == nil {
		return
	}

	nodeCount := countNodes(state.World)
	unitCount := countUnits(state.World)

	slog.Debug("游戏状态摘要",
		"turn", state.Turn,
		"phase", state.Phase,
		"players", formatPlayers(state),
		"node_count", nodeCount,
		"unit_count", unitCount,
	)
}

func formatPlayers(state *domain.GameState) string {
	if state == nil || len(state.Players) == 0 {
		return ""
	}

	ids := make([]string, 0, len(state.Players))
	for playerID := range state.Players {
		ids = append(ids, playerID)
	}
	sort.Strings(ids)

	parts := make([]string, 0, len(ids))
	for _, playerID := range ids {
		p := state.Players[playerID]
		if p == nil {
			continue
		}
		parts = append(parts, fmt.Sprintf(
			"%s: resources={ore:%d wood:%d food:%d bp:%d} tokens=%d castle_hp=%d",
			playerID,
			p.Resources.Get(domain.ResourceOre),
			p.Resources.Get(domain.ResourceWood),
			p.Resources.Get(domain.ResourceFood),
			p.Resources.Get(domain.ResourceBuildPoints),
			p.TokensLeft,
			p.MainCastleHP,
		))
	}

	return strings.Join(parts, " | ")
}

func countNodes(world donburi.World) int {
	if world == nil {
		return 0
	}
	count := 0
	ecs.AllNodes(world).Each(world, func(_ *donburi.Entry) {
		count++
	})
	return count
}

func countUnits(world donburi.World) int {
	if world == nil {
		return 0
	}
	count := 0
	ecs.AllUnits(world).Each(world, func(_ *donburi.Entry) {
		count++
	})
	return count
}
