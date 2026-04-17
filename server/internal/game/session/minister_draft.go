package session

import (
	"context"
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/ai"
)

const domesticMinisterRole = "domestic"

func (r *Runtime) PrepareMinisterDraftCacheForTurn(turn int) {
	if r == nil || r.state == nil || turn <= 0 {
		return
	}
	r.preparedMinisterDraftsMu.RLock()
	if _, ok := r.preparedMinisterDrafts[turn]; ok {
		r.preparedMinisterDraftsMu.RUnlock()
		return
	}
	r.preparedMinisterDraftsMu.RUnlock()

	draftsByPlayer := make(map[string][]domain.MinisterDraft, len(r.participants))
	jobs := make([]preparedMinisterDraftPolishJob, 0, len(r.participants))
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
		drafts := buildDomesticMinisterDrafts(turn, playerID, candidates)
		draftsByPlayer[playerID] = drafts
		for _, draft := range drafts {
			r.RecordMinisterMemory(playerID, draft.MinisterRole, ministerengine.MemoryEntry{
				Turn:       turn,
				Type:       "draft",
				Content:    fmt.Sprintf("%s:%s", draft.Kind, strings.TrimSpace(draft.TargetLabel)),
				Outcome:    "generated",
				PlayerResp: "pending",
			})
			if r.ministerEngine == nil || draft.MinisterRole != domesticMinisterRole {
				continue
			}
			jobs = append(jobs, preparedMinisterDraftPolishJob{
				Turn:     turn,
				PlayerID: playerID,
				Draft:    draft,
				Input: ministerengine.DraftPromptInput{
					Turn:               turn,
					PlayerID:           playerID,
					ObservationSummary: buildMinisterObservationSummary(r.state, observation),
					CurrentPolicy:      currentPolicyValue(r.state, playerID),
					CurrentResearch:    currentResearchValue(r.state, playerID),
				},
			})
		}
	}

	r.preparedMinisterDraftsMu.Lock()
	r.preparedMinisterDrafts[turn] = draftsByPlayer
	for cachedTurn := range r.preparedMinisterDrafts {
		if cachedTurn >= turn-1 {
			continue
		}
		delete(r.preparedMinisterDrafts, cachedTurn)
	}
	r.preparedMinisterDraftsMu.Unlock()

	for _, job := range jobs {
		go r.polishPreparedMinisterDraft(job)
	}
}

func (r *Runtime) ApplyPreparedMinisterDrafts(turn int) {
	if r == nil || r.state == nil || turn <= 0 || r.state.Turn != turn {
		return
	}
	if len(r.state.TurnRuntime.Planning.MinisterDrafts) > 0 {
		return
	}
	r.preparedMinisterDraftsMu.RLock()
	_, ok := r.preparedMinisterDrafts[turn]
	r.preparedMinisterDraftsMu.RUnlock()
	if !ok {
		r.PrepareMinisterDraftCacheForTurn(turn)
	}

	r.state.TurnRuntime.Planning.EnsureDraftMaps()
	clear(r.state.TurnRuntime.Planning.MinisterDrafts)
	r.preparedMinisterDraftsMu.RLock()
	for playerID, drafts := range r.preparedMinisterDrafts[turn] {
		r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	}
	r.preparedMinisterDraftsMu.RUnlock()
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

type preparedMinisterDraftPolishJob struct {
	Turn     int
	PlayerID string
	Draft    domain.MinisterDraft
	Input    ministerengine.DraftPromptInput
}

func (r *Runtime) polishPreparedMinisterDraft(job preparedMinisterDraftPolishJob) {
	if r == nil || r.ministerEngine == nil {
		return
	}
	output, ok := r.ministerEngine.PolishDraft(context.Background(), job.PlayerID, job.Draft, job.Input)
	if !ok || output == nil {
		return
	}
	r.applyPreparedMinisterDraftPolish(job, output)
}

func (r *Runtime) applyPreparedMinisterDraftPolish(job preparedMinisterDraftPolishJob, output *ministerengine.DraftOutput) {
	if r == nil || output == nil {
		return
	}
	r.preparedMinisterDraftsMu.Lock()
	defer r.preparedMinisterDraftsMu.Unlock()
	if r.planningStartPreparedTurn == job.Turn {
		return
	}
	playerDrafts, ok := r.preparedMinisterDrafts[job.Turn]
	if !ok {
		return
	}
	drafts := playerDrafts[job.PlayerID]
	for idx := range drafts {
		if strings.TrimSpace(drafts[idx].DraftID) != strings.TrimSpace(job.Draft.DraftID) {
			continue
		}
		drafts[idx].Title = strings.TrimSpace(output.Title)
		drafts[idx].Summary = strings.TrimSpace(output.Summary)
		drafts[idx].Rationale = strings.TrimSpace(output.Rationale)
		drafts[idx].RiskNote = strings.TrimSpace(output.RiskNote)
		drafts[idx].Source = domain.MinisterDraftSourceRuleLLM
		playerDrafts[job.PlayerID] = drafts
		return
	}
}
