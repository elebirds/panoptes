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
