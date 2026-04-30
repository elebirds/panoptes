// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载入站协议帧分发适配逻辑。

package inbound

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

type stubLobbyHandler struct {
	command *pb.LobbyCommand
	ctx     cmddispatch.InboundContext
}

func (h *stubLobbyHandler) HandleLobbyCommand(ctx cmddispatch.InboundContext, cmd *pb.LobbyCommand) error {
	h.command = cmd
	h.ctx = ctx
	return nil
}

type stubGameHandler struct {
	command *pb.GameCommand
	ctx     cmddispatch.InboundContext
}

func (h *stubGameHandler) HandleGameCommand(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	h.command = cmd
	h.ctx = ctx
	return nil
}

func TestDispatcherRoutesLobbyTarget(t *testing.T) {
	lobbyHandler := &stubLobbyHandler{}
	dispatcher := Dispatcher{Lobby: lobbyHandler}

	err := dispatcher.Dispatch(cmddispatch.InboundContext{PlayerID: "host-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-1", TraceId: "trace-1"},
		Target: &pb.ClientFrame_Lobby{
			Lobby: &pb.LobbyCommand{
				Body: &pb.LobbyCommand_CreateRoom{
					CreateRoom: &pb.MsgCreateRoom{Name: "alpha"},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if lobbyHandler.command == nil {
		t.Fatalf("lobby handler not called")
	}
	if lobbyHandler.command.GetCreateRoom().GetName() != "alpha" {
		t.Fatalf("create_room.name = %q", lobbyHandler.command.GetCreateRoom().GetName())
	}
	if lobbyHandler.ctx.RequestID != "req-1" || lobbyHandler.ctx.TraceID != "trace-1" {
		t.Fatalf("ctx meta = (%q, %q), want (req-1, trace-1)", lobbyHandler.ctx.RequestID, lobbyHandler.ctx.TraceID)
	}
}

func TestDispatcherRoutesGameTarget(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{Game: gameHandler}

	err := dispatcher.Dispatch(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-2"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_Planning{
					Planning: &pb.PlanningCommand{
						Body: &pb.PlanningCommand_SubmitTurn{
							SubmitTurn: &pb.MsgSubmitTurn{},
						},
					},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if gameHandler.command == nil {
		t.Fatalf("game handler not called")
	}
	if gameHandler.command.GetPlanning().GetSubmitTurn() == nil {
		t.Fatalf("planning.submit_turn = nil")
	}
}

func TestDispatcherRoutesGameCommandBatchTarget(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{Game: gameHandler}

	err := dispatcher.Dispatch(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-batch-1"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_CommandBatch{
					CommandBatch: &pb.MsgGameCommandBatch{
						Commands: []*pb.CommandEnvelope{
							{
								CommandId: "cmd-1",
								Body: &pb.CommandEnvelope_SubmitTurn{
									SubmitTurn: &pb.MsgSubmitTurn{},
								},
							},
						},
					},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if gameHandler.command == nil {
		t.Fatalf("game handler not called")
	}
	if len(gameHandler.command.GetCommandBatch().GetCommands()) != 1 {
		t.Fatalf("command_batch.commands len = %d", len(gameHandler.command.GetCommandBatch().GetCommands()))
	}
}

func TestDispatcherRejectsMissingHandlerAfterGeneratedBodyValidation(t *testing.T) {
	dispatcher := Dispatcher{}

	err := dispatcher.Dispatch(cmddispatch.InboundContext{PlayerID: "host-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-1"},
		Target: &pb.ClientFrame_Lobby{
			Lobby: &pb.LobbyCommand{
				Body: &pb.LobbyCommand_CreateRoom{
					CreateRoom: &pb.MsgCreateRoom{Name: "alpha"},
				},
			},
		},
	})
	if err == nil {
		t.Fatalf("Dispatch() error = nil")
	}
	problem, ok := cmddispatch.AsProblem(err)
	if !ok {
		t.Fatalf("AsProblem() ok = false, err = %v", err)
	}
	if problem.GetCode() != "internal_error" {
		t.Fatalf("problem.code = %q, want internal_error", problem.GetCode())
	}
}

func TestDispatcherRoutesGameChatTarget(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{Game: gameHandler}

	err := dispatcher.Dispatch(cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-chat-2"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_Chat{
					Chat: &pb.ChatCommand{
						Body: &pb.ChatCommand_SendGameChat{
							SendGameChat: &pb.MsgSendGameChat{
								Payload: &pb.ChatPayload{
									Body: &pb.ChatPayload_Emote{
										Emote: pb.ChatEmote_CHAT_EMOTE_WARNING,
									},
								},
							},
						},
					},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if gameHandler.command == nil {
		t.Fatalf("game handler not called")
	}
	if gameHandler.command.GetChat().GetSendGameChat() == nil {
		t.Fatalf("chat.send_game_chat = nil")
	}
}
