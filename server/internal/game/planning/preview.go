package planning

import (
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/economy"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type buildCommandEvaluation struct {
	OK               bool
	ErrorCode        string
	Validation       economy.BuildOrderValidation
	ResourceCost     domain.ResourceBag
	PointCost        domain.PointBag
	FeedbackMessage  string
	FeedbackDetails  []*pb.FeedbackDetail
	ReplacingDraft   bool
}

type recipeCommandEvaluation struct {
	OK               bool
	ErrorCode        string
	Validation       economy.RecipeSelectionValidation
	FeedbackMessage  string
	FeedbackDetails  []*pb.FeedbackDetail
	WarningMessage   string
	WarningDetails   []*pb.FeedbackDetail
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
		ResourceCost:   domain.NewResourceBag(),
		PointCost:      domain.NewPointBag(),
		ReplacingDraft: room != nil && room.State() != nil && room.State().TurnRuntime.Planning.HasBuildOrder(playerID, nodeID),
	}

	if room == nil || room.State() == nil || playerState == nil {
		eval.ErrorCode = "invalid_request"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(nil, playerID, ctx, eval.ErrorCode)
		return eval
	}

	if !eval.ReplacingDraft && playerState.TokensLeft <= 0 {
		eval.ErrorCode = "no_tokens_left"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
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

	if !room.State().CanAffordResources(playerID, eval.ResourceCost) {
		ctx.MissingResources = missingResources(playerState.Resources, eval.ResourceCost)
		eval.ErrorCode = "insufficient_resources"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}

	availablePoints := room.State().EnsurePointBudget(playerID)
	if !availablePoints.CanAfford(eval.PointCost) {
		ctx.MissingPoints = missingPoints(availablePoints, eval.PointCost)
		eval.ErrorCode = "insufficient_points"
		eval.FeedbackMessage, eval.FeedbackDetails = buildFeedback(room.State(), playerID, ctx, eval.ErrorCode)
		return eval
	}

	eval.OK = true
	return eval
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
