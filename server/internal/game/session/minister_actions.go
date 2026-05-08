// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-08 00:00:00 +0800
// Description: Stages minister LLM actions as approval-gated planning drafts.

package session

import (
	"context"
	"fmt"
	"log/slog"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/planning"
	gameprojection "github.com/elebirds/panoptes/internal/game/projection"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

func (r *Runtime) ApplyMinisterActions(playerID string, role string, actions []ministerengine.MinisterActionItem) error {
	if r == nil || r.state == nil || len(actions) == 0 {
		return nil
	}
	playerID = strings.TrimSpace(playerID)
	if playerID == "" {
		return nil
	}
	role = strings.TrimSpace(role)
	if role == "" {
		role = "domestic"
	}

	staged := false
	selectedCandidateIDs := make(map[string]struct{})
	for _, action := range actions {
		if ministerActionIsCandidateSelection(action) {
			draftID := ministerActionStringParam(action.Params, "draft_id", "candidate_id")
			if draftID != "" {
				selectedCandidateIDs[draftID] = struct{}{}
			}
			continue
		}
		if r.stageMinisterActionDraft(playerID, role, action) {
			staged = true
		}
	}
	if len(selectedCandidateIDs) > 0 && r.applyMinisterCandidateSelections(playerID, role, selectedCandidateIDs) {
		staged = true
	}
	if staged {
		_ = r.sendMinisterProposalSync(playerID)
	}
	return nil
}

func ministerActionIsCandidateSelection(action ministerengine.MinisterActionItem) bool {
	switch strings.TrimSpace(action.Type) {
	case "select_candidate", "select_draft":
		return true
	default:
		return false
	}
}

func (r *Runtime) stageMinisterActionDraft(playerID string, role string, action ministerengine.MinisterActionItem) bool {
	switch strings.TrimSpace(action.Type) {
	case "build":
		return r.stageMinisterBuildDraft(playerID, role, action.Params)
	case "research", "set_research":
		return r.stageMinisterResearchDraft(playerID, role, action.Params)
	case "policy", "set_policy":
		return r.stageMinisterPolicyDraft(playerID, role, action.Params)
	case "institution_loadout", "set_institution_loadout":
		return r.stageMinisterInstitutionDraft(playerID, role, action.Params)
	case "recipe", "set_recipe", "set_building_recipe":
		return r.stageMinisterRecipeDraft(playerID, role, action.Params)
	default:
		slog.Warn("unsupported minister action type", "player_id", playerID, "type", strings.TrimSpace(action.Type))
		return false
	}
}

func (r *Runtime) stageMinisterResearchDraft(playerID string, role string, params map[string]any) bool {
	technologyID := ministerActionStringParam(params, "technology_id", "tech_id", "target_id")
	if technologyID == "" {
		return false
	}
	validation := economy.ValidateResearchTarget(r.state, playerID, technologyID)
	if !validation.OK {
		slog.Warn("minister research action rejected", "player_id", playerID, "technology_id", technologyID, "error_code", validation.ErrorCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.SetResearchTargetIntent{TechnologyID: technologyID})
}

func (r *Runtime) stageMinisterPolicyDraft(playerID string, role string, params map[string]any) bool {
	policyID := ministerActionStringParam(params, "policy_id", "national_policy_id", "target_id")
	if policyID == "" {
		return false
	}
	if _, errCode := planning.ValidatePolicySelection(r.state, playerID, policyID, "national"); errCode != "" {
		slog.Warn("minister policy action rejected", "player_id", playerID, "policy_id", policyID, "error_code", errCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.SetPolicyIntent{NationalPolicyID: policyID})
}

func (r *Runtime) stageMinisterInstitutionDraft(playerID string, role string, params map[string]any) bool {
	if r == nil || r.state == nil {
		return false
	}
	institutionIDs := ministerActionStringSliceParam(params, "institution_ids", "policies")
	normalized, errCode := planning.ValidateInstitutionLoadout(r.state, playerID, r.state.Players[playerID], institutionIDs)
	if errCode != "" || len(normalized) == 0 {
		slog.Warn("minister institution action rejected", "player_id", playerID, "institution_ids", strings.Join(institutionIDs, ","), "error_code", errCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.SetInstitutionLoadoutIntent{InstitutionIDs: normalized})
}

func (r *Runtime) stageMinisterBuildDraft(playerID string, role string, params map[string]any) bool {
	nodeID := ministerActionStringParam(params, "node_id", "target_node", "target_node_id")
	buildingType := ministerActionStringParam(params, "building_type", "building_type_id")
	cityID := ministerActionStringParam(params, "city_id")
	if nodeID == "" || buildingType == "" {
		return false
	}

	validation := economy.ValidateBuildOrder(r.state, playerID, nodeID, buildingType, cityID)
	if !validation.OK {
		slog.Warn("minister build action rejected", "player_id", playerID, "node_id", nodeID, "building_type", buildingType, "city_id", cityID, "error_code", validation.ErrorCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.BuildStructureIntent{
		NodeID:         nodeID,
		BuildingTypeID: buildingType,
		CityID:         cityID,
	})
}

func (r *Runtime) stageMinisterRecipeDraft(playerID string, role string, params map[string]any) bool {
	nodeID := ministerActionStringParam(params, "node_id", "building_node_id")
	recipeID := ministerActionStringParam(params, "recipe_id", "target_id")
	if nodeID == "" || recipeID == "" {
		return false
	}
	validation := economy.ValidateRecipeSelection(r.state, playerID, nodeID, recipeID)
	if !validation.OK {
		slog.Warn("minister recipe action rejected", "player_id", playerID, "node_id", nodeID, "recipe_id", recipeID, "error_code", validation.ErrorCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.SetBuildingRecipeIntent{NodeID: nodeID, RecipeID: recipeID})
}

func (r *Runtime) upsertMinisterActionDraft(playerID string, draft domain.MinisterDraft) bool {
	if r == nil || r.state == nil {
		return false
	}
	drafts := r.state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	for idx := range drafts {
		if strings.TrimSpace(drafts[idx].DraftID) != strings.TrimSpace(draft.DraftID) {
			continue
		}
		drafts[idx] = draft
		r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
		return true
	}
	drafts = append(drafts, draft)
	r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	return true
}

func (r *Runtime) applyMinisterCandidateSelections(playerID string, role string, selectedDraftIDs map[string]struct{}) bool {
	if r == nil || r.state == nil || len(selectedDraftIDs) == 0 {
		return false
	}
	turn := r.state.Turn
	role = strings.TrimSpace(role)
	r.PrepareMinisterDraftCacheForTurn(turn)
	candidates := r.preparedMinisterDraftsForPlayer(turn, playerID)
	if len(candidates) == 0 {
		return false
	}
	changed := false
	for _, draft := range candidates {
		if !draft.Available || draft.Status != domain.MinisterDraftStatusPending || draft.Turn != turn {
			continue
		}
		if role != "" && strings.TrimSpace(draft.MinisterRole) != role {
			continue
		}
		if _, selected := selectedDraftIDs[strings.TrimSpace(draft.DraftID)]; !selected {
			continue
		}
		draft.Source = domain.MinisterDraftSourceLLMAction
		if r.upsertMinisterActionDraft(playerID, draft) {
			changed = true
		}
	}
	return changed
}

func (r *Runtime) upsertMinisterActionIntentDraft(playerID string, role string, intent planning.Intent) bool {
	if r == nil || r.state == nil {
		return false
	}
	draft, ok := ministerDraftFromIntent(r.state.Turn, playerID, intent)
	if !ok {
		return false
	}
	role = strings.TrimSpace(role)
	if role == "" {
		role = domesticMinisterRole
	}
	draft.MinisterRole = role
	draft.Source = domain.MinisterDraftSourceLLMAction
	draft.DraftID = strings.Join([]string{
		role,
		"report_action",
		strings.TrimSpace(string(draft.Kind)),
		safeDraftIDPart(strings.TrimSpace(draft.TargetID)),
		fmt.Sprint(r.state.Turn),
	}, ":")
	draft.Title, draft.Summary, draft.Rationale, draft.RiskNote = ministerDraftText(role, string(draft.Kind), draft.TargetLabel)
	return r.upsertMinisterActionDraft(playerID, draft)
}

func (r *Runtime) sendMinisterProposalSync(playerID string) error {
	if r == nil || r.state == nil {
		return nil
	}
	observations := r.observations
	if observations == nil {
		observations = gamequery.NewObservationStore()
	}
	observation := observations.BuildObservation(r.state, playerID)
	msg := gameprojection.ProjectGameSyncFromObservation(r.state, observation, int32(r.state.Turn), r.state.Phase, "", nil)
	if msg == nil {
		return nil
	}
	return r.SendToPlayer(context.Background(), playerID, msg)
}

func ministerActionStringParam(params map[string]any, keys ...string) string {
	for _, key := range keys {
		value, ok := params[key]
		if !ok || value == nil {
			continue
		}
		switch typed := value.(type) {
		case string:
			return strings.TrimSpace(typed)
		case fmt.Stringer:
			return strings.TrimSpace(typed.String())
		default:
			text := strings.TrimSpace(fmt.Sprint(value))
			if text != "" && text != "<nil>" {
				return text
			}
		}
	}
	return ""
}

func ministerActionStringSliceParam(params map[string]any, keys ...string) []string {
	if len(params) == 0 {
		return nil
	}
	for _, key := range keys {
		raw, ok := params[key]
		if !ok || raw == nil {
			continue
		}
		switch typed := raw.(type) {
		case []string:
			out := make([]string, 0, len(typed))
			for _, value := range typed {
				if value = strings.TrimSpace(value); value != "" {
					out = append(out, value)
				}
			}
			return out
		case []any:
			out := make([]string, 0, len(typed))
			for _, value := range typed {
				if text := strings.TrimSpace(fmt.Sprint(value)); text != "" && text != "<nil>" {
					out = append(out, text)
				}
			}
			return out
		case string:
			parts := strings.Split(typed, ",")
			out := make([]string, 0, len(parts))
			for _, value := range parts {
				if value = strings.TrimSpace(value); value != "" {
					out = append(out, value)
				}
			}
			return out
		}
	}
	return nil
}
