package planning

import (
	"fmt"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gamefeedback "github.com/elebirds/panoptes/internal/game/feedback"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type feedbackContext struct {
	NodeID               string
	BuildingTypeID       string
	RecipeID             string
	CityID               string
	RequiredResourceType string
	NodeResourceType     string
	MissingResources     domain.ResourceBag
	MissingPoints        domain.PointBag
}

func buildFeedback(state *domain.GameState, playerID string, ctx feedbackContext, code string) (string, []*pb.FeedbackDetail) {
	ctx = enrichBuildFeedbackContext(state, ctx)
	return buildFeedbackMessage(state, playerID, ctx, code), feedbackDetails(ctx)
}

func demolishFeedback(state *domain.GameState, playerID string, ctx feedbackContext, code string) (string, []*pb.FeedbackDetail) {
	ctx = enrichDemolishFeedbackContext(state, ctx)
	return demolishFeedbackMessage(state, playerID, ctx, code), feedbackDetails(ctx)
}

func recipeFeedback(state *domain.GameState, playerID string, ctx feedbackContext, code string) (string, []*pb.FeedbackDetail) {
	ctx = enrichRecipeFeedbackContext(state, ctx)
	return recipeFeedbackMessage(state, playerID, ctx, code), feedbackDetails(ctx)
}

func runtimeReasonMessage(reason string) string {
	return gamefeedback.RuntimeReasonMessage(reason)
}

func RuntimeReasonMessage(reason string) string {
	return gamefeedback.RuntimeReasonMessage(reason)
}

func buildSkippedReasonMessage(reason string) string {
	return gamefeedback.BuildReasonMessage(reason)
}

func BuildReasonMessage(reason string) string {
	return gamefeedback.BuildReasonMessage(reason)
}

func recipeReasonMessage(reason string) string {
	return gamefeedback.RecipeReasonMessage(reason)
}

func RecipeReasonMessage(reason string) string {
	return gamefeedback.RecipeReasonMessage(reason)
}

func enrichBuildFeedbackContext(state *domain.GameState, ctx feedbackContext) feedbackContext {
	ctx.NodeID = strings.TrimSpace(ctx.NodeID)
	ctx.BuildingTypeID = strings.TrimSpace(ctx.BuildingTypeID)
	ctx.CityID = strings.TrimSpace(ctx.CityID)
	if ctx.BuildingTypeID != "" {
		if cfg, ok := staticdata.Default().GetBuilding(ctx.BuildingTypeID); ok {
			if ctx.RequiredResourceType == "" {
				ctx.RequiredResourceType = strings.TrimSpace(cfg.RequiredResourceType)
			}
		}
	}
	if state == nil || ctx.NodeID == "" {
		return ctx
	}
	entry, ok := state.GetNode(ctx.NodeID)
	if !ok || entry == nil {
		return ctx
	}
	node := ecs.NodeC.Get(entry)
	if ctx.NodeResourceType == "" {
		ctx.NodeResourceType = strings.TrimSpace(node.ResourceType)
	}
	return ctx
}

func enrichDemolishFeedbackContext(state *domain.GameState, ctx feedbackContext) feedbackContext {
	ctx.NodeID = strings.TrimSpace(ctx.NodeID)
	if state == nil || ctx.NodeID == "" {
		return ctx
	}
	entry, ok := state.GetNode(ctx.NodeID)
	if !ok || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return ctx
	}
	ctx.BuildingTypeID = strings.TrimSpace(string(ecs.BuildingC.Get(entry).Type))
	if ctx.CityID == "" {
		ctx.CityID = strings.TrimSpace(building.ResolveCityID(entry))
	}
	return ctx
}

func enrichRecipeFeedbackContext(state *domain.GameState, ctx feedbackContext) feedbackContext {
	ctx.NodeID = strings.TrimSpace(ctx.NodeID)
	ctx.RecipeID = strings.TrimSpace(ctx.RecipeID)
	if state == nil || ctx.NodeID == "" {
		return ctx
	}
	entry, ok := state.GetNode(ctx.NodeID)
	if !ok || entry == nil {
		return ctx
	}
	if ctx.BuildingTypeID == "" && entry.HasComponent(ecs.BuildingC) {
		ctx.BuildingTypeID = strings.TrimSpace(string(ecs.BuildingC.Get(entry).Type))
	}
	if ctx.CityID == "" {
		ctx.CityID = strings.TrimSpace(building.ResolveCityID(entry))
	}
	return ctx
}

