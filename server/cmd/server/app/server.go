package app

import (
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
	httptransport "github.com/elebirds/panoptes/internal/transport/http"
	wstransport "github.com/elebirds/panoptes/internal/transport/websocket"
)

func (a *App) buildServer() *http.Server {
	userStore := auth.NewUserStore(a.infra.pgDB.Queries())
	authSvc := auth.NewService(userStore, a.cfg.JWTSecret, a.cfg.JWTExpiration)

	wsHub := wstransport.NewHub(a.cfg.JWTSecret)
	a.gameTransport = wstransport.NewTransport(wsHub)
	go wsHub.Run()

	httpServer := httptransport.NewServer(
		authSvc,
		a.cfg.JWTSecret,
		a.infra.pgDB,
		a.infra.redisClient,
	)

	mux := http.NewServeMux()
	mux.Handle("/api/", httpServer.Handler())
	mux.HandleFunc("/ws", wsHub.ServeWS)

	return &http.Server{
		Addr:    ":" + a.cfg.Port,
		Handler: mux,
	}
}
