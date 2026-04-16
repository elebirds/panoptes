package economy

import (
	"strings"

	"github.com/elebirds/panoptes/internal/building"
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

type ValidationResult struct {
	OK        bool
	ErrorCode string
}

// 这组 validator 是 planning 与 settlement 共享的“单一规则真相”。
// planning 用它们给即时反馈，结算期再用同一套规则做最终复核，
// 避免出现“下单成功但 settlement 静默吞掉”的口径漂移。
type ResearchTargetValidation struct {
	ValidationResult
	Technology staticdata.TechnologyDefinition
}

type BuildOrderValidation struct {
	ValidationResult
	NodeEntry *donburi.Entry
	Building  staticdata.BuildingDefinition
}

type RecipeSelectionValidation struct {
	ValidationResult
	NodeEntry *donburi.Entry
	Building  ecs.BuildingComp
	Recipe    staticdata.RecipeDefinition
}

func ValidateResearchTarget(state *domain.GameState, playerID string, technologyID string) ResearchTargetValidation {
	if state == nil || strings.TrimSpace(playerID) == "" || strings.TrimSpace(technologyID) == "" {
		return ResearchTargetValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_request"}}
	}
	playerState := state.Players[playerID]
	if playerState == nil {
		return ResearchTargetValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_request"}}
	}
	technology, ok := staticdata.Default().GetTechnology(technologyID)
	if !ok {
		return ResearchTargetValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	if playerState.Research.HasCompletedTechnology(technologyID) || playerState.Research.HasTechnology(technologyID) {
		return ResearchTargetValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_directive"}}
	}
	if errCode := validatePrerequisites(state, playerID, technology.Prerequisites); errCode != "" {
		return ResearchTargetValidation{ValidationResult: ValidationResult{ErrorCode: errCode}}
	}
	return ResearchTargetValidation{
		ValidationResult: ValidationResult{OK: true},
		Technology:       technology,
	}
}

func ValidateBuildOrder(state *domain.GameState, playerID string, nodeID string, buildingType string, cityID string) BuildOrderValidation {
	if state == nil || strings.TrimSpace(playerID) == "" || strings.TrimSpace(nodeID) == "" || strings.TrimSpace(buildingType) == "" {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_request"}}
	}
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	if nodeEntry.HasComponent(ecs.BuildingC) {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: "building_exists"}}
	}
	cfg, ok := staticdata.Default().GetBuilding(buildingType)
	if !ok {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	if !state.IsBuildingUnlocked(playerID, buildingType) {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_directive"}}
	}
	if errCode := building.ValidatePlacement(state, nodeEntry, playerID, cfg, cityID); errCode != "" {
		return BuildOrderValidation{ValidationResult: ValidationResult{ErrorCode: errCode}}
	}
	return BuildOrderValidation{
		ValidationResult: ValidationResult{OK: true},
		NodeEntry:        nodeEntry,
		Building:         cfg,
	}
}

func ValidateRecipeSelection(state *domain.GameState, playerID string, nodeID string, recipeID string) RecipeSelectionValidation {
	if state == nil || strings.TrimSpace(playerID) == "" || strings.TrimSpace(nodeID) == "" || strings.TrimSpace(recipeID) == "" {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_request"}}
	}
	nodeEntry, ok := state.GetNode(nodeID)
	if !ok || !nodeEntry.HasComponent(ecs.BuildingC) {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	recipe, ok := staticdata.Default().GetRecipe(recipeID)
	if !ok {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	building := ecs.BuildingC.Get(nodeEntry)
	if normalizeToken(building.Owner) != normalizeToken(playerID) {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "unauthorized"}}
	}
	if !state.IsRecipeUnlocked(playerID, recipeID) {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_directive"}}
	}
	cfg, ok := staticdata.Default().GetBuilding(string(building.Type))
	if !ok {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_target"}}
	}
	allowed := false
	for _, candidate := range cfg.RecipeIDs {
		if candidate == recipeID {
			allowed = true
			break
		}
	}
	if !allowed || recipe.BuildingID != string(building.Type) {
		return RecipeSelectionValidation{ValidationResult: ValidationResult{ErrorCode: "invalid_directive"}}
	}
	// 到这里为止，已经同时保证了：
	// 1. 玩家对建筑有配方控制权；
	// 2. recipe 已解锁；
	// 3. recipe 确实属于该建筑类型。
	return RecipeSelectionValidation{
		ValidationResult: ValidationResult{OK: true},
		NodeEntry:        nodeEntry,
		Building:         *building,
		Recipe:           recipe,
	}
}

func validatePrerequisites(state *domain.GameState, playerID string, prerequisites []staticdata.Prerequisite) string {
	if state == nil {
		return "invalid_target"
	}
	// 这里的 prerequisite 仍然是内容数据层语义。
	// 例如 technology_unlocked 指“科技已经 active/unlocked”，并不是旧的 settlement 事件名字。
	for _, prereq := range prerequisites {
		switch prereq.Type {
		case "technology_unlocked":
			if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
				return "invalid_directive"
			}
		case "policy_active":
			if !state.IsPolicyActive(playerID, prereq.TargetID) {
				return "invalid_directive"
			}
		}
	}
	return ""
}

func normalizeToken(value string) string {
	return strings.ToLower(strings.TrimSpace(value))
}
