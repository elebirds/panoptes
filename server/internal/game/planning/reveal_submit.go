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
	if playerState.TokensLeft <= 0 {
		delivery.send(&pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
		return rejectedHandleIntentResult("no_tokens_left"), nil
	}
	nodeView := room.BuildNodeViewForPlayer(nodeID, playerID)
	if nodeView == nil {
		delivery.send(&pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return rejectedHandleIntentResult("invalid_target"), nil
	}
	playerState.TokensLeft--
	delivery.send(&pb.MsgRevealResult{NodeId: nodeID, TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleSubmitTurn(room Session, playerID string) (handleIntentResult, error) {
	room.Submit(playerID)
	return acceptedHandleIntentResult(), nil
}
