// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现规划输入模块的服务编排逻辑。

package planning

import (
	"context"
	"encoding/json"
	"errors"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	coretransport "github.com/elebirds/panoptes/internal/transport"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type Session interface {
	State() *domain.GameState
	Submit(playerID string)
	SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error
	IsDevMode() bool
	QueueBuildOrder(order domain.BuildOrder)
	QueueRecipeSelection(order domain.RecipeSelectionOrder)
	SetMinisterDirective(playerID string, directive string)
	SetWarDirectives(playerID string, directives []domain.WarZoneDirective)
	SetUnitOrder(order gameorders.UnitOrder)
	CancelUnitOrder(playerID string, unitID string)
	SendPlanningSnapshot(ctx context.Context, playerID string) error
	BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView
	NodeByID(nodeID string) (*donburi.Entry, bool)
}

type Service struct{}

func (s *Service) Enter(room Session) {
	if room == nil || room.State() == nil {
		return
	}
	room.State().TurnRuntime.Planning.EnsureDraftMaps()
}

func (s *Service) HandleCommand(room Session, inbound cmddispatch.InboundContext, cmd *pb.PlanningCommand) error {
	playerID := inbound.PlayerID
	state := room.State()
	if state == nil {
		return errors.New("state is nil")
	}
	playerState, ok := state.Players[playerID]
	if !ok || playerState == nil {
		return errors.New("player not found")
	}
	if cmd == nil || cmd.Body == nil {
		return errors.New("planning command is nil")
	}
	eventCtx := coretransport.ContextWithEventMeta(context.Background(), coretransport.EventMetaFromInbound(inbound))

	switch body := cmd.Body.(type) {
	case *pb.PlanningCommand_SetPolicy:
		msg := body.SetPolicy
		policyID := strings.TrimSpace(msg.GetNationalPolicyId())
		if policyID == "" {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: "invalid_request"})
			return nil
		}
		if _, ok := staticdata.Default().GetPolicy(policyID); !ok {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgSetPolicyResult{Success: false, NationalPolicyId: policyID, ErrorCode: "invalid_target"})
			return nil
		}
		_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgSetPolicyResult{Success: true, NationalPolicyId: policyID})
		state.TurnRuntime.Planning.SetPendingPolicy(playerID, domain.Policy(policyID))
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		return nil
	case *pb.PlanningCommand_BuildStructure:
		msg := body.BuildStructure
		return s.handleBuildRequest(eventCtx, room, playerID, playerState, msg.GetNodeId(), msg.GetBuildingTypeId(), msg.GetCityId())
	case *pb.PlanningCommand_RevealNode:
		msg := body.RevealNode
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		nodeView := room.BuildNodeViewForPlayer(msg.GetNodeId(), playerID)
		if nodeView == nil {
			_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		playerState.TokensLeft--
		_ = room.SendToPlayer(eventCtx, playerID, &pb.MsgRevealResult{NodeId: msg.GetNodeId(), TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
		return nil
	case *pb.PlanningCommand_SetResearchTarget:
		msg := body.SetResearchTarget
		return s.handleResearchRequest(eventCtx, room, playerID, playerState, strings.TrimSpace(msg.GetTechnologyId()))
	case *pb.PlanningCommand_SetBuildingRecipe:
		msg := body.SetBuildingRecipe
		return s.handleSetBuildingRecipe(eventCtx, room, playerID, strings.TrimSpace(msg.GetNodeId()), strings.TrimSpace(msg.GetRecipeId()))
	case *pb.PlanningCommand_SetMinisterDirective:
		msg := body.SetMinisterDirective
		room.SetMinisterDirective(playerID, msg.GetContent())
		return nil
	case *pb.PlanningCommand_SetWarZone:
		msg := body.SetWarZone
		updated := false
		for _, zone := range playerState.WarZones {
			if zone.ID == msg.GetZoneId() {
				zone.Name = msg.GetName()
				zone.NodeIDs = msg.GetNodeIds()
				updated = true
				break
			}
		}
		if !updated {
			playerState.WarZones = append(playerState.WarZones, &domain.WarZone{ID: msg.GetZoneId(), Name: msg.GetName(), NodeIDs: msg.GetNodeIds()})
		}
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		return nil
	case *pb.PlanningCommand_WarZoneDirective:
		msg := body.WarZoneDirective
		directives := append([]domain.WarZoneDirective(nil), state.TurnRuntime.Planning.WarDirectives[playerID]...)
		directives = append(directives, domain.WarZoneDirective{ZoneID: msg.GetZoneId(), Directive: msg.GetDirective(), TargetNode: msg.GetTargetNode()})
		room.SetWarDirectives(playerID, directives)
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		return nil
	case *pb.PlanningCommand_IssueUnitOrder:
		msg := body.IssueUnitOrder
		order := gameorders.UnitOrder{
			PlayerID:        playerID,
			UnitID:          strings.TrimSpace(msg.GetUnitId()),
			Action:          gameorders.UnitAction(strings.TrimSpace(msg.GetAction())),
			TargetNodeID:    strings.TrimSpace(msg.GetTargetNodeId()),
			TargetUnitID:    strings.TrimSpace(msg.GetTargetUnitId()),
			SecondaryNodeID: strings.TrimSpace(msg.GetSecondaryNodeId()),
			Params:          cloneParams(msg.GetParams()),
		}
		room.SetUnitOrder(order)
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		return nil
	case *pb.PlanningCommand_CancelUnitOrder:
		msg := body.CancelUnitOrder
		room.CancelUnitOrder(playerID, strings.TrimSpace(msg.GetUnitId()))
		_ = room.SendPlanningSnapshot(eventCtx, playerID)
		return nil
	case *pb.PlanningCommand_PlanningPathPreviewRequest:
		msg := body.PlanningPathPreviewRequest
		_ = room.SendToPlayer(eventCtx, playerID, buildPlanningPathPreviewResponse(room.State(), playerID, msg))
		return nil
	case *pb.PlanningCommand_SubmitTurn:
		room.Submit(playerID)
		return nil
	}

	return nil
}

func cloneParams(src map[string]string) map[string]string {
	if len(src) == 0 {
		return nil
	}
	dst := make(map[string]string, len(src))
	for k, v := range src {
		dst[k] = v
	}
	return dst
}

func (s *Service) handleResearchRequest(ctx context.Context, room Session, playerID string, playerState *domain.PlayerState, technologyID string) error {
	if playerState == nil || technologyID == "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_request"})
		return nil
	}

	state := room.State()
	tech, ok := staticdata.Default().GetTechnology(technologyID)
	if !ok {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_target"})
		return nil
	}
	if playerState.Research.HasTechnology(technologyID) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_directive"})
		return nil
	}
	for _, prereq := range tech.Prerequisites {
		if prereq.Type != "technology_unlocked" {
			continue
		}
		if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
			_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_directive"})
			return nil
		}
	}

	state.TurnRuntime.Planning.SetPendingResearchTarget(playerID, technologyID)
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return nil
}

