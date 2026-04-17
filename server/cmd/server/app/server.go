// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现服务端应用装配的服务启动与生命周期管理。

package app

import (
	"context"
	"log/slog"
	"net/http"

	"github.com/elebirds/panoptes/internal/auth"
	"github.com/elebirds/panoptes/internal/debug"
	"github.com/elebirds/panoptes/internal/game"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/lobby"
	redistore "github.com/elebirds/panoptes/internal/store/redis"
	httptransport "github.com/elebirds/panoptes/internal/transport/http"
	"github.com/elebirds/panoptes/internal/transport/inbound"
	wstransport "github.com/elebirds/panoptes/internal/transport/websocket"
	"github.com/google/uuid"
)

func (a *App) buildServer() *http.Server {
	userStore := auth.NewUserStore(a.infra.pgDB.Queries())
	authSvc := auth.NewService(userStore, a.cfg.JWTSecret, a.cfg.JWTExpiration)

	wsHub := wstransport.NewHub(a.cfg.JWTSecret)
	wsHub.SetMessageLogger(debug.NewMessageLogger(a.cfg.DevMode))
	a.gameTransport = wstransport.NewTransport(wsHub)

	lobbyStore := redistore.NewLobbyStore(a.infra.redisClient)
	lobbySvc := lobby.NewService(lobbyStore, a.gameTransport, authSvc, a.cfg.DefaultMaxPlayers, a.cfg.DevMode)
	lobbySvc.SetGameStartCallback(a.onGameStart)

	wsHub.SetDispatcher(&inbound.Dispatcher{
		Lobby: lobby.NewCommandHandler(lobbySvc),
		Game:  game.NewRegistryCommandHandler(game.Registry),
	})
	wsHub.SetLeaveRoomFunc(func(ctx context.Context, playerID string) error {
		if err := lobbySvc.HandleDisconnect(ctx, playerID); err != nil {
			return err
		}
		game.Registry.HandleParticipantDisconnect(playerID)
		return nil
	})
	wsHub.SetConnectFunc(func(ctx context.Context, playerID string) error {
		return a.gameTransport.Send(ctx, playerID, &pb.MsgClientRuntimeConfig{
			DevMode: a.cfg.DevMode,
		})
	})
	go wsHub.Run(context.Background())

	var settlementRecorder *debug.SettlementRecorder
	var commandRecorder *debug.CommandResultRecorder
	if a.cfg.DevMode {
		settlementRecorder = debug.NewSettlementRecorder()
		commandRecorder = debug.NewCommandResultRecorder()
		game.SetDebugHooks(game.DebugHooks{
			DumpStateSummary:      debug.DumpGameStateSummary,
			RecordSettlement:      settlementRecorder.RecordSettlement,
			RecordGameOver:        settlementRecorder.RecordGameOver,
			RecordOutgoingMessage: commandRecorder.RecordOutgoingMessage,
		})
	} else {
		game.SetDebugHooks(game.DebugHooks{})
	}

	httpServer := httptransport.NewServer(
		authSvc,
		a.cfg.JWTSecret,
		a.infra.pgDB,
		a.infra.redisClient,
		a.cfg.DevMode,
		httptransport.NewDebugHandler(game.Registry, settlementRecorder, commandRecorder),
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

	var participants []game.ParticipantSpec
	for _, player := range lobbyRoom.Players {
		if player.IsBot {
			participants = append(participants, game.NewBotParticipantSpec(player.PlayerID, player.Username))
			continue
		}

		participants = append(participants, game.NewHumanParticipantSpec(player.PlayerID, player.Username))
	}

	room := game.NewRoom(
		uuid.NewString(),
		participants,
		a.gameTransport,
		a.cfg,
	)
	if a.newMinisterEngine != nil {
		room.SetMinisterEngine(a.newMinisterEngine())
	}

	slog.Info("游戏开始", "room_id", lobbyRoom.ID, "players", len(lobbyRoom.Players), "game_room_id", room.ID)
	go room.Start()
}
