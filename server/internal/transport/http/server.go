// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现HTTP 传输层的服务启动与生命周期管理。

package http

import (
	"context"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
)

type Pinger interface {
	Ping(ctx context.Context) error
}

type Server struct {
	mux          *http.ServeMux
	authHandler  *AuthHandler
	debugHandler *DebugHandler
	jwtSecret    string
	dbPinger     Pinger
	redisPinger  Pinger
	devMode      bool
}

func NewServer(
	authSvc *auth.Service,
	jwtSecret string,
	dbPinger Pinger,
	redisPinger Pinger,
	devMode bool,
	debugHandler *DebugHandler,
) *Server {
	mux := http.NewServeMux()
	s := &Server{
		mux:          mux,
		authHandler:  NewAuthHandler(authSvc, jwtSecret),
		debugHandler: debugHandler,
		jwtSecret:    jwtSecret,
		dbPinger:     dbPinger,
		redisPinger:  redisPinger,
		devMode:      devMode,
	}

	s.registerRoutes()
	s.registerHealthRoutes()

	return s
}

func (s *Server) registerRoutes() {
	// Public routes
	s.mux.HandleFunc("POST /api/register", s.authHandler.Register)
	s.mux.HandleFunc("POST /api/login", s.authHandler.Login)

	// Protected routes
	s.mux.HandleFunc("GET /api/users/{username}", AuthMiddleware(s.jwtSecret, s.authHandler.GetUser))

	if s.devMode && s.debugHandler != nil {
		s.mux.HandleFunc("GET /api/dev/game/state", AuthMiddleware(s.jwtSecret, s.debugHandler.GetState))
		s.mux.HandleFunc("GET /api/dev/game/sync", AuthMiddleware(s.jwtSecret, s.debugHandler.GetGameSync))
		s.mux.HandleFunc("POST /api/dev/game/command", AuthMiddleware(s.jwtSecret, s.debugHandler.Command))
		s.mux.HandleFunc("POST /api/dev/game/vision", AuthMiddleware(s.jwtSecret, s.debugHandler.Vision))
		s.mux.HandleFunc("POST /api/dev/game/submit", AuthMiddleware(s.jwtSecret, s.debugHandler.Submit))
		s.mux.HandleFunc("POST /api/dev/game/step-turn", AuthMiddleware(s.jwtSecret, s.debugHandler.StepTurn))
	}
}

func (s *Server) registerHealthRoutes() {
	s.mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) {
		status := map[string]any{
			"status": "ok",
			"postgres": map[string]any{
				"connected": s.dbPinger != nil,
			},
			"redis": map[string]any{
				"connected": s.redisPinger != nil,
			},
		}

		if s.dbPinger != nil {
			if err := s.dbPinger.Ping(r.Context()); err != nil {
				status["postgres"] = map[string]any{
					"connected": false,
					"error":     err.Error(),
				}
				status["status"] = "degraded"
			}
		}

		if s.redisPinger != nil {
			if err := s.redisPinger.Ping(r.Context()); err != nil {
				status["redis"] = map[string]any{
					"connected": false,
					"error":     err.Error(),
				}
				status["status"] = "degraded"
			}
		}

		code := http.StatusOK
		if status["status"] == "degraded" {
			code = http.StatusServiceUnavailable
		}
		writeJSON(w, code, status)
	})
}

func (s *Server) Handler() http.Handler {
	return s.mux
}
