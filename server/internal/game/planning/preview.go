// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"context"
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	"github.com/elebirds/panoptes/internal/engine/economy"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	transportproblem "github.com/elebirds/panoptes/internal/transport/problem"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type buildCommandEvaluation struct {
	OK              bool
	ErrorCode       string
	Validation      economy.BuildOrderValidation
	ResourceCost    domain.ResourceBag
	PointCost       domain.PointBag
	FeedbackMessage string
	FeedbackDetails []*pb.FeedbackDetail
}

type recipeCommandEvaluation struct {
	OK              bool
	ErrorCode       string
	Validation      economy.RecipeSelectionValidation
	FeedbackMessage string
	FeedbackDetails []*pb.FeedbackDetail
	WarningMessage  string
	WarningDetails  []*pb.FeedbackDetail
}

func handlePreviewCommand(room Session, inbound cmddispatch.InboundContext, cmd *pb.PlanningCommand) error {
	msg, err := previewCommandResponse(room, inbound.PlayerID, cmd)
	if err != nil {
		return err
	}
	eventCtx := coretransport.ContextWithEventMeta(context.Background(), coretransport.EventMetaFromInbound(inbound))
	newCommandDelivery(eventCtx, inbound.PlayerID, room).send(msg)
	return nil
}

func previewCommandResponse(room Session, playerID string, cmd *pb.PlanningCommand) (proto.Message, error) {
	switch body := cmd.GetBody().(type) {
	case *pb.PlanningCommand_PlanningPathPreviewRequest:
		return buildPlanningPathPreviewResponse(room.State(), playerID, body.PlanningPathPreviewRequest), nil
	case *pb.PlanningCommand_BuildStructurePreview:
		return buildStructurePreviewResponse(room, playerID, body.BuildStructurePreview), nil
	case *pb.PlanningCommand_SetBuildingRecipePreview:
		return setBuildingRecipePreviewResponse(room.State(), playerID, body.SetBuildingRecipePreview), nil
	default:
		return nil, transportproblem.InvalidRequest("unsupported planning preview command")
	}
}

func evaluateBuildCommand(room Session, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, cityID string) buildCommandEvaluation {
	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	cityID = strings.TrimSpace(cityID)
	ctx := feedbackContext{
		NodeID:         nodeID,
		BuildingTypeID: buildingType,
		CityID:         cityID,
	}
	eval := buildCommandEvaluation{
		ResourceCost: domain.NewResourceBag(),
		PointCost:    domain.NewPointBag(),
	}

	if room == nil || room.State() == nil || playerState == nil {
		eval.ErrorCode = "invalid_request"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(nil, playerID, ctx, eval.ErrorCode)
		return eval
	}

	validation := economy.ValidateBuildOrder(room.State(), playerID, nodeID, buildingType, cityID)
	if !validation.OK {
		eval.ErrorCode = validation.ErrorCode
		eval.Validation = validation
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}
	eval.Validation = validation

	resourceCost, err := domain.ResourceBagFromAmounts(validation.Building.ResourceCosts)
	if err != nil {
		eval.ErrorCode = "invalid_directive"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}
	pointCost, err := domain.PointBagFromAmounts(validation.Building.PointCosts)
	if err != nil {
		eval.ErrorCode = "invalid_directive"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}
	eval.ResourceCost = room.State().ApplyResourceModifiers(playerID, string(staticdata.ModifierTriggerBuildingResourceCost), buildingType, resourceCost)
	eval.PointCost = room.State().ApplyPointModifiers(playerID, string(staticdata.ModifierTriggerBuildingPointCost), buildingType, pointCost)

	if room.IsDevMode() {
		eval.OK = true
		return eval
	}

	availableResources := room.State().PlayerResourceView(playerID)
	availablePoints := planningPointBudget(room.State(), playerID)
	subtractQueuedBuildCosts(room.State(), playerID, nodeID, availableResources, availablePoints)

	if !availableResources.CanAfford(eval.ResourceCost) {
		ctx.MissingResources = missingResources(availableResources, eval.ResourceCost)
		eval.ErrorCode = "insufficient_resources"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}

	if !availablePoints.CanAfford(eval.PointCost) {
		ctx.MissingPoints = missingPoints(availablePoints, eval.PointCost)
		eval.ErrorCode = "insufficient_points"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}

	eval.OK = true
	return eval
}

