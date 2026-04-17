package inbound

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

type stubLobbyHandler struct {
	command *pb.LobbyCommand
}

func (h *stubLobbyHandler) HandleLobbyCommand(_ cmddispatch.InboundContext, cmd *pb.LobbyCommand) error {
	h.command = cmd
	return nil
}

type stubGameHandler struct {
	command *pb.GameCommand
}

func (h *stubGameHandler) HandleGameCommand(_ cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	h.command = cmd
	return nil
}

func TestDispatcherRoutesLobbyTarget(t *testing.T) {
	lobbyHandler := &stubLobbyHandler{}
	dispatcher := Dispatcher{Lobby: lobbyHandler}

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
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if lobbyHandler.command == nil {
		t.Fatalf("lobby handler not called")
	}
	if lobbyHandler.command.GetCreateRoom().GetName() != "alpha" {
		t.Fatalf("create_room.name = %q", lobbyHandler.command.GetCreateRoom().GetName())
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
