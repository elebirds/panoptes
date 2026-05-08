package chat

import (
	"context"
	"errors"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"google.golang.org/protobuf/proto"
)

type Session interface {
	State() *domain.GameState
	Broadcast(ctx context.Context, msg proto.Message)
	NextChatSequence() int64
}

type Service struct{}

func (s *Service) HandleCommand(room Session, inbound cmddispatch.InboundContext, cmd *pb.ChatCommand) error {
	if room == nil {
		return errors.New("chat session is nil")
	}
	if cmd == nil || cmd.GetBody() == nil {
		return transportproblem.InvalidRequest("chat command is nil")
	}

	switch body := cmd.GetBody().(type) {
	case *pb.ChatCommand_SendGameChat:
		return s.handleSendGameChat(room, inbound, body.SendGameChat)
	default:
		return transportproblem.InvalidRequest("unsupported chat command")
	}
}

func (s *Service) handleSendGameChat(room Session, inbound cmddispatch.InboundContext, msg *pb.MsgSendGameChat) error {
	payload := msg.GetPayload()
	if payload == nil || payload.GetBody() == nil {
		return transportproblem.InvalidRequest("chat payload is required")
	}
	if !isSupportedRealtimePayload(payload) {
		return transportproblem.InvalidRequest("unsupported chat payload")
	}

	state := room.State()
	if state == nil {
		return errors.New("state is nil")
	}

	entry := &pb.ChatEntry{
		Sequence:       room.NextChatSequence(),
		SenderPlayerId: inbound.PlayerID,
		Turn:           int32(state.Turn),
		Phase:          state.Phase,
		Payload:        proto.Clone(payload).(*pb.ChatPayload),
	}
	eventCtx := coretransport.ContextWithEventMeta(context.Background(), coretransport.EventMetaFromInbound(inbound))
	room.Broadcast(eventCtx, &pb.MsgGameChatPosted{Entry: entry})
	return nil
}

func BuildChatSync(entries []*pb.ChatEntry) *pb.MsgGameChatSync {
	cloned := make([]*pb.ChatEntry, 0, len(entries))
	for _, entry := range entries {
		if entry == nil {
			continue
		}
		cloned = append(cloned, proto.Clone(entry).(*pb.ChatEntry))
	}
	return &pb.MsgGameChatSync{Entries: cloned}
}

func isSupportedRealtimePayload(payload *pb.ChatPayload) bool {
	if payload == nil || payload.GetBody() == nil {
		return false
	}
	switch body := payload.GetBody().(type) {
	case *pb.ChatPayload_EmoteId:
		emoteID := strings.TrimSpace(body.EmoteId)
		if emoteID == "" {
			return false
		}
		catalog := staticdata.Default()
		if catalog == nil {
			return false
		}
		_, ok := catalog.GetEmote(emoteID)
		return ok
	case *pb.ChatPayload_Emote:
		switch body.Emote {
		case pb.ChatEmote_CHAT_EMOTE_THUMBS_UP,
			pb.ChatEmote_CHAT_EMOTE_THINKING,
			pb.ChatEmote_CHAT_EMOTE_LAUGH,
			pb.ChatEmote_CHAT_EMOTE_ANGRY,
			pb.ChatEmote_CHAT_EMOTE_WARNING,
			pb.ChatEmote_CHAT_EMOTE_GG:
			return true
		default:
			return false
		}
	default:
		return false
	}
}