func subtractQueuedBuildCosts(state *domain.GameState, playerID string, replacingNodeID string, resources domain.ResourceBag, points domain.PointBag) {
	if state == nil || resources == nil || points == nil {
		return
	}
	replacingNodeID = strings.TrimSpace(replacingNodeID)
	for _, order := range state.TurnRuntime.Planning.BuildOrders {
		if order.PlayerID != playerID || strings.TrimSpace(order.NodeID) == replacingNodeID {
			continue
		}
		buildingType := strings.TrimSpace(order.BuildingType)
		if buildingType == "" {
			continue
		}
		cfg, ok := staticdata.Default().GetBuilding(buildingType)
		if !ok {
			continue
		}
		resourceCost, err := domain.ResourceBagFromAmounts(cfg.ResourceCosts)
		if err == nil {
			resourceCost = state.ApplyResourceModifiers(playerID, string(staticdata.ModifierTriggerBuildingResourceCost), buildingType, resourceCost)
			for _, key := range resourceCost.Keys() {
				resources.AddAmount(key, -resourceCost.Get(key))
			}
		}
		pointCost, err := domain.PointBagFromAmounts(cfg.PointCosts)
		if err == nil {
			pointCost = state.ApplyPointModifiers(playerID, string(staticdata.ModifierTriggerBuildingPointCost), buildingType, pointCost)
			for _, key := range pointCost.Keys() {
				points.AddAmount(key, -pointCost.Get(key))
			}
		}
	}
}

func planningPointBudget(state *domain.GameState, playerID string) domain.PointBag {
	if state == nil {
		return domain.NewPointBag()
	}
	budget := state.EnsurePointBudget(playerID).Clone()
	if len(budget) > 0 {
		return budget
	}
	budget.Set(domain.PointResearchOutput, state.EffectiveResearchOutput(playerID))
	budget.Set(domain.PointIndustryOutput, state.EffectiveIndustryOutput(playerID))
	return budget
}

func evaluateRecipeCommand(state *domain.GameState, playerID string, nodeID string, recipeID string) recipeCommandEvaluation {
	nodeID = strings.TrimSpace(nodeID)
	recipeID = strings.TrimSpace(recipeID)
	ctx := feedbackContext{
		NodeID:   nodeID,
		RecipeID: recipeID,
	}
	eval := recipeCommandEvaluation{}
	if state == nil {
		eval.ErrorCode = "invalid_request"
		eval.FeedbackMessage, eval.FeedbackDetails = recipeFeedback(nil, playerID, ctx, eval.ErrorCode)
		return eval
	}

	validation := economy.ValidateRecipeSelection(state, playerID, nodeID, recipeID)
	if !validation.OK {
		eval.ErrorCode = validation.ErrorCode
		eval.Validation = validation
		eval.FeedbackMessage, eval.FeedbackDetails = recipeFeedback(state, playerID, ctx, eval.ErrorCode)
		return eval
	}
	eval.OK = true
	eval.Validation = validation

	warningReason := currentRecipeWarningReason(state, validation.NodeEntry)
	if warningReason != "" {
		ctx = recipeFeedbackContextForEntry(validation.NodeEntry, recipeID)
		eval.WarningMessage = runtimeReasonMessage(warningReason)
		eval.WarningDetails = feedbackDetails(ctx)
	}
	return eval
}

func buildStructurePreviewResponse(room Session, playerID string, msg *pb.MsgBuildStructurePreviewRequest) *pb.MsgBuildStructurePreviewResponse {
	resp := &pb.MsgBuildStructurePreviewResponse{
		RequestId:      msg.GetRequestId(),
		NodeId:         msg.GetNodeId(),
		BuildingTypeId: msg.GetBuildingTypeId(),
		CityId:         msg.GetCityId(),
	}
	var playerState *domain.PlayerState
	if room != nil && room.State() != nil {
		playerState = room.State().Players[playerID]
	}
	eval := evaluateBuildCommand(room, playerID, playerState, msg.GetNodeId(), msg.GetBuildingTypeId(), msg.GetCityId())
	resp.Valid = eval.OK
	resp.ErrorCode = eval.ErrorCode
	resp.FeedbackMessage = eval.FeedbackMessage
	resp.FeedbackDetails = eval.FeedbackDetails
	return resp
}

func setBuildingRecipePreviewResponse(state *domain.GameState, playerID string, msg *pb.MsgSetBuildingRecipePreviewRequest) *pb.MsgSetBuildingRecipePreviewResponse {
	resp := &pb.MsgSetBuildingRecipePreviewResponse{
		RequestId: msg.GetRequestId(),
		NodeId:    msg.GetNodeId(),
		RecipeId:  msg.GetRecipeId(),
	}
	eval := evaluateRecipeCommand(state, playerID, msg.GetNodeId(), msg.GetRecipeId())
	resp.Valid = eval.OK
	resp.ErrorCode = eval.ErrorCode
	if eval.OK {
		resp.FeedbackMessage = eval.WarningMessage
		resp.FeedbackDetails = eval.WarningDetails
		return resp
	}
	resp.FeedbackMessage = eval.FeedbackMessage
	resp.FeedbackDetails = eval.FeedbackDetails
	return resp
}

