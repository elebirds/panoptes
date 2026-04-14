// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现HTTP 传输层的中间件逻辑。

package http

import (
	"context"
	"net/http"

	coretransport "github.com/elebirds/panoptes/internal/transport"
)

type contextKey string

const PlayerIDKey contextKey = "player_id"

func AuthMiddleware(jwtSecret string, next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		tokenString, err := coretransport.ParseBearerToken(r.Header.Get("Authorization"))
		if err != nil {
			writeError(w, http.StatusUnauthorized, "unauthorized")
			return
		}

		playerID, err := coretransport.ParseAndValidateJWT(jwtSecret, tokenString)
		if err != nil {
			writeError(w, http.StatusUnauthorized, "unauthorized")
			return
		}

		ctx := context.WithValue(r.Context(), PlayerIDKey, playerID)
		next(w, r.WithContext(ctx))
	}
}