func buildFeedbackMessage(state *domain.GameState, playerID string, ctx feedbackContext, code string) string {
	switch strings.TrimSpace(code) {
	case "invalid_request":
		return "建造指令不完整，请重新选择建筑、节点和所属城市。"
	case "invalid_target":
		return "目标节点或所属城市无效，无法提交建造。"
	case "unauthorized":
		return "所属城市不归你控制，无法以它作为建造归属。"
	case "invalid_directive":
		return describeBuildDirectiveViolation(state, playerID, ctx.BuildingTypeID, ctx.CityID)
	case "building_exists":
		return "该节点已经有建筑，不能重复建造。"
	case "outside_territory":
		return "该节点不在你的有效辖区内，当前不能建造。"
	case "terrain_not_buildable":
		return "该地形不能建造这类建筑。"
	case "resource_only_required":
		return "这类建筑只能建在资源点上。"
	case "resource_type_mismatch":
		return "该资源点类型与建筑要求不匹配。"
	case "insufficient_resources":
		return "资源不足，无法提交这条建造。"
	case "insufficient_points":
		return "工业点数不足，无法提交这条建造。"
	default:
		return ""
	}
}

func demolishFeedbackMessage(state *domain.GameState, playerID string, ctx feedbackContext, code string) string {
	switch strings.TrimSpace(code) {
	case "invalid_request":
		return "拆除指令不完整，请重新选择建筑。"
	case "invalid_target":
		return "目标节点无效，无法拆除。"
	case "unauthorized":
		return "这座建筑不归你控制，无法拆除。"
	case "invalid_directive":
		return describeDemolishDirectiveViolation(state, playerID, ctx.BuildingTypeID)
	default:
		return ""
	}
}

func recipeFeedbackMessage(state *domain.GameState, playerID string, ctx feedbackContext, code string) string {
	switch strings.TrimSpace(code) {
	case "invalid_request":
		return "配方设置指令不完整，请重新选择建筑和配方。"
	case "invalid_target":
		return describeRecipeTargetViolation(state, ctx.NodeID, ctx.RecipeID)
	case "unauthorized":
		return "这座建筑不归你控制，无法设置配方。"
	case "invalid_directive":
		return describeRecipeDirectiveViolation(state, playerID, ctx.NodeID, ctx.RecipeID)
	default:
		return recipeReasonMessage(code)
	}
}

func describeBuildDirectiveViolation(state *domain.GameState, playerID string, buildingTypeID string, cityID string) string {
	buildingTypeID = strings.TrimSpace(buildingTypeID)
	if cfg, ok := staticdata.Default().GetBuilding(buildingTypeID); ok {
		if domain.NormalizeBuildingScope(cfg.BuildingScope) == domain.BuildingScopeCityCore {
			return "城市核心不能通过普通建造指令直接放置。"
		}
	}
	if state != nil && buildingTypeID != "" && !state.IsBuildingUnlocked(playerID, buildingTypeID) {
		return "该建筑尚未解锁，当前不能建造。"
	}
	if state != nil && strings.TrimSpace(cityID) != "" {
		if _, cityState, errCode := building.ResolveCityContext(state, playerID, cityID); errCode == "" && !domain.IsCityOnline(state, cityState) {
			return "所属城市当前尚未上线，暂时不能建造。"
		}
	}
	return "当前不能提交这条建造指令。"
}

func describeRecipeTargetViolation(state *domain.GameState, nodeID string, recipeID string) string {
	if state == nil {
		return "目标建筑或配方无效，无法设置配方。"
	}
	nodeID = strings.TrimSpace(nodeID)
	recipeID = strings.TrimSpace(recipeID)
	if nodeID == "" || recipeID == "" {
		return "目标建筑或配方无效，无法设置配方。"
	}
	entry, ok := state.GetNode(nodeID)
	if !ok || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return "目标建筑不存在，无法设置配方。"
	}
	if _, ok := staticdata.Default().GetRecipe(recipeID); !ok {
		return "目标配方不存在，无法设置配方。"
	}
	return "目标建筑或配方无效，无法设置配方。"
}

