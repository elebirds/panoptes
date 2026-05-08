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
	for _, action := range actions {
		if r.stageMinisterActionDraft(playerID, role, action) {
			staged = true
		}
	}
	if staged {
		_ = r.sendMinisterProposalSync(playerID)
	}
	return nil
}

func (r *Runtime) stageMinisterActionDraft(playerID string, role string, action ministerengine.MinisterActionItem) bool {
	switch strings.TrimSpace(action.Type) {
	case "build":
		return r.stageMinisterBuildDraft(playerID, role, action.Params)
	case "move_units":
		return r.stageMinisterMoveDraft(playerID, role, action.Params)
	default:
		slog.Warn("unsupported minister action type", "player_id", playerID, "type", strings.TrimSpace(action.Type))
		return false
	}
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
