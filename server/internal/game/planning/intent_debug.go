package planning

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/game/participant"
)

type DebugIntentRecord struct {
	Source        string
	ActorLabel    string
	IntentType    string
	IntentLabel   string
	ActionSummary string
	Summary       string
	Fields        map[string]any
}

func DebugIntentRecordFor(kind participant.Kind, participantID string, intent Intent) DebugIntentRecord {
	record := DebugIntentRecord{
		Source:      debugIntentSource(kind),
		ActorLabel:  debugActorLabel(kind, participantID),
		IntentType:  "unknown",
		IntentLabel: "未知操作",
		Fields:      map[string]any{},
	}

	switch typed := intent.(type) {
	case SetPolicyIntent:
		record.IntentType = "set_policy"
		record.IntentLabel = "设置国策"
		record.ActionSummary = joinNonEmpty("设置国策", strings.TrimSpace(typed.NationalPolicyID))
		record.Fields = map[string]any{
			"policy_id": strings.TrimSpace(typed.NationalPolicyID),
		}
	case SetInstitutionLoadoutIntent:
		record.IntentType = "set_institution_loadout"
		record.IntentLabel = "设置制度装配"
		cardIDs := append([]string(nil), typed.InstitutionIDs...)
		record.ActionSummary = fmt.Sprintf("设置制度装配 [%s]", strings.Join(cardIDs, ", "))
		record.Fields = map[string]any{
			"card_ids": cardIDs,
		}
	case BuildStructureIntent:
		record.IntentType = "build_structure"
		record.IntentLabel = "建造建筑"
		record.ActionSummary = buildStructureSummary(strings.TrimSpace(typed.NodeID), strings.TrimSpace(typed.BuildingTypeID))
		record.Fields = map[string]any{
			"node_id":       strings.TrimSpace(typed.NodeID),
			"building_type": strings.TrimSpace(typed.BuildingTypeID),
		}
	case DemolishBuildingIntent:
		record.IntentType = "demolish_building"
		record.IntentLabel = "拆除建筑"
		record.ActionSummary = joinNonEmpty("拆除建筑", strings.TrimSpace(typed.NodeID))
		record.Fields = map[string]any{
			"node_id": strings.TrimSpace(typed.NodeID),
		}
	case RevealNodeIntent:
		record.IntentType = "reveal_node"
		record.IntentLabel = "侦察节点"
		record.ActionSummary = joinNonEmpty("侦察节点", strings.TrimSpace(typed.NodeID))
		record.Fields = map[string]any{
			"node_id": strings.TrimSpace(typed.NodeID),
		}
	case SetResearchTargetIntent:
		record.IntentType = "set_research_target"
		record.IntentLabel = "设置科研目标"
		record.ActionSummary = joinNonEmpty("设置科研目标", strings.TrimSpace(typed.TechnologyID))
		record.Fields = map[string]any{
			"technology_id": strings.TrimSpace(typed.TechnologyID),
		}
	case SetBuildingRecipeIntent:
		record.IntentType = "set_building_recipe"
		record.IntentLabel = "设置建筑配方"
		record.ActionSummary = buildingRecipeSummary(strings.TrimSpace(typed.NodeID), strings.TrimSpace(typed.RecipeID))
		record.Fields = map[string]any{
			"node_id":   strings.TrimSpace(typed.NodeID),
			"recipe_id": strings.TrimSpace(typed.RecipeID),
		}
	case CancelBuildingRecipeIntent:
		record.IntentType = "cancel_building_recipe"
		record.IntentLabel = "取消建筑配方"
		record.ActionSummary = joinNonEmpty("取消建筑配方", strings.TrimSpace(typed.NodeID))
		record.Fields = map[string]any{
			"node_id": strings.TrimSpace(typed.NodeID),
		}
	case SetMinisterDirectiveIntent:
		record.IntentType = "set_minister_directive"
		record.IntentLabel = "处理大臣草案"
		record.ActionSummary = joinNonEmpty("处理大臣草案", strings.TrimSpace(typed.MinisterRole), strings.TrimSpace(typed.DirectiveType), strings.TrimSpace(typed.DraftID))
		record.Fields = map[string]any{
			"directive_type": strings.TrimSpace(typed.DirectiveType),
			"candidate_id":   strings.TrimSpace(typed.CandidateID),
		}
	case IssueUnitOrderIntent:
		record.IntentType = "issue_unit_order"
		record.IntentLabel = "下达单位指令"
		record.ActionSummary = unitOrderSummary(typed)
		record.Fields = map[string]any{
			"unit_id":        strings.TrimSpace(typed.UnitID),
			"directive_type": strings.TrimSpace(typed.Action),
			"target_node_id": strings.TrimSpace(typed.TargetNodeID),
		}
	case CancelUnitOrderIntent:
		record.IntentType = "cancel_unit_order"
		record.IntentLabel = "取消单位指令"
		record.ActionSummary = fmt.Sprintf("取消单位 %s 的指令", strings.TrimSpace(typed.UnitID))
		record.Fields = map[string]any{
			"unit_id": strings.TrimSpace(typed.UnitID),
		}
	case SubmitTurnIntent:
		record.IntentType = "submit_turn"
		record.IntentLabel = "结束回合"
		record.ActionSummary = "结束回合"
	case nil:
		record.IntentType = "nil"
		record.IntentLabel = "空操作"
		record.ActionSummary = "提交空操作"
	}

	record.Summary = joinNonEmpty(record.ActorLabel, record.ActionSummary)
	return record
}

