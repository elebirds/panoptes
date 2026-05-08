package domain

import "strings"

type MinisterDraftKind string

const (
	MinisterDraftKindResearch    MinisterDraftKind = "research"
	MinisterDraftKindPolicy      MinisterDraftKind = "policy"
	MinisterDraftKindInstitution MinisterDraftKind = "institution"
	MinisterDraftKindBuild       MinisterDraftKind = "build"
	MinisterDraftKindRecipe      MinisterDraftKind = "recipe"
	MinisterDraftKindUnitOrder   MinisterDraftKind = "unit_order"
)

type MinisterDraftStatus string

const (
	MinisterDraftStatusPending  MinisterDraftStatus = "pending"
	MinisterDraftStatusAccepted MinisterDraftStatus = "accepted"
	MinisterDraftStatusRejected MinisterDraftStatus = "rejected"
	MinisterDraftStatusStale    MinisterDraftStatus = "stale"
)

type MinisterDraftSource string

const (
	MinisterDraftSourceRuleOnly  MinisterDraftSource = "rule_only"
	MinisterDraftSourceRuleLLM   MinisterDraftSource = "rule+llm"
	MinisterDraftSourceLLMAction MinisterDraftSource = "llm_action"
)

type MinisterDraft struct {
	DraftID         string              `json:"draft_id"`
	PlayerID        string              `json:"player_id"`
	MinisterRole    string              `json:"minister_role"`
	Kind            MinisterDraftKind   `json:"kind"`
	TargetID        string              `json:"target_id"`
	TargetLabel     string              `json:"target_label"`
	Title           string              `json:"title"`
	Summary         string              `json:"summary"`
	Rationale       string              `json:"rationale"`
	RiskNote        string              `json:"risk_note"`
	Status          MinisterDraftStatus `json:"status"`
	Available       bool                `json:"available"`
	Turn            int                 `json:"turn"`
	Source          MinisterDraftSource `json:"source"`
	PolicyIDs       []string            `json:"policy_ids,omitempty"`
	NodeID          string              `json:"node_id,omitempty"`
	BuildingTypeID  string              `json:"building_type_id,omitempty"`
	CityID          string              `json:"city_id,omitempty"`
	RecipeID        string              `json:"recipe_id,omitempty"`
	UnitID          string              `json:"unit_id,omitempty"`
	Action          string              `json:"action,omitempty"`
	TargetNodeID    string              `json:"target_node_id,omitempty"`
	TargetUnitID    string              `json:"target_unit_id,omitempty"`
	SecondaryNodeID string              `json:"secondary_node_id,omitempty"`
	Params          map[string]string   `json:"params,omitempty"`
}

func (p *PlanningInputs) SetMinisterDrafts(playerID string, drafts []MinisterDraft) {
	if p == nil {
		return
	}
	p.EnsureDraftMaps()
	playerID = strings.TrimSpace(playerID)
	if playerID == "" {
		return
	}
	if len(drafts) == 0 {
		delete(p.MinisterDrafts, playerID)
		return
	}
	out := make([]MinisterDraft, 0, len(drafts))
	for _, draft := range drafts {
		draft.PlayerID = playerID
		out = append(out, draft)
	}
	p.MinisterDrafts[playerID] = out
}

func (p *PlanningInputs) MinisterDraftsForPlayer(playerID string) []MinisterDraft {
	if p == nil || p.MinisterDrafts == nil {
		return nil
	}
	drafts := p.MinisterDrafts[strings.TrimSpace(playerID)]
	if len(drafts) == 0 {
		return nil
	}
	out := make([]MinisterDraft, len(drafts))
	copy(out, drafts)
	return out
}

func (p *PlanningInputs) FindMinisterDraft(playerID string, draftID string) (MinisterDraft, int, bool) {
	if p == nil || p.MinisterDrafts == nil {
		return MinisterDraft{}, -1, false
	}
	playerID = strings.TrimSpace(playerID)
	draftID = strings.TrimSpace(draftID)
	if playerID == "" || draftID == "" {
		return MinisterDraft{}, -1, false
	}
	drafts := p.MinisterDrafts[playerID]
	for idx := range drafts {
		if strings.TrimSpace(drafts[idx].DraftID) == draftID {
			return drafts[idx], idx, true
		}
	}
	return MinisterDraft{}, -1, false
}

func (p *PlanningInputs) ReplaceMinisterDraft(playerID string, idx int, draft MinisterDraft) bool {
	if p == nil || p.MinisterDrafts == nil {
		return false
	}
	playerID = strings.TrimSpace(playerID)
	if playerID == "" || idx < 0 || idx >= len(p.MinisterDrafts[playerID]) {
		return false
	}
	draft.PlayerID = playerID
	p.MinisterDrafts[playerID][idx] = draft
	return true
}
