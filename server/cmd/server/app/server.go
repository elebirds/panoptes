package app

import (
	"log/slog"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/lobby"
	redistore "github.com/elebirds/panoptes/internal/store/redis"
	httptransport "github.com/elebirds/panoptes/internal/transport/http"
	wstransport "github.com/elebirds/panoptes/internal/transport/websocket"
)

func (a *App) buildServer() *http.Server {
	userStore := auth.NewUserStore(a.infra.pgDB.Queries())
	authSvc := auth.NewService(userStore, a.cfg.JWTSecret, a.cfg.JWTExpiration)

	wsHub := wstransport.NewHub(a.cfg.JWTSecret)
	a.gameTransport = wstransport.NewTransport(wsHub)

	lobbyStore := redistore.NewLobbyStore(a.infra.redisClient)
	lobbySvc := lobby.NewService(lobbyStore, a.gameTransport, authSvc, a.cfg.DefaultMaxPlayers)
	lobbySvc.SetGameStartCallback(func(room *lobby.Room) {
		slog.Info("游戏开始", "room_id", room.ID, "players", len(room.Players))
		// TODO: 创建 Game Room，推送 MsgGameInit
	})

	router := wstransport.NewRouter(lobbySvc)
	wsHub.SetRouter(router)
	wsHub.SetLeaveRoomFunc(lobbySvc.LeaveRoom)
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
