// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现PostgreSQL 存储层的连接初始化与装配。

package postgres

import (
	"context"
	"fmt"

	"github.com/elebirds/panoptes/internal/db"
	"github.com/elebirds/panoptes/internal/gen/sqlc"
	"github.com/jackc/pgx/v5/pgxpool"
)

type DB struct {
	pool *pgxpool.Pool
	tx   *db.TxRunner
	q    *sqlc.Queries
}

// NewDB creates a new PostgreSQL connection pool
func NewDB(ctx context.Context, dsn string) (*DB, error) {
	pool, err := db.NewPool(ctx, dsn)
	if err != nil {
		return nil, fmt.Errorf("create connection pool: %w", err)
	}

	if err := pool.Ping(ctx); err != nil {
		pool.Close()
		return nil, fmt.Errorf("ping database: %w", err)
	}

	return &DB{
		pool: pool,
		tx:   db.NewTxRunner(pool),
		q:    sqlc.New(pool),
	}, nil
}

// Close closes the connection pool
func (db *DB) Close() {
	db.pool.Close()
}

// Pool returns the underlying pgxpool.Pool
func (db *DB) Pool() *pgxpool.Pool {
	return db.pool
}

// TxRunner returns the transaction runner
func (db *DB) TxRunner() *db.TxRunner {
	return db.tx
}

// Queries returns the sqlc queries
func (db *DB) Queries() *sqlc.Queries {
	return db.q
}

// Ping checks if the database is responsive
func (db *DB) Ping(ctx context.Context) error {
	return db.pool.Ping(ctx)
}
