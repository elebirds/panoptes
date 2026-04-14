// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现规划输入模块的服务编排逻辑。

package planning

import (
	"encoding/json"
	"errors"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/combat"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

type Session interface {
	State() *domain.GameState
	Submit(playerID string)
	SendToPlayer(playerID string, msg proto.Message) error
	IsDevMode() bool
	QueueBuildOrder(order domain.BuildOrder)
	QueueResearchOrder(order domain.ResearchOrder)
	QueueRecipeSelection(order domain.RecipeSelectionOrder)
	SetMinisterDirective(playerID string, directive string)
	SetWarDirectives(playerID string, directives []domain.WarZoneDirective)
	SetUnitOrder(order gameorders.UnitOrder)
	CancelUnitOrder(playerID string, unitID string)
	SendPlanningSnapshot(playerID string) error
	BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView
	NodeByID(nodeID string) (*donburi.Entry, bool)
}

type Service struct{}

func (s *Service) Enter(room Session) {
	if room == nil || room.State() == nil {
		return
	}
	planning := &room.State().TurnRuntime.Planning
	if planning.MinisterDirectives == nil {
		planning.MinisterDirectives = make(map[string]string)
	}
	if planning.WarDirectives == nil {
		planning.WarDirectives = make(map[string][]domain.WarZoneDirective)
	}
	if planning.UnitOrders == nil {
		planning.UnitOrders = make(map[string]domain.UnitDirective)
	}
}

func (s *Service) HandleMessage(room Session, playerID string, msgType string, payload []byte) error {
	state := room.State()
	if state == nil {
		return errors.New("state is nil")
	}
	playerState, ok := state.Players[playerID]
	if !ok || playerState == nil {
		return errors.New("player not found")
	}

	switch msgType {
	case "MsgSetPolicy":
		msg := &pb.MsgSetPolicy{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		playerState.Policy = domain.Policy(msg.GetPolicy())
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "set_policy", TokensLeft: int32(playerState.TokensLeft)})
		return nil
	case "MsgBuildStructure":
		msg := &pb.MsgBuildStructure{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		return s.handleBuildRequest(room, playerID, playerState, msg.GetNodeId(), msg.GetBuildingType(), msg.GetCastleId())
	case "MsgRevealNode":
		msg := &pb.MsgRevealNode{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_request"})
			return err
		}
		if playerState.TokensLeft <= 0 {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
			return nil
		}
		nodeView := room.BuildNodeViewForPlayer(msg.GetNodeId(), playerID)
		if nodeView == nil {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "reveal", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
			return nil
		}
		playerState.TokensLeft--
		_ = room.SendToPlayer(playerID, &pb.MsgRevealResult{NodeId: msg.GetNodeId(), TrueState: nodeView, TokensLeft: int32(playerState.TokensLeft)})
		return nil
	case "MsgSetResearchTarget":
		msg := &pb.MsgSetResearchTarget{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: "", ErrorCode: "invalid_request"})
			return err
		}
		return s.handleResearchRequest(room, playerID, playerState, strings.TrimSpace(msg.GetTechnologyId()))
	case "MsgSetBuildingRecipe":
		msg := &pb.MsgSetBuildingRecipe{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, ErrorCode: "invalid_request"})
			return err
		}
		return s.handleSetBuildingRecipe(room, playerID, strings.TrimSpace(msg.GetNodeId()), strings.TrimSpace(msg.GetRecipeId()))
	case "MsgSetMinisterDirective":
		msg := &pb.MsgSetMinisterDirective{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		room.SetMinisterDirective(playerID, msg.GetContent())
		return nil
	case "MsgSetWarZone":
		msg := &pb.MsgSetWarZone{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
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
		_ = room.SendPlanningSnapshot(playerID)
		return nil
	case "MsgWarZoneDirective":
		msg := &pb.MsgWarZoneDirective{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		directives := append([]domain.WarZoneDirective(nil), state.TurnRuntime.Planning.WarDirectives[playerID]...)
		directives = append(directives, domain.WarZoneDirective{ZoneID: msg.GetZoneId(), Directive: msg.GetDirective(), TargetNode: msg.GetTargetNode()})
		room.SetWarDirectives(playerID, directives)
		_ = room.SendPlanningSnapshot(playerID)
		return nil
	case "MsgIssueUnitOrder":
		msg := &pb.MsgIssueUnitOrder{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
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
		_ = room.SendPlanningSnapshot(playerID)
		return nil
	case "MsgCancelUnitOrder":
		msg := &pb.MsgCancelUnitOrder{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		room.CancelUnitOrder(playerID, strings.TrimSpace(msg.GetUnitId()))
		_ = room.SendPlanningSnapshot(playerID)
		return nil
	case "MsgPlanningPathPreviewRequest":
		msg := &pb.MsgPlanningPathPreviewRequest{}
		if err := protojson.Unmarshal(payload, msg); err != nil {
			return err
		}
		_ = room.SendToPlayer(playerID, buildPlanningPathPreviewResponse(room.State(), playerID, msg))
		return nil
	case "MsgSubmitTurn":
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

func (s *Service) handleResearchRequest(room Session, playerID string, playerState *domain.PlayerState, technologyID string) error {
	if playerState == nil || technologyID == "" {
		_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_request"})
		return nil
	}

	state := room.State()
	tech, ok := staticdata.Default().GetTechnology(technologyID)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_target"})
		return nil
	}
	if playerState.Research.HasTechnology(technologyID) {
		_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_directive"})
		return nil
	}
	for _, order := range state.TurnRuntime.Planning.ResearchOrders {
		if order.PlayerID == playerID && order.TechnologyID == technologyID {
			_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_directive"})
			return nil
		}
	}
	if playerState.Research.TechPoints < tech.TechPointCost {
		_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "insufficient_resources"})
		return nil
	}
	for _, prereq := range tech.Prerequisites {
		if prereq.Type != "technology_unlocked" {
			continue
		}
		if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
			_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: false, TechnologyId: technologyID, ErrorCode: "invalid_directive"})
			return nil
		}
	}

	room.QueueResearchOrder(domain.ResearchOrder{PlayerID: playerID, TechnologyID: technologyID})
	_ = room.SendToPlayer(playerID, &pb.MsgResearchResult{Success: true, TechnologyId: technologyID})
	return nil
}

