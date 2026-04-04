package db

import (
	"context"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"
)

// TxRunner manages database transactions
type TxRunner struct {
	Pool *pgxpool.Pool
}

// NewTxRunner creates a new transaction runner
func NewTxRunner(pool *pgxpool.Pool) *TxRunner {
	return &TxRunner{Pool: pool}
}

// InTx executes a function within a transaction
func (r *TxRunner) InTx(ctx context.Context, fn func(pgx.Tx) error) error {
	tx, err := r.Pool.BeginTx(ctx, pgx.TxOptions{})
	if err != nil {
		return err
	}
	defer tx.Rollback(ctx)

	if err := fn(tx); err != nil {
		return err
	}

	return tx.Commit(ctx)
}
