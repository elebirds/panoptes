package app

import (
	"context"
	"log/slog"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/game"
	"github.com/elebirds/panoptes/internal/lobby"
	redistore "github.com/elebirds/panoptes/internal/store/redis"
	httptransport "github.com/elebirds/panoptes/internal/transport/http"
	wstransport "github.com/elebirds/panoptes/internal/transport/websocket"
	"github.com/google/uuid"
)

func (a *App) buildServer() *http.Server {
	userStore := auth.NewUserStore(a.infra.pgDB.Queries())
	authSvc := auth.NewService(userStore, a.cfg.JWTSecret, a.cfg.JWTExpiration)

	wsHub := wstransport.NewHub(a.cfg.JWTSecret)
	a.gameTransport = wstransport.NewTransport(wsHub)

	lobbyStore := redistore.NewLobbyStore(a.infra.redisClient)
	lobbySvc := lobby.NewService(lobbyStore, a.gameTransport, authSvc, a.cfg.DefaultMaxPlayers, a.cfg.DevMode)
	lobbySvc.SetGameStartCallback(a.onGameStart)

	router := wstransport.NewRouter(lobbySvc, game.Registry)
	wsHub.SetRouter(router)
	wsHub.SetLeaveRoomFunc(lobbySvc.LeaveRoom)
	go wsHub.Run(context.Background())

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

func (a *App) onGameStart(lobbyRoom *lobby.Room) {
	if lobbyRoom == nil {
		return
	}

	var players []game.Player
	for _, player := range lobbyRoom.Players {
		if player.IsBot {
			players = append(players, game.NewBotPlayer(
				player.PlayerID,
				player.Username,
				&game.RandomStrategy{},
			))
			continue
		}

		players = append(players, game.NewHumanPlayer(
			player.PlayerID,
			player.Username,
			a.gameTransport,
		))
	}

	room := game.NewRoom(
		uuid.NewString(),
		players,
		a.gameTransport,
		a.cfg,
	)

	slog.Info("游戏开始", "room_id", lobbyRoom.ID, "players", len(lobbyRoom.Players), "game_room_id", room.ID)
	go room.Start()
}
