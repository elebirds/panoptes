// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-17 00:00:00 +0800
// Description: 提供大厅 Redis 命名空间的清理能力。

package redis

import (
	"context"
	"errors"
	"fmt"
)

const lobbyNamespacePattern = "lobby:*"

func ClearLobbyNamespace(ctx context.Context, client *Client) error {
	if client == nil || client.Client == nil {
		return errors.New("redis client is not configured")
	}

	iter := client.Scan(ctx, 0, lobbyNamespacePattern, 0).Iterator()
	keys := make([]string, 0, 16)
	for iter.Next(ctx) {
		keys = append(keys, iter.Val())
	}
	if err := iter.Err(); err != nil {
		return fmt.Errorf("scan lobby namespace: %w", err)
	}
	if len(keys) == 0 {
		return nil
	}
	if err := client.Del(ctx, keys...).Err(); err != nil {
		return fmt.Errorf("delete lobby namespace: %w", err)
	}
	return nil
}
