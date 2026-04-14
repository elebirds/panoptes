package websocket

import (
	"context"
	"testing"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/transport/codec"
)

func TestTransportSendWrapsLobbyEventInServerFrame(t *testing.T) {
	hub := NewHub("secret")
	client := &Client{
		playerID: "host-1",
		send:     make(chan []byte, 1),
	}
	hub.clients["host-1"] = client

	transport := NewTransport(hub)
	if err := transport.Send(context.Background(), "host-1", &pb.MsgRoomCreated{
		RoomId:   "room-1",
		RoomCode: "ABCD12",
	}); err != nil {
		t.Fatalf("Send() error = %v", err)
	}

	raw := <-client.send
	frame, err := codec.DecodeServerFrame(raw)
	if err != nil {
		t.Fatalf("DecodeServerFrame() error = %v", err)
	}

	if frame.GetLobby().GetRoomCreated().GetRoomCode() != "ABCD12" {
		t.Fatalf("room_code = %q", frame.GetLobby().GetRoomCreated().GetRoomCode())
	}
	if frame.GetMeta().GetServerUnixMillis() == 0 {
		t.Fatalf("server_unix_millis = 0, want non-zero")
	}
}