func buildPlanningPathPreviewResponse(state *domain.GameState, playerID string, msg *pb.MsgPlanningPathPreviewRequest) *pb.MsgPlanningPathPreviewResponse {
	resp := &pb.MsgPlanningPathPreviewResponse{
		RequestId:    msg.GetRequestId(),
		UnitId:       msg.GetUnitId(),
		Action:       msg.GetAction(),
		TargetNodeId: msg.GetTargetNodeId(),
		Valid:        false,
	}
	if state == nil || msg == nil {
		resp.ErrorCode = "invalid_request"
		return resp
	}
	if gameorders.UnitAction(msg.GetAction()) != gameorders.ActionMove {
		resp.ErrorCode = "invalid_directive"
		return resp
	}
	entry, ok := findPreviewUnit(state, msg.GetUnitId(), playerID)
	if !ok {
		resp.ErrorCode = "unit_not_found"
		return resp
	}
	if _, ok := state.GetNode(msg.GetTargetNodeId()); !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	planner := combat.NewWeightedRoutePlanner(combat.DefaultTerrainCostPolicy{})
	preview, ok := planner.BuildPreview(state.World, state, ecs.UnitStatsC.Get(entry).ID, msg.GetTargetNodeId())
	if !ok {
		resp.ErrorCode = "invalid_target"
		return resp
	}

	resp.Valid = true
	resp.PathNodeIds = append(resp.PathNodeIds, preview.PathNodeIDs...)
	resp.FirstTurnNodeId = preview.FirstTurnNodeID
	resp.TotalTurns = int32(preview.TotalTurns)
	for _, stop := range preview.TurnStops {
		resp.TurnStops = append(resp.TurnStops, &pb.MarchTurnStop{
			TurnIndex: int32(stop.TurnIndex),
			NodeId:    stop.NodeID,
		})
	}
	return resp
}

func currentRecipeWarningReason(state *domain.GameState, entry *donburi.Entry) string {
	if entry == nil {
		return ""
	}
	if entry.HasComponent(ecs.BuildingOperationC) {
		if reason := strings.TrimSpace(ecs.BuildingOperationC.Get(entry).BlockedReason); reason != "" {
			return reason
		}
	}
	if state != nil {
		_, reason := domain.BuildingLifecycleStateAtTurn(entry, state.Turn)
		if reason != "" {
			return strings.TrimSpace(reason)
		}
	}
	if entry.HasComponent(ecs.BuildingStateC) {
		return strings.TrimSpace(ecs.BuildingStateC.Get(entry).DisabledReason)
	}
	return ""
}

func recipeContextFromNode(state *domain.GameState, nodeID string, recipeID string) feedbackContext {
	ctx := feedbackContext{NodeID: strings.TrimSpace(nodeID), RecipeID: strings.TrimSpace(recipeID)}
	if state == nil || ctx.NodeID == "" {
		return ctx
	}
	entry, ok := state.GetNode(ctx.NodeID)
	if !ok || entry == nil {
		return ctx
	}
	return recipeFeedbackContextForEntry(entry, recipeID)
}

func buildContextFromValidation(state *domain.GameState, validation economy.BuildOrderValidation, nodeID string, buildingType string, cityID string) feedbackContext {
	ctx := feedbackContext{
		NodeID:         strings.TrimSpace(nodeID),
		BuildingTypeID: strings.TrimSpace(buildingType),
		CityID:         strings.TrimSpace(cityID),
	}
	if validation.NodeEntry != nil {
		node := ecs.NodeC.Get(validation.NodeEntry)
		ctx.NodeResourceType = strings.TrimSpace(node.ResourceType)
	}
	if validation.Building.ID != "" {
		ctx.RequiredResourceType = strings.TrimSpace(validation.Building.RequiredResourceType)
	}
	return enrichBuildFeedbackContext(state, ctx)
}

func recipeContextFromValidation(state *domain.GameState, validation economy.RecipeSelectionValidation, nodeID string, recipeID string) feedbackContext {
	ctx := recipeContextFromNode(state, nodeID, recipeID)
	if ctx.BuildingTypeID == "" && validation.Building.Type != "" {
		ctx.BuildingTypeID = strings.TrimSpace(string(validation.Building.Type))
	}
	if ctx.CityID == "" && validation.NodeEntry != nil {
		ctx.CityID = strings.TrimSpace(building.ResolveCityID(validation.NodeEntry))
	}
	return ctx
}
