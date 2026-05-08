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
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
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
	case "move_units":
		return r.stageMinisterMoveDraft(playerID, role, action.Params)
	case "unit_order", "issue_unit_order":
		return r.stageMinisterUnitOrderDraft(playerID, role, action.Params)
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
	policyIDs := ministerActionStringSliceParam(params, "policy_ids", "policies")
	normalized, errCode := planning.ValidateInstitutionLoadout(r.state, playerID, r.state.Players[playerID], policyIDs)
	if errCode != "" || len(normalized) == 0 {
		slog.Warn("minister institution action rejected", "player_id", playerID, "policy_ids", strings.Join(policyIDs, ","), "error_code", errCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, planning.SetInstitutionLoadoutIntent{PolicyIDs: normalized})
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

	targetLabel := buildingType + " @ " + nodeID
	draft := ministerActionDraftBase(playerID, role, domain.MinisterDraftKindBuild, nodeID+":"+buildingType, targetLabel, r.state.Turn)
	draft.Title = "大臣建设提案"
	draft.Summary = "建议在 " + nodeID + " 建设 " + buildingType + "。"
	draft.Rationale = "该行动来自大臣局势汇报，已通过规则层预检，待你批准后执行。"
	draft.RiskNote = "批准时仍会按当前局势再次校验；若资源或辖区变化，命令可能被拒绝。"
	draft.NodeID = nodeID
	draft.BuildingTypeID = buildingType
	draft.CityID = cityID
	return r.upsertMinisterActionDraft(playerID, draft)
}

