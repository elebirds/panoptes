// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleRevealNode(delivery commandDelivery, room Session, playerID string, playerState *domain.PlayerState, nodeID string) (handleIntentResult, error) {
	if playerState == nil {
		return rejectedHandleIntentResult("invalid_request"), nil
	}
	// 检查亲政令牌是否足够
	if playerState.TokensLeft <= 0 {
		delivery.send(&pb.MsgMandateResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_mandate_tokens"})
		return rejectedHandleIntentResult("no_mandate_tokens"), nil
	}
	nodeView := room.BuildNodeViewForPlayer(nodeID, playerID)
	if nodeView == nil {
		delivery.send(&pb.MsgMandateResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return rejectedHandleIntentResult("invalid_target"), nil
	}
	// 消耗亲政令牌
	playerState.TokensLeft--
	delivery.send(&pb.MsgRevealResult{NodeId: nodeID, TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleSubmitTurn(room Session, playerID string) (handleIntentResult, error) {
	room.Submit(playerID)
	return acceptedHandleIntentResult(), nil
}
