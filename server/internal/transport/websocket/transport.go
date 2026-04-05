package websocket

import (
	"errors"

	pb "github.com/elebirds/panoptes/internal/gen/proto"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

var _ coretransport.GameTransport = (*WSTransport)(nil)

// WSTransport adapts GameTransport to websocket clients managed by Hub.
type WSTransport struct {
	hub *Hub
}

func NewTransport(hub *Hub) *WSTransport {
	return &WSTransport{hub: hub}
}

func (t *WSTransport) Send(playerID string, msg proto.Message) error {
	if playerID == "" {
		return errors.New("playerID is required")
	}

	data, err := marshalEnvelope(msg)
	if err != nil {
		return err
	}

	return t.hub.SendToPlayer(playerID, data)
}

func (t *WSTransport) Broadcast(roomID string, msg proto.Message) error {
	if roomID == "" {
		return errors.New("roomID is required")
	}

	data, err := marshalEnvelope(msg)
	if err != nil {
		return err
	}

	t.hub.BroadcastToRoom(roomID, data)
	return nil
}

func (t *WSTransport) Stream(playerID string, msgs <-chan proto.Message) error {
	for msg := range msgs {
		if err := t.Send(playerID, msg); err != nil {
			return err
		}
	}
	return nil
}

func marshalEnvelope(msg proto.Message) ([]byte, error) {
	if msg == nil {
		return nil, errors.New("message is nil")
	}

	payload, err := protojson.Marshal(msg)
	if err != nil {
		return nil, err
	}

	fullName := proto.MessageName(msg)
	msgType := string(fullName.Name())
	if msgType == "" {
		msgType = string(fullName)
	}

	envelope := &pb.Envelope{
		Type:    msgType,
		Payload: string(payload),
	}

	return protojson.Marshal(envelope)
}
