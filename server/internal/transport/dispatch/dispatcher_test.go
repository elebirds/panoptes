package dispatch

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

type stubLobbyHandler struct {
	createRoom *pb.MsgCreateRoom
}

func (h *stubLobbyHandler) CreateRoom(_ InboundContext, cmd *pb.MsgCreateRoom) error {
	h.createRoom = cmd
	return nil
}

func (h *stubLobbyHandler) JoinRoom(InboundContext, *pb.MsgJoinRoom) error     { return nil }
func (h *stubLobbyHandler) LeaveRoom(InboundContext, *pb.MsgLeaveRoom) error   { return nil }
func (h *stubLobbyHandler) ReadyUp(InboundContext, *pb.MsgReadyUp) error       { return nil }
func (h *stubLobbyHandler) AddBot(InboundContext, *pb.MsgAddBot) error         { return nil }
func (h *stubLobbyHandler) StartGame(InboundContext, *pb.MsgStartGame) error   { return nil }
func (h *stubLobbyHandler) KickPlayer(InboundContext, *pb.MsgKickPlayer) error { return nil }

type stubGameHandler struct {
	planning *pb.PlanningCommand
	syncReq  *pb.MsgStaticCatalogSyncRequest
	chat     *pb.ChatCommand
}

func (h *stubGameHandler) Planning(_ InboundContext, cmd *pb.PlanningCommand) error {
	h.planning = cmd
	return nil
}

func (h *stubGameHandler) StaticCatalogSyncRequest(_ InboundContext, cmd *pb.MsgStaticCatalogSyncRequest) error {
	h.syncReq = cmd
	return nil
}

func (h *stubGameHandler) Chat(_ InboundContext, cmd *pb.ChatCommand) error {
	h.chat = cmd
	return nil
}

func TestDispatcherRoutesLobbyCreateRoom(t *testing.T) {
	lobbyHandler := &stubLobbyHandler{}
	dispatcher := Dispatcher{
		Lobby: lobbyHandler,
	}

	err := dispatcher.Dispatch(InboundContext{PlayerID: "host-1"}, &pb.ClientFrame{
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
	if lobbyHandler.createRoom == nil {
		t.Fatalf("lobby create room handler not called")
	}
	if lobbyHandler.createRoom.GetName() != "alpha" {
		t.Fatalf("create_room.name = %q", lobbyHandler.createRoom.GetName())
	}
}

func TestDispatcherRoutesPlanningSetPolicy(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{
		Game: gameHandler,
	}

	err := dispatcher.Dispatch(InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-2"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_Planning{
					Planning: &pb.PlanningCommand{
						Body: &pb.PlanningCommand_SetPolicy{
							SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "expansion"},
						},
					},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if gameHandler.planning == nil {
		t.Fatalf("game planning handler not called")
	}
	body, ok := gameHandler.planning.Body.(*pb.PlanningCommand_SetPolicy)
	if !ok {
		t.Fatalf("planning body type = %T", gameHandler.planning.Body)
	}
	if body.SetPolicy.GetNationalPolicyId() != "expansion" {
		t.Fatalf("set_policy.national_policy_id = %q", body.SetPolicy.GetNationalPolicyId())
	}
}

func TestDispatcherRoutesStaticCatalogSyncRequest(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{
		Game: gameHandler,
	}

	err := dispatcher.Dispatch(InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-2b"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_StaticCatalogSyncRequest{
					StaticCatalogSyncRequest: &pb.MsgStaticCatalogSyncRequest{
						BundleHash: "bundle-1",
					},
				},
			},
		},
	})
	if err != nil {
		t.Fatalf("Dispatch() error = %v", err)
	}
	if gameHandler.syncReq == nil {
		t.Fatalf("game static catalog sync handler not called")
	}
	if gameHandler.syncReq.GetBundleHash() != "bundle-1" {
		t.Fatalf("static_catalog_sync_request.bundle_hash = %q", gameHandler.syncReq.GetBundleHash())
	}
}

func TestDispatcherRoutesGameChat(t *testing.T) {
	gameHandler := &stubGameHandler{}
	dispatcher := Dispatcher{
		Game: gameHandler,
	}

	err := dispatcher.Dispatch(InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-chat-3"},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_Chat{
					Chat: &pb.ChatCommand{
						Body: &pb.ChatCommand_SendGameChat{
							SendGameChat: &pb.MsgSendGameChat{
								Payload: &pb.ChatPayload{
									Body: &pb.ChatPayload_Emote{
										Emote: pb.ChatEmote_CHAT_EMOTE_ANGRY,
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
	if gameHandler.chat == nil {
		t.Fatalf("game chat handler not called")
	}
	if gameHandler.chat.GetSendGameChat() == nil {
		t.Fatalf("chat.send_game_chat = nil")
	}
}

func TestDispatcherRejectsEmptyTarget(t *testing.T) {
	dispatcher := Dispatcher{}

	err := dispatcher.Dispatch(InboundContext{PlayerID: "player-1"}, &pb.ClientFrame{
		Meta: &pb.CommandMeta{RequestId: "req-3"},
	})
	if err == nil {
		t.Fatalf("Dispatch() error = nil")
	}

	problem, ok := AsProblem(err)
	if !ok {
		t.Fatalf("AsProblem() ok = false, err = %v", err)
	}
	if problem.GetCode() != "invalid_request" {
		t.Fatalf("problem.code = %q", problem.GetCode())
	}
}