func describeRecipeDirectiveViolation(state *domain.GameState, playerID string, nodeID string, recipeID string) string {
	if state == nil {
		return "当前不能设置这个配方。"
	}
	recipeID = strings.TrimSpace(recipeID)
	if recipeID != "" && !state.IsRecipeUnlocked(playerID, recipeID) {
		return "该配方尚未解锁，当前不能设置。"
	}
	nodeID = strings.TrimSpace(nodeID)
	entry, ok := state.GetNode(nodeID)
	if !ok || entry == nil || !entry.HasComponent(ecs.BuildingC) {
		return "目标建筑不存在，无法设置配方。"
	}
	buildingComp := ecs.BuildingC.Get(entry)
	recipe, recipeOK := staticdata.Default().GetRecipe(recipeID)
	if !recipeOK {
		return "目标配方不存在，无法设置配方。"
	}
	cfg, ok := staticdata.Default().GetBuilding(string(buildingComp.Type))
	if !ok {
		return "当前不能设置这个配方。"
	}
	allowed := false
	for _, candidate := range cfg.RecipeIDs {
		if candidate == recipeID {
			allowed = true
			break
		}
	}
	if !allowed || recipe.BuildingID != string(buildingComp.Type) {
		return "该配方不属于这座建筑，无法设置。"
	}
	return "当前不能设置这个配方。"
}

func describeDemolishDirectiveViolation(state *domain.GameState, playerID string, buildingTypeID string) string {
	buildingTypeID = strings.TrimSpace(buildingTypeID)
	if buildingTypeID == "" {
		return "当前不能拆除这座建筑。"
	}
	if cfg, ok := staticdata.Default().GetBuilding(buildingTypeID); ok && domain.NormalizeBuildingScope(cfg.BuildingScope) == domain.BuildingScopeCityCore {
		return "城市核心不能拆除。"
	}
	return "当前不能拆除这座建筑。"
}

func feedbackDetails(ctx feedbackContext) []*pb.FeedbackDetail {
	details := make([]*pb.FeedbackDetail, 0, 8)
	appendDetail := func(key string, value string) {
		details = append(details, &pb.FeedbackDetail{
			Key:   key,
			Value: strings.TrimSpace(value),
		})
	}

	appendDetail("node_id", ctx.NodeID)
	appendDetail("building_type_id", ctx.BuildingTypeID)
	appendDetail("recipe_id", ctx.RecipeID)
	appendDetail("city_id", ctx.CityID)

	if ctx.RequiredResourceType != "" {
		appendDetail("required_resource_type", ctx.RequiredResourceType)
	}
	if ctx.NodeResourceType != "" {
		appendDetail("node_resource_type", ctx.NodeResourceType)
	}

	resourceKeys := ctx.MissingResources.Keys()
	sort.Slice(resourceKeys, func(i, j int) bool { return resourceKeys[i] < resourceKeys[j] })
	for _, key := range resourceKeys {
		appendDetail("missing_resource."+string(key), fmt.Sprintf("%d", ctx.MissingResources.Get(key)))
	}

	pointKeys := ctx.MissingPoints.Keys()
	sort.Slice(pointKeys, func(i, j int) bool { return pointKeys[i] < pointKeys[j] })
	for _, key := range pointKeys {
		appendDetail("missing_point."+string(key), fmt.Sprintf("%d", ctx.MissingPoints.Get(key)))
	}

	return details
}

func missingResources(available domain.ResourceBag, cost domain.ResourceBag) domain.ResourceBag {
	if len(cost) == 0 {
		return domain.NewResourceBag()
	}
	out := domain.NewResourceBag()
	for _, key := range cost.Keys() {
		missing := cost.Get(key) - available.Get(key)
		if missing > 0 {
			out.Set(key, missing)
		}
	}
	return out
}

func missingPoints(available domain.PointBag, cost domain.PointBag) domain.PointBag {
	if len(cost) == 0 {
		return domain.NewPointBag()
	}
	out := domain.NewPointBag()
	for _, key := range cost.Keys() {
		missing := cost.Get(key) - available.Get(key)
		if missing > 0 {
			out.Set(key, missing)
		}
	}
	return out
}

func recipeFeedbackContextForEntry(entry *donburi.Entry, recipeID string) feedbackContext {
	ctx := feedbackContext{RecipeID: strings.TrimSpace(recipeID)}
	if entry == nil {
		return ctx
	}
	ctx.NodeID = strings.TrimSpace(ecs.NodeC.Get(entry).ID)
	if entry.HasComponent(ecs.BuildingC) {
		ctx.BuildingTypeID = strings.TrimSpace(string(ecs.BuildingC.Get(entry).Type))
	}
	ctx.CityID = strings.TrimSpace(building.ResolveCityID(entry))
	return ctx
}
