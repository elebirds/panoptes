package codec

import (
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func TestEncodeDecodeClientFrameRoundTrip(t *testing.T) {
	frame := &pb.ClientFrame{
		Meta: &pb.CommandMeta{
			RequestId: "req-1",
			TraceId:   "trace-1",
		},
		Target: &pb.ClientFrame_Lobby{
			Lobby: &pb.LobbyCommand{
				Body: &pb.LobbyCommand_CreateRoom{
					CreateRoom: &pb.MsgCreateRoom{
						Name:       "room-a",
						MaxPlayers: 4,
					},
				},
			},
		},
	}

	raw, err := EncodeClientFrame(frame)
	if err != nil {
		t.Fatalf("EncodeClientFrame() error = %v", err)
	}

	decoded, err := DecodeClientFrame(raw)
	if err != nil {
		t.Fatalf("DecodeClientFrame() error = %v", err)
	}

	if decoded.GetMeta().GetRequestId() != "req-1" {
		t.Fatalf("request_id = %q", decoded.GetMeta().GetRequestId())
	}

	createRoom := decoded.GetLobby().GetCreateRoom()
	if createRoom == nil {
		t.Fatalf("lobby.create_room = nil")
	}
	if createRoom.GetName() != "room-a" {
		t.Fatalf("create_room.name = %q", createRoom.GetName())
	}
}

func TestEncodeDecodeClientFrameRoundTripForGameChat(t *testing.T) {
	frame := &pb.ClientFrame{
		Meta: &pb.CommandMeta{
			RequestId: "req-chat-1",
			TraceId:   "trace-chat-1",
		},
		Target: &pb.ClientFrame_Game{
			Game: &pb.GameCommand{
				Body: &pb.GameCommand_Chat{
					Chat: &pb.ChatCommand{
						Body: &pb.ChatCommand_SendGameChat{
							SendGameChat: &pb.MsgSendGameChat{
								Payload: &pb.ChatPayload{
									Body: &pb.ChatPayload_Emote{
										Emote: pb.ChatEmote_CHAT_EMOTE_THUMBS_UP,
									},
								},
							},
						},
					},
				},
			},
		},
	}

	raw, err := EncodeClientFrame(frame)
	if err != nil {
		t.Fatalf("EncodeClientFrame() error = %v", err)
	}

	decoded, err := DecodeClientFrame(raw)
	if err != nil {
		t.Fatalf("DecodeClientFrame() error = %v", err)
	}

	send := decoded.GetGame().GetChat().GetSendGameChat()
	if send == nil {
		t.Fatalf("game.chat.send_game_chat = nil")
	}
	if send.GetPayload().GetEmote() != pb.ChatEmote_CHAT_EMOTE_THUMBS_UP {
		t.Fatalf("payload.emote = %v, want %v", send.GetPayload().GetEmote(), pb.ChatEmote_CHAT_EMOTE_THUMBS_UP)
	}
}

func TestDecodeClientFrameRejectsMissingRequestID(t *testing.T) {
	raw := []byte(`{"lobby":{"createRoom":{"name":"room-a","maxPlayers":4}}}`)

	_, err := DecodeClientFrame(raw)
	if err == nil {
		t.Fatalf("DecodeClientFrame() error = nil")
	}

	problem, ok := AsProblem(err)
	if !ok {
		t.Fatalf("AsProblem() ok = false, err = %v", err)
	}
	if problem.GetCode() != "invalid_request" {
		t.Fatalf("problem.code = %q", problem.GetCode())
	}
}

func TestWrapServerMessageForGameChatPosted(t *testing.T) {
	frame, err := WrapServerMessage(&pb.MsgGameChatPosted{
		Entry: &pb.ChatEntry{
			Sequence:       7,
			SenderPlayerId: "player-1",
			Turn:           3,
			Phase:          "planning",
			Payload: &pb.ChatPayload{
				Body: &pb.ChatPayload_Emote{
					Emote: pb.ChatEmote_CHAT_EMOTE_LAUGH,
				},
			},
		},
	}, &pb.EventMeta{RequestId: "req-chat-posted"})
	if err != nil {
		t.Fatalf("WrapServerMessage() error = %v", err)
	}

	posted := frame.GetGame().GetGameChatPosted()
	if posted == nil {
		t.Fatalf("game.game_chat_posted = nil")
	}
	if posted.GetEntry().GetSequence() != 7 {
		t.Fatalf("entry.sequence = %d, want 7", posted.GetEntry().GetSequence())
	}
}

func TestWrapServerMessageForGameChatSync(t *testing.T) {
	frame, err := WrapServerMessage(&pb.MsgGameChatSync{
		Entries: []*pb.ChatEntry{
			{
				Sequence:       1,
				SenderPlayerId: "player-1",
				Turn:           1,
				Phase:          "planning",
				Payload: &pb.ChatPayload{
					Body: &pb.ChatPayload_Emote{
						Emote: pb.ChatEmote_CHAT_EMOTE_GG,
					},
				},
			},
		},
	}, &pb.EventMeta{RequestId: "req-chat-sync"})
	if err != nil {
		t.Fatalf("WrapServerMessage() error = %v", err)
	}

	sync := frame.GetGame().GetGameChatSync()
	if sync == nil {
		t.Fatalf("game.game_chat_sync = nil")
	}
	if len(sync.GetEntries()) != 1 {
		t.Fatalf("entries len = %d, want 1", len(sync.GetEntries()))
	}
}

func TestEncodeDecodeServerFrameRoundTrip(t *testing.T) {
	frame := &pb.ServerFrame{
		Meta: &pb.EventMeta{
			RequestId:        "req-2",
			TraceId:          "trace-2",
			ServerUnixMillis: 1234,
			GameSessionId:    "game-1",
		},
		Target: &pb.ServerFrame_Lobby{
			Lobby: &pb.LobbyEvent{
				Body: &pb.LobbyEvent_RoomCreated{
					RoomCreated: &pb.MsgRoomCreated{
						RoomId:   "room-1",
						RoomCode: "ABC123",
					},
				},
			},
		},
	}

	raw, err := EncodeServerFrame(frame)
	if err != nil {
		t.Fatalf("EncodeServerFrame() error = %v", err)
	}

	decoded, err := DecodeServerFrame(raw)
	if err != nil {
		t.Fatalf("DecodeServerFrame() error = %v", err)
	}

	if decoded.GetMeta().GetRequestId() != "req-2" {
		t.Fatalf("request_id = %q", decoded.GetMeta().GetRequestId())
	}
	if decoded.GetMeta().GetGameSessionId() != "game-1" {
		t.Fatalf("game_session_id = %q", decoded.GetMeta().GetGameSessionId())
	}
	if decoded.GetLobby().GetRoomCreated().GetRoomCode() != "ABC123" {
		t.Fatalf("room_code = %q", decoded.GetLobby().GetRoomCreated().GetRoomCode())
	}
}
