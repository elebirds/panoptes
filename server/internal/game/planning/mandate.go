// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-05-08 00:00:00 +0800
// Description: 亲政令牌系统 - 处理玩家绕过大臣的直接操作

package planning

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

// MandateAction 亲政操作类型
type MandateAction string

const (
	MandateActionReveal        MandateAction = "reveal"         // 查看真实状态
	MandateActionOverride      MandateAction = "override"       // 否决大臣行动
	MandateActionDirectCommand MandateAction = "direct_command" // 亲自下达命令
)

// handleMandateAction 处理亲政操作
func (s *Service) handleMandateAction(
	delivery commandDelivery,
	room Session,
	playerID string,
	playerState *domain.PlayerState,
	action MandateAction,
) (handleIntentResult, error) {
	if playerState == nil {
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	// 检查亲政令牌是否足够
	if playerState.TokensLeft <= 0 {
		delivery.send(&pb.MsgMandateResult{
			Success:    false,
			Action:     string(action),
			TokensLeft: int32(playerState.TokensLeft),
			ErrorCode:  "no_mandate_tokens",
		})
		return rejectedHandleIntentResult("no_mandate_tokens"), nil
	}

	// 根据操作类型执行不同逻辑
	switch action {
	case MandateActionReveal:
		return s.handleMandateReveal(delivery, room, playerID, playerState)
	case MandateActionOverride:
		return s.handleMandateOverride(delivery, room, playerID, playerState)
	case MandateActionDirectCommand:
		return s.handleMandateDirectCommand(delivery, room, playerID, playerState)
	default:
		return rejectedHandleIntentResult("unknown_mandate_action"), nil
	}
}

// handleMandateReveal 处理查看真实状态
func (s *Service) handleMandateReveal(
	delivery commandDelivery,
	room Session,
	playerID string,
	playerState *domain.PlayerState,
) (handleIntentResult, error) {
	// 这个函数会在 reveal_submit.go 中被调用
	// 这里只是占位，实际逻辑在 reveal_submit.go 中
	return acceptedHandleIntentResult(), nil
}

// handleMandateOverride 处理否决大臣行动
func (s *Service) handleMandateOverride(
	delivery commandDelivery,
	room Session,
	playerID string,
	playerState *domain.PlayerState,
) (handleIntentResult, error) {
	state := room.State()
	if state == nil {
		return rejectedHandleIntentResult("invalid_request"), nil
	}

	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	changed := false
	transitions := make([]ministerDraftTransition, 0, len(drafts))
	for idx := range drafts {
		draft := drafts[idx]
		if draft.Turn != state.Turn || !isMinisterDraftInteractive(draft) {
			continue
		}
		before := draft.Status
		draft.Status = domain.MinisterDraftStatusRejected
		draft.Available = false
		drafts[idx] = draft
		changed = true
		transitions = append(transitions, ministerDraftTransition{Draft: draft, FromStatus: before, ToStatus: draft.Status})
	}
	if !changed {
		delivery.send(&pb.MsgMandateResult{
			Success:    false,
			Action:     "override",
			TokensLeft: int32(playerState.TokensLeft),
			ErrorCode:  "no_minister_actions",
		})
		return rejectedHandleIntentResult("no_minister_actions"), nil
	}

	state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	playerState.TokensLeft--
	recordMinisterDraftTransitions(room, playerID, state.Turn, transitions)

	delivery.send(&pb.MsgMandateResult{
		Success:    true,
		Action:     "override",
		TokensLeft: int32(playerState.TokensLeft),
		Message:    "已否决所有大臣行动",
	})

	return acceptedHandleIntentResult(), nil
}

// handleMandateDirectCommand 处理亲自下达命令
func (s *Service) handleMandateDirectCommand(
	delivery commandDelivery,
	room Session,
	playerID string,
	playerState *domain.PlayerState,
) (handleIntentResult, error) {
	// 消耗亲政令牌
	playerState.TokensLeft--

	// 标记玩家进入亲政模式
	// 在亲政模式下，玩家可以直接下达命令，不经过大臣
	room.SetPlayerMandateMode(playerID, true)

	delivery.send(&pb.MsgMandateResult{
		Success:    true,
		Action:     "direct_command",
		TokensLeft: int32(playerState.TokensLeft),
		Message:    "进入亲政模式，可直接下达命令",
	})

	return acceptedHandleIntentResult(), nil
}