func (r DebugIntentRecord) AttemptSummary() string {
	return joinNonEmpty(r.ActorLabel, "尝试"+strings.TrimSpace(r.ActionSummary))
}

func (r DebugIntentRecord) ResultSummary(success bool) string {
	resultLabel := "失败"
	if success {
		resultLabel = "成功"
	}
	return fmt.Sprintf("%s，结果：%s", joinNonEmpty(r.ActorLabel, r.ActionSummary), resultLabel)
}

func (r DebugIntentRecord) FieldAttrs() []any {
	keys := []string{
		"policy_id",
		"card_ids",
		"node_id",
		"building_type",
		"technology_id",
		"recipe_id",
		"unit_id",
		"directive_type",
		"candidate_id",
		"target_node_id",
	}
	attrs := make([]any, 0, len(keys)*2)
	for _, key := range keys {
		value, ok := r.Fields[key]
		if !ok {
			continue
		}
		switch typed := value.(type) {
		case string:
			if typed == "" {
				continue
			}
		case []string:
			if len(typed) == 0 {
				continue
			}
		}
		attrs = append(attrs, key, value)
	}
	return attrs
}

func debugIntentSource(kind participant.Kind) string {
	if kind == participant.KindBot || kind == participant.KindAI {
		return "ai"
	}
	return "player"
}

func debugActorLabel(kind participant.Kind, participantID string) string {
	if kind == participant.KindBot || kind == participant.KindAI {
		return fmt.Sprintf("AI[%s]", participantID)
	}
	return fmt.Sprintf("玩家[%s]", participantID)
}

func joinNonEmpty(parts ...string) string {
	filtered := make([]string, 0, len(parts))
	for _, part := range parts {
		part = strings.TrimSpace(part)
		if part == "" {
			continue
		}
		filtered = append(filtered, part)
	}
	return strings.Join(filtered, " ")
}

func buildStructureSummary(nodeID string, buildingType string) string {
	switch {
	case nodeID != "" && buildingType != "":
		return fmt.Sprintf("在 %s 建造 %s", nodeID, buildingType)
	case buildingType != "":
		return fmt.Sprintf("建造 %s", buildingType)
	default:
		return joinNonEmpty("建造建筑", nodeID)
	}
}

func buildingRecipeSummary(nodeID string, recipeID string) string {
	switch {
	case nodeID != "" && recipeID != "":
		return fmt.Sprintf("将 %s 配方设为 %s", nodeID, recipeID)
	case nodeID != "":
		return fmt.Sprintf("设置 %s 的建筑配方", nodeID)
	default:
		return joinNonEmpty("设置建筑配方", recipeID)
	}
}

func unitOrderSummary(intent IssueUnitOrderIntent) string {
	unitID := strings.TrimSpace(intent.UnitID)
	action := strings.TrimSpace(intent.Action)
	targetNodeID := strings.TrimSpace(intent.TargetNodeID)
	targetUnitID := strings.TrimSpace(intent.TargetUnitID)

	switch {
	case unitID != "" && action != "" && targetNodeID != "":
		return fmt.Sprintf("命令 %s 执行 %s 到 %s", unitID, action, targetNodeID)
	case unitID != "" && action != "" && targetUnitID != "":
		return fmt.Sprintf("命令 %s 对 %s 执行 %s", unitID, targetUnitID, action)
	case unitID != "" && action != "":
		return fmt.Sprintf("命令 %s 执行 %s", unitID, action)
	default:
		return joinNonEmpty("下达单位指令", unitID, action)
	}
}