func (r *Runtime) stageMinisterMoveDraft(playerID string, role string, params map[string]any) bool {
	unitID := ministerActionStringParam(params, "unit_id")
	targetNodeID := ministerActionStringParam(params, "target_node", "target_node_id", "destination_node_id")
	if unitID == "" || targetNodeID == "" {
		return false
	}

	order := gameorders.UnitOrder{
		PlayerID:     playerID,
		UnitID:       unitID,
		Action:       gameorders.ActionMove,
		TargetNodeID: targetNodeID,
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(r.state, playerID, order); errCode != "" {
		slog.Warn("minister move action rejected", "player_id", playerID, "unit_id", unitID, "target_node_id", targetNodeID, "error_code", errCode)
		return false
	}

	targetLabel := unitID + " -> " + targetNodeID
	draft := ministerActionDraftBase(playerID, role, domain.MinisterDraftKindUnitOrder, unitID+":move:"+targetNodeID, targetLabel, r.state.Turn)
	draft.Title = "大臣调动提案"
	draft.Summary = "建议命令 " + unitID + " 移动至 " + targetNodeID + "。"
	draft.Rationale = "该调动来自大臣局势汇报，已通过规则层预检，待你批准后执行。"
	draft.RiskNote = "批准时仍会按当前战场状态再次校验；若路径或单位状态变化，命令可能被拒绝。"
	draft.UnitID = unitID
	draft.Action = string(gameorders.ActionMove)
	draft.TargetNodeID = targetNodeID
	return r.upsertMinisterActionDraft(playerID, draft)
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

func (r *Runtime) stageMinisterUnitOrderDraft(playerID string, role string, params map[string]any) bool {
	unitID := ministerActionStringParam(params, "unit_id")
	action := ministerActionStringParam(params, "action")
	if unitID == "" || action == "" {
		return false
	}
	intent := planning.IssueUnitOrderIntent{
		UnitID:          unitID,
		Action:          action,
		TargetNodeID:    ministerActionStringParam(params, "target_node", "target_node_id"),
		TargetUnitID:    ministerActionStringParam(params, "target_unit", "target_unit_id"),
		SecondaryNodeID: ministerActionStringParam(params, "secondary_node", "secondary_node_id"),
		Params:          ministerActionUnitOrderParams(params),
	}
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          intent.UnitID,
		Action:          gameorders.UnitAction(intent.Action),
		TargetNodeID:    intent.TargetNodeID,
		TargetUnitID:    intent.TargetUnitID,
		SecondaryNodeID: intent.SecondaryNodeID,
		Params:          cloneStringMap(intent.Params),
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(r.state, playerID, order); errCode != "" {
		slog.Warn("minister unit action rejected", "player_id", playerID, "unit_id", unitID, "action", action, "error_code", errCode)
		return false
	}
	return r.upsertMinisterActionIntentDraft(playerID, role, intent)
}

func ministerActionDraftBase(playerID string, role string, kind domain.MinisterDraftKind, targetID string, targetLabel string, turn int) domain.MinisterDraft {
	draftID := strings.Join([]string{
		strings.TrimSpace(role),
		"report_action",
		strings.TrimSpace(string(kind)),
		safeDraftIDPart(strings.TrimSpace(targetID)),
		fmt.Sprint(turn),
	}, ":")
	return domain.MinisterDraft{
		DraftID:      draftID,
		PlayerID:     strings.TrimSpace(playerID),
		MinisterRole: strings.TrimSpace(role),
		Kind:         kind,
		TargetID:     strings.TrimSpace(targetID),
		TargetLabel:  strings.TrimSpace(targetLabel),
		Status:       domain.MinisterDraftStatusPending,
		Available:    true,
		Turn:         turn,
		Source:       domain.MinisterDraftSourceLLMAction,
	}
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
	role = strings.TrimSpace(role)
	drafts := r.state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return false
	}
	found := false
	for _, draft := range drafts {
		if !ministerDraftSelectableForRole(draft, r.state.Turn, role) {
			continue
		}
		if _, ok := selectedDraftIDs[strings.TrimSpace(draft.DraftID)]; ok {
			found = true
			break
		}
	}
	if !found {
		return false
	}
	changed := false
	for idx := range drafts {
		if !ministerDraftSelectableForRole(drafts[idx], r.state.Turn, role) {
			continue
		}
		draftID := strings.TrimSpace(drafts[idx].DraftID)
		if _, selected := selectedDraftIDs[draftID]; selected {
			if drafts[idx].Source != domain.MinisterDraftSourceLLMAction {
				drafts[idx].Source = domain.MinisterDraftSourceLLMAction
				changed = true
			}
			continue
		}
		if drafts[idx].Source == domain.MinisterDraftSourceRuleOnly || drafts[idx].Source == domain.MinisterDraftSourceRuleLLM {
			drafts[idx].Status = domain.MinisterDraftStatusStale
			drafts[idx].Available = false
			changed = true
		}
	}
	if changed {
		r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	}
	return changed
}

func ministerDraftSelectableForRole(draft domain.MinisterDraft, turn int, role string) bool {
	if !draft.Available || draft.Status != domain.MinisterDraftStatusPending || draft.Turn != turn {
		return false
	}
	if role == "" {
		return true
	}
	return strings.TrimSpace(draft.MinisterRole) == role
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

func ministerActionUnitOrderParams(params map[string]any) map[string]string {
	out := ministerActionStringMapParam(params, "params")
	for _, key := range []string{"building_type_id", "building_type", "improvement_type", "city_id", "service_city_id"} {
		if value := ministerActionStringParam(params, key); value != "" {
			if out == nil {
				out = make(map[string]string)
			}
			out[key] = value
		}
	}
	return out
}

func ministerActionStringMapParam(params map[string]any, keys ...string) map[string]string {
	if len(params) == 0 {
		return nil
	}
	for _, key := range keys {
		raw, ok := params[key]
		if !ok || raw == nil {
			continue
		}
		switch typed := raw.(type) {
		case map[string]string:
			return cloneStringMap(typed)
		case map[string]any:
			out := make(map[string]string, len(typed))
			for k, v := range typed {
				k = strings.TrimSpace(k)
				value := strings.TrimSpace(fmt.Sprint(v))
				if k != "" && value != "" && value != "<nil>" {
					out[k] = value
				}
			}
			if len(out) > 0 {
				return out
			}
		}
	}
	return nil
}
