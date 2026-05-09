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
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
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

func (s *Service) handleCancelBuildingRecipe(delivery commandDelivery, room Session, playerID string, nodeID string) (handleIntentResult, error) {
	nodeID = strings.TrimSpace(nodeID)
	if errCode := validateRecipeCancel(room.State(), playerID, nodeID); errCode != "" {
		message := "目标建筑无效，无法取消配方。"
		switch errCode {
		case "invalid_request":
			message = "取消配方指令不完整，请重新选择建筑。"
		case "unauthorized":
			message = "这座建筑不归你控制，无法取消配方。"
		}
		return rejectedHandleIntentResult(errCode), transportproblem.New(errCode, message)
	}
	if nodeID != "" {
		room.CancelRecipeSelection(playerID, nodeID)
	}
	delivery.snapshot()
	return acceptedHandleIntentResult(), nil
}

func validateRecipeCancel(state *domain.GameState, playerID string, nodeID string) string {
	if state == nil || strings.TrimSpace(playerID) == "" || strings.TrimSpace(nodeID) == "" {
		return "invalid_request"
	}
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok || nodeEntry == nil || !nodeEntry.HasComponent(ecs.BuildingC) {
		return "invalid_target"
	}
	building := ecs.BuildingC.Get(nodeEntry)
	if strings.ToLower(strings.TrimSpace(building.Owner)) != strings.ToLower(strings.TrimSpace(playerID)) {
		return "unauthorized"
	}
	return ""
}
