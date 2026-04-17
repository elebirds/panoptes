package session

import (
	"context"
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/ai"
)

const domesticMinisterRole = "domestic"

func (r *Runtime) PrepareMinisterDraftCacheForTurn(turn int) {
	if r == nil || r.state == nil || turn <= 0 {
		return
	}
	if _, ok := r.preparedMinisterDrafts[turn]; ok {
		return
	}

	draftsByPlayer := make(map[string][]domain.MinisterDraft, len(r.participants))
	for _, binding := range r.participants {
		playerID := strings.TrimSpace(binding.Participant.ID)
		if playerID == "" {
			continue
		}
		observation := r.BuildObservation(playerID)
		req := ai.Request{
			Participant: binding.Participant,
			State:       r.state,
			Observation: observation,
		}
		candidates := ai.BuildDomesticDraftCandidates(context.Background(), req)
		draftsByPlayer[playerID] = buildDomesticMinisterDrafts(turn, playerID, candidates)
	}

	r.preparedMinisterDrafts[turn] = draftsByPlayer
	for cachedTurn := range r.preparedMinisterDrafts {
		if cachedTurn >= turn-1 {
			continue
		}
		delete(r.preparedMinisterDrafts, cachedTurn)
	}
}

func (r *Runtime) ApplyPreparedMinisterDrafts(turn int) {
	if r == nil || r.state == nil || turn <= 0 || r.state.Turn != turn {
		return
	}
	if len(r.state.TurnRuntime.Planning.MinisterDrafts) > 0 {
		return
	}
	if _, ok := r.preparedMinisterDrafts[turn]; !ok {
		r.PrepareMinisterDraftCacheForTurn(turn)
	}

	r.state.TurnRuntime.Planning.EnsureDraftMaps()
	clear(r.state.TurnRuntime.Planning.MinisterDrafts)
	for playerID, drafts := range r.preparedMinisterDrafts[turn] {
		r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	}
}

func buildDomesticMinisterDrafts(turn int, playerID string, candidates []ai.DomesticDraftCandidate) []domain.MinisterDraft {
	if len(candidates) == 0 {
		return nil
	}

	out := make([]domain.MinisterDraft, 0, len(candidates))
	for _, candidate := range candidates {
		targetID := strings.TrimSpace(candidate.TargetID)
		kind := strings.TrimSpace(candidate.Kind)
		if targetID == "" || kind == "" {
			continue
		}

		targetLabel := strings.TrimSpace(candidate.TargetLabel)
		if targetLabel == "" {
			targetLabel = targetID
		}
		title, summary, rationale, riskNote := domesticDraftText(kind, targetLabel)
		out = append(out, domain.MinisterDraft{
			DraftID:      fmt.Sprintf("domestic:%s:%s:%d", kind, targetID, turn),
			PlayerID:     playerID,
			MinisterRole: domesticMinisterRole,
			Kind:         domain.MinisterDraftKind(kind),
			TargetID:     targetID,
			TargetLabel:  targetLabel,
			Title:        title,
			Summary:      summary,
			Rationale:    rationale,
			RiskNote:     riskNote,
			Status:       domain.MinisterDraftStatusPending,
			Available:    true,
			Turn:         turn,
			Source:       domain.MinisterDraftSourceRuleOnly,
		})
	}
	return out
}

func domesticDraftText(kind string, targetLabel string) (string, string, string, string) {
	switch strings.TrimSpace(kind) {
	case string(domain.MinisterDraftKindPolicy):
		return "调整国家政策", targetLabel + "适合当前国势，可作为本回合优先政策。", "这项建议来自现有规则评估，目标是让国家政策与当前局势更一致。", "若本回合还有其他更重要的手动安排，这张卡可能会变为“已偏离”。"
	default:
		return "锁定科研目标", targetLabel + "是当前最优科研候选，可作为本回合主线研究。", "这项建议来自现有规则评估，优先兼顾当前局面与后续解锁收益。", "如果你改选其他科技，这张卡会保留但标记为“已偏离”。"
	}
}
