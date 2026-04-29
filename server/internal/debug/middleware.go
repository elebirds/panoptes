// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现调试支持模块的中间件逻辑。

package debug

import (
	"log/slog"
	"sync"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

type playerSnapshot struct {
	turn  int32
	phase string
}

// MessageLogger logs websocket ingress/egress messages in DEV_MODE.
type MessageLogger struct {
	enabled bool

	mu       sync.RWMutex
	players  map[string]playerSnapshot
	jsonOpts protojson.UnmarshalOptions
}

func NewMessageLogger(enabled bool) *MessageLogger {
	return &MessageLogger{
		enabled:  enabled,
		players:  make(map[string]playerSnapshot),
		jsonOpts: protojson.UnmarshalOptions{DiscardUnknown: true},
	}
}

func (l *MessageLogger) LogOutgoing(playerID string, raw []byte) {
	if l == nil || !l.enabled {
		return
	}

	frame := &pb.ServerFrame{}
	if err := l.jsonOpts.Unmarshal(raw, frame); err != nil {
		slog.Debug("→ 推送消息",
			"player_id", playerID,
			"type", "unknown",
			"turn", int32(0),
			"phase", "",
			"payload_size", len(raw),
		)
		return
	}

	msgType, payload, ok := outgoingMessage(frame)
	if !ok {
		slog.Debug("→ 推送消息",
			"player_id", playerID,
			"type", "unknown",
			"turn", int32(0),
			"phase", "",
			"payload_size", len(raw),
		)
		return
	}

	turn, phase := l.resolveOutgoingSnapshot(playerID, payload)
	payloadSize := len(raw)
	if payload != nil {
		if encoded, err := protojson.Marshal(payload); err == nil {
			payloadSize = len(encoded)
		}
	}
	slog.Debug("→ 推送消息",
		"player_id", playerID,
		"type", msgType,
		"turn", turn,
		"phase", phase,
		"payload_size", payloadSize,
	)
}

func (l *MessageLogger) LogIncoming(playerID string, msgType string, payloadJSON string) {
	if l == nil || !l.enabled {
		return
	}

	slog.Debug("← 收到消息",
		"player_id", playerID,
		"type", msgType,
		"raw", payloadJSON,
	)
}

func (l *MessageLogger) resolveOutgoingSnapshot(playerID string, msg proto.Message) (int32, string) {
	prev := l.snapshot(playerID)
	turn := prev.turn
	phase := prev.phase

	switch typed := msg.(type) {
	case *pb.MsgGameInit:
		turn = typed.GetTurn()
		phase = typed.GetPhase()
	case *pb.MsgPlanningStart:
		turn = typed.GetTurn()
		phase = typed.GetPhase()
	case *pb.MsgGameSync:
		turn = typed.GetTurn()
		phase = typed.GetPhase()
	}

	l.storeSnapshot(playerID, playerSnapshot{turn: turn, phase: phase})
	return turn, phase
}

func outgoingMessage(frame *pb.ServerFrame) (string, proto.Message, bool) {
	if frame == nil {
		return "", nil, false
	}

	switch target := frame.Target.(type) {
	case *pb.ServerFrame_Auth:
		if target.Auth == nil || target.Auth.Body == nil {
			return "AuthEvent", nil, false
		}
		switch body := target.Auth.Body.(type) {
		case *pb.AuthEvent_LoginSuccess:
			return "MsgLoginSuccess", body.LoginSuccess, true
		case *pb.AuthEvent_AuthError:
			return "MsgAuthError", body.AuthError, true
		case *pb.AuthEvent_ClientRuntimeConfig:
			return "MsgClientRuntimeConfig", body.ClientRuntimeConfig, true
		}
	case *pb.ServerFrame_Lobby:
		if target.Lobby == nil || target.Lobby.Body == nil {
			return "LobbyEvent", nil, false
		}
		switch body := target.Lobby.Body.(type) {
		case *pb.LobbyEvent_RoomCreated:
			return "MsgRoomCreated", body.RoomCreated, true
		case *pb.LobbyEvent_RoomState:
			return "MsgRoomState", body.RoomState, true
		case *pb.LobbyEvent_GameStarting:
			return "MsgGameStarting", body.GameStarting, true
		case *pb.LobbyEvent_PlayerKicked:
			return "MsgPlayerKicked", body.PlayerKicked, true
		case *pb.LobbyEvent_LobbyError:
			return "MsgLobbyError", body.LobbyError, true
		}
	case *pb.ServerFrame_Game:
		if target.Game == nil || target.Game.Body == nil {
			return "GameEvent", nil, false
		}
		switch body := target.Game.Body.(type) {
		case *pb.GameEvent_StaticCatalogManifest:
			return "MsgStaticCatalogManifest", body.StaticCatalogManifest, true
		case *pb.GameEvent_StaticCatalogSnapshot:
			return "MsgStaticCatalogSnapshot", body.StaticCatalogSnapshot, true
		case *pb.GameEvent_StaticCatalogSectionChunk:
			return "MsgStaticCatalogSectionChunk", body.StaticCatalogSectionChunk, true
		case *pb.GameEvent_StaticCatalogSyncComplete:
			return "MsgStaticCatalogSyncComplete", body.StaticCatalogSyncComplete, true
		case *pb.GameEvent_ConfigBatchJson:
			return "MsgConfigBatchJson", body.ConfigBatchJson, true
		case *pb.GameEvent_GameInit:
			return "MsgGameInit", body.GameInit, true
		case *pb.GameEvent_PlanningStart:
			return "MsgPlanningStart", body.PlanningStart, true
		case *pb.GameEvent_PlanningSnapshot:
			return "MsgPlanningSnapshot", body.PlanningSnapshot, true
		case *pb.GameEvent_PlanningPathPreviewResponse:
			return "MsgPlanningPathPreviewResponse", body.PlanningPathPreviewResponse, true
		case *pb.GameEvent_TokenResult:
			return "MsgTokenResult", body.TokenResult, true
		case *pb.GameEvent_RevealResult:
			return "MsgRevealResult", body.RevealResult, true
		case *pb.GameEvent_ResearchResult:
			return "MsgResearchResult", body.ResearchResult, true
		case *pb.GameEvent_SetPolicyResult:
			return "MsgSetPolicyResult", body.SetPolicyResult, true
		case *pb.GameEvent_SetInstitutionLoadoutResult:
			return "MsgSetInstitutionLoadoutResult", body.SetInstitutionLoadoutResult, true
		case *pb.GameEvent_IssueUnitOrderResult:
			return "MsgIssueUnitOrderResult", body.IssueUnitOrderResult, true
		case *pb.GameEvent_SetBuildingRecipeResult:
			return "MsgSetBuildingRecipeResult", body.SetBuildingRecipeResult, true
		case *pb.GameEvent_BuildStructureResult:
			return "MsgBuildStructureResult", body.BuildStructureResult, true
		case *pb.GameEvent_TurnReport:
			return "MsgTurnReport", body.TurnReport, true
		case *pb.GameEvent_GameSync:
			return "MsgGameSync", body.GameSync, true
		case *pb.GameEvent_GameOver:
			return "MsgGameOver", body.GameOver, true
		case *pb.GameEvent_MinisterReportChunk:
			return "MsgMinisterReportChunk", body.MinisterReportChunk, true
		case *pb.GameEvent_MinisterMetrics:
			return "MsgMinisterMetrics", body.MinisterMetrics, true
		}
	case *pb.ServerFrame_Problem:
		return "Problem", target.Problem, target.Problem != nil
	}

	return "", nil, false
}

func (l *MessageLogger) snapshot(playerID string) playerSnapshot {
	l.mu.RLock()
	defer l.mu.RUnlock()
	return l.players[playerID]
}

func (l *MessageLogger) storeSnapshot(playerID string, snap playerSnapshot) {
	l.mu.Lock()
	defer l.mu.Unlock()
	l.players[playerID] = snap
}
