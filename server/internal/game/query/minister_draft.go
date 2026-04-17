package query

import (
	"encoding/json"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func BuildMinisterDraftViews(state *domain.GameState, playerID string) []*pb.MinisterDraftView {
	if state == nil {
		return nil
	}
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return nil
	}
	out := make([]*pb.MinisterDraftView, 0, len(drafts))
	for _, draft := range drafts {
		payload, err := json.Marshal(draft)
		if err != nil {
			continue
		}
		out = append(out, &pb.MinisterDraftView{
			MinisterRole: strings.TrimSpace(draft.MinisterRole),
			JsonPayload:  string(payload),
			Available:    draft.Available,
		})
	}
	return out
}

func BuildMinisterRosterViews() []*pb.MinisterView {
	ministers := staticdata.Default().Ministers()
	if len(ministers) == 0 {
		return nil
	}
	out := make([]*pb.MinisterView, 0, len(ministers))
	for _, minister := range ministers {
		role := strings.TrimSpace(minister.Role)
		if role == "" {
			continue
		}
		out = append(out, &pb.MinisterView{
			Role:        role,
			Name:        strings.TrimSpace(minister.Name),
			Ability:     int32(minister.Ability),
			Personality: strings.TrimSpace(minister.Personality),
		})
	}
	return out
}
