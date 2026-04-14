// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 定义认证模块的存储接口。

package auth

import (
	"context"

	"github.com/elebirds/panoptes/internal/gen/sqlc"
	"github.com/google/uuid"
)

// userStoreImpl handles user persistence operations using sqlc
type userStoreImpl struct {
	q *sqlc.Queries
}

// NewUserStore creates a new UserStore
func NewUserStore(queries *sqlc.Queries) UserStore {
	return &userStoreImpl{q: queries}
}

func (s *userStoreImpl) Create(ctx context.Context, user *User) error {
	params := sqlc.CreateUserParams{
		Username:     user.Username,
		PasswordHash: user.PasswordHash,
	}
	createdUser, err := s.q.CreateUser(ctx, params)
	if err != nil {
		return err
	}
	user.ID = createdUser.ID.String()
	user.CreatedAt = createdUser.CreatedAt
	user.UpdatedAt = createdUser.UpdatedAt
	return nil
}

func (s *userStoreImpl) GetByUsername(ctx context.Context, username string) (*User, error) {
	user, err := s.q.GetUserByUsername(ctx, username)
	if err != nil {
		return nil, err
	}
	return &User{
		ID:           user.ID.String(),
		Username:     user.Username,
		PasswordHash: user.PasswordHash,
		CreatedAt:    user.CreatedAt,
		UpdatedAt:    user.UpdatedAt,
	}, nil
}

func (s *userStoreImpl) GetByID(ctx context.Context, id string) (*User, error) {
	uid, err := uuid.Parse(id)
	if err != nil {
		return nil, err
	}

	user, err := s.q.GetUserByID(ctx, uid)
	if err != nil {
		return nil, err
	}

	return &User{
		ID:           user.ID.String(),
		Username:     user.Username,
		PasswordHash: user.PasswordHash,
		CreatedAt:    user.CreatedAt,
		UpdatedAt:    user.UpdatedAt,
	}, nil
}
