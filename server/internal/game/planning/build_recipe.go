// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"errors"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
)

func (s *Service) handleBuildRequest(delivery commandDelivery, room Session, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, cityID string) (handleIntentResult, error) {
	if playerState == nil {
		return handleIntentResult{}, errors.New("player not found")
	}
	eval := evaluateBuildCommand(room, playerID, playerState, nodeID, buildingType, cityID)
	if !eval.OK {
		delivery.send(&pb.MsgBuildStructureResult{
			Success:         false,
			NodeId:          strings.TrimSpace(nodeID),
			BuildingTypeId:  strings.TrimSpace(buildingType),
			CityId:          strings.TrimSpace(cityID),
			ErrorCode:       eval.ErrorCode,
			FeedbackMessage: eval.FeedbackMessage,
			FeedbackDetails: eval.FeedbackDetails,
		})
		return rejectedHandleIntentResult(eval.ErrorCode), nil
	}

	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	cityID = strings.TrimSpace(cityID)
	room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CityID: cityID})
	result := &pb.MsgBuildStructureResult{
		Success:        true,
		NodeId:         nodeID,
		BuildingTypeId: buildingType,
		CityId:         cityID,
	}
	if !eval.ReplacingDraft {
		playerState.TokensLeft--
		delivery.sendAllWithSnapshot(result, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
		return acceptedHandleIntentResult(), nil
	}
	delivery.sendWithSnapshot(result)
	return acceptedHandleIntentResult(), nil
}

func (s *Service) handleSetBuildingRecipe(delivery commandDelivery, room Session, playerID string, nodeID string, recipeID string) (handleIntentResult, error) {
	eval := evaluateRecipeCommand(room.State(), playerID, nodeID, recipeID)
	if !eval.OK {
		delivery.send(&pb.MsgSetBuildingRecipeResult{
			Success:         false,
			NodeId:          strings.TrimSpace(nodeID),
			RecipeId:        strings.TrimSpace(recipeID),
			ErrorCode:       eval.ErrorCode,
			FeedbackMessage: eval.FeedbackMessage,
			FeedbackDetails: eval.FeedbackDetails,
		})
		return rejectedHandleIntentResult(eval.ErrorCode), nil
	}

	room.QueueRecipeSelection(domain.RecipeSelectionOrder{
		PlayerID: playerID,
		NodeID:   strings.TrimSpace(nodeID),
		RecipeID: strings.TrimSpace(recipeID),
	})
	delivery.sendWithSnapshot(&pb.MsgSetBuildingRecipeResult{
		Success:  true,
		NodeId:   strings.TrimSpace(nodeID),
		RecipeId: strings.TrimSpace(recipeID),
	})
	return acceptedHandleIntentResult(), nil
}