func (s *Service) handleSetBuildingRecipe(ctx context.Context, room Session, playerID string, nodeID string, recipeID string) error {
	if nodeID == "" || recipeID == "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_request"})
		return nil
	}
	nodeEntry, ok := room.NodeByID(nodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
		return nil
	}
	recipe, ok := staticdata.Default().GetRecipe(recipeID)
	if !ok {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
		return nil
	}
	building := ecs.BuildingC.Get(nodeEntry)
	node := ecs.NodeC.Get(nodeEntry)
	if normalizeToken(building.Owner) != normalizeToken(playerID) && normalizeToken(node.Owner) != normalizeToken(playerID) && normalizeToken(node.TerritoryOwner) != normalizeToken(playerID) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "unauthorized"})
		return nil
	}
	cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
	if !ok {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
		return nil
	}
	allowed := false
	for _, candidate := range cfg.RecipeIDs {
		if candidate == recipeID {
			allowed = true
			break
		}
	}
	if !allowed || recipe.BuildingID != string(building.Type) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_directive"})
		return nil
	}
	if !room.State().IsRecipeUnlocked(playerID, recipeID) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_directive"})
		return nil
	}
	room.QueueRecipeSelection(domain.RecipeSelectionOrder{PlayerID: playerID, NodeID: nodeID, RecipeID: recipeID})
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgSetBuildingRecipeResult{Success: true, NodeId: nodeID, RecipeId: recipeID})
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return nil
}

func (s *Service) handleBuildRequest(ctx context.Context, room Session, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, cityID string) error {
	if playerState == nil {
		return errors.New("player not found")
	}
	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	cityID = strings.TrimSpace(cityID)
	replacingExistingDraft := room.State().TurnRuntime.Planning.HasBuildOrder(playerID, nodeID)

	if !replacingExistingDraft && playerState.TokensLeft <= 0 {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "no_tokens_left"})
		return nil
	}
	nodeEntry, ok := room.NodeByID(nodeID)
	if !ok {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "invalid_target"})
		return nil
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "building_exists"})
		return nil
	}

	cfg, ok := staticdata.Default().GetBuilding(buildingType)
	if !ok {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "invalid_target"})
		return nil
	}
	if !room.State().IsBuildingUnlocked(playerID, buildingType) {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "invalid_directive"})
		return nil
	}

	if errCode := ecs.ValidateBuildingPlacement(room.State(), nodeEntry, playerID, cfg, cityID); errCode != "" {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: errCode})
		return nil
	}

	cost, err := domain.ResourceBagFromAmounts(cfg.ResourceCosts)
	if err != nil {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "invalid_directive"})
		return nil
	}
	if !room.State().CanAffordResources(playerID, cost) && !room.IsDevMode() {
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: false, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID, ErrorCode: "insufficient_resources"})
		return nil
	}

	room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CityID: cityID})
	_ = room.SendToPlayer(ctx, playerID, &pb.MsgBuildStructureResult{Success: true, NodeId: nodeID, BuildingTypeId: buildingType, CityId: cityID})
	if !replacingExistingDraft {
		playerState.TokensLeft--
		_ = room.SendToPlayer(ctx, playerID, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
	}
	_ = room.SendPlanningSnapshot(ctx, playerID)
	return nil
}

func normalizeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
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

func findPreviewUnit(state *domain.GameState, unitID string, ownerID string) (*donburi.Entry, bool) {
	if state == nil || state.World == nil {
		return nil, false
	}
	var found *donburi.Entry
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if found != nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.ID == unitID && stats.Faction == ownerID {
			found = entry
		}
	})
	return found, found != nil
}

type expandPayload struct {
	UnitID       string `json:"unit_id"`
	CenterNodeID string `json:"center_node_id"`
}

func MarshalExpandParams(unitID, centerNodeID string) ([]byte, error) {
	return json.Marshal(expandPayload{UnitID: unitID, CenterNodeID: centerNodeID})
}