func (s *Service) handleSetBuildingRecipe(room Session, playerID string, nodeID string, recipeID string) error {
	if nodeID == "" || recipeID == "" {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_request"})
		return nil
	}
	nodeEntry, ok := room.NodeByID(nodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
		return nil
	}
	recipe, ok := staticdata.Default().GetRecipe(recipeID)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
		return nil
	}
	building := ecs.BuildingC.Get(nodeEntry)
	node := ecs.NodeC.Get(nodeEntry)
	if normalizeToken(building.Owner) != normalizeToken(playerID) && normalizeToken(node.Owner) != normalizeToken(playerID) && normalizeToken(node.TerritoryOwner) != normalizeToken(playerID) {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "unauthorized"})
		return nil
	}
	cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_target"})
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
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_directive"})
		return nil
	}
	if !room.State().IsRecipeUnlocked(playerID, recipeID) {
		_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: false, NodeId: nodeID, RecipeId: recipeID, ErrorCode: "invalid_directive"})
		return nil
	}
	room.QueueRecipeSelection(domain.RecipeSelectionOrder{PlayerID: playerID, NodeID: nodeID, RecipeID: recipeID})
	_ = room.SendToPlayer(playerID, &pb.MsgSetBuildingRecipeResult{Success: true, NodeId: nodeID, RecipeId: recipeID})
	return nil
}

func (s *Service) handleBuildRequest(room Session, playerID string, playerState *domain.PlayerState, nodeID string, buildingType string, castleID string) error {
	if playerState == nil {
		return errors.New("player not found")
	}
	nodeID = strings.TrimSpace(nodeID)
	buildingType = strings.TrimSpace(buildingType)
	castleID = strings.TrimSpace(castleID)

	if playerState.TokensLeft <= 0 {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "no_tokens_left"})
		return nil
	}
	nodeEntry, ok := room.NodeByID(nodeID)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return nil
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "building_exists"})
		return nil
	}

	cfg, ok := staticdata.Default().GetBuilding(buildingType)
	if !ok {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_target"})
		return nil
	}
	if !room.State().IsBuildingUnlocked(playerID, buildingType) {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_directive"})
		return nil
	}
	if castleID != "" {
		if errCode := validateCastleContext(room, playerID, castleID); errCode != "" {
			_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: errCode})
			return nil
		}
	}

	nodeComp := ecs.NodeC.Get(nodeEntry)
	if errCode := validateBuildPlacement(nodeComp, cfg, playerID); errCode != "" {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: errCode})
		return nil
	}

	cost, err := domain.ResourceBagFromAmounts(cfg.BuildCost)
	if err != nil {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "invalid_directive"})
		return nil
	}
	if !room.State().CanAffordFromCastle(playerID, castleID, cost) && !room.IsDevMode() {
		_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: false, Action: "build", TokensLeft: int32(playerState.TokensLeft), ErrorCode: "insufficient_resources"})
		return nil
	}

	room.QueueBuildOrder(domain.BuildOrder{PlayerID: playerID, NodeID: nodeID, BuildingType: buildingType, CastleID: castleID})
	playerState.TokensLeft--
	_ = room.SendToPlayer(playerID, &pb.MsgTokenResult{Success: true, Action: "build", TokensLeft: int32(playerState.TokensLeft)})
	return nil
}

func validateCastleContext(room Session, playerID string, castleID string) string {
	castleID = strings.TrimSpace(castleID)
	if castleID == "" {
		return "invalid_request"
	}

	castleEntry, ok := room.NodeByID(castleID)
	if !ok || !castleEntry.HasComponent(ecs.BuildingC) {
		return "invalid_target"
	}
	building := ecs.BuildingC.Get(castleEntry)
	if normalizeToken(string(building.Type)) != "castle" {
		return "invalid_target"
	}
	node := ecs.NodeC.Get(castleEntry)
	player := normalizeToken(playerID)
	if normalizeToken(building.Owner) != player && normalizeToken(node.Owner) != player && normalizeToken(node.TerritoryOwner) != player {
		return "unauthorized"
	}
	return ""
}

func validateBuildPlacement(node *ecs.NodeComp, cfg staticdata.BuildingDefinition, playerID string) string {
	if node == nil {
		return "invalid_target"
	}
	terrainID := normalizeToken(string(node.Terrain))
	if terrainID != "" {
		if terrain, ok := staticdata.Default().GetTerrain(terrainID); ok && !terrain.Buildable {
			return "terrain_not_buildable"
		}
	}
	rule := normalizeToken(cfg.PlacementRule)
	switch rule {
	case "city_only":
		player := normalizeToken(playerID)
		territoryOwner := normalizeToken(node.TerritoryOwner)
		owner := normalizeToken(node.Owner)
		if territoryOwner != player && owner != player {
			return "outside_territory"
		}
	case "resource_only":
		if !node.IsResource {
			return "resource_only_required"
		}
		required := normalizeToken(cfg.RequiredResourceType)
		if required != "" && normalizeToken(node.ResourceType) != required {
			return "resource_type_mismatch"
		}
	}
	return ""
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
