package redis

import (
	"context"

	"github.com/redis/go-redis/v9"
)

type Client struct {
	*redis.Client
}

// NewClient creates a new Redis client
func NewClient(addr, password string, db int) *Client {
	rdb := redis.NewClient(&redis.Options{
		Addr:     addr,
		Password: password,
		DB:       db,
	})
	return &Client{Client: rdb}
}

// Ping checks if the Redis server is responsive
func (c *Client) Ping(ctx context.Context) error {
	return c.Client.Ping(ctx).Err()
}

// Close closes the Redis connection
func (c *Client) Close() error {
	return c.Client.Close()
}
