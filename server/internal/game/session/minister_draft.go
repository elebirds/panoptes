package session

import (
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/staticdata"
)

const domesticMinisterRole = "domestic"
const militaryMinisterRole = "military"

func (r *Runtime) PrepareMinisterDraftCacheForTurn(turn int) {
	if r == nil || r.state == nil || turn <= 0 {
		return
	}
	r.preparedMinisterDraftsMu.RLock()
	if _, ok := r.preparedMinisterDrafts[turn]; ok {
		r.preparedMinisterDraftsMu.RUnlock()
		return
	}
	r.preparedMinisterDraftsMu.RUnlock()

	draftsByPlayer := make(map[string][]domain.MinisterDraft, len(r.participants))
	for _, binding := range r.participants {
		playerID := strings.TrimSpace(binding.Participant.ID)
		if playerID == "" {
			continue
		}
		observation := r.BuildObservation(playerID)
		draftsByPlayer[playerID] = buildMinisterDraftsFromLegalCandidates(turn, playerID, r.state, observation)
	}

	r.preparedMinisterDraftsMu.Lock()
	r.preparedMinisterDrafts[turn] = draftsByPlayer
	for cachedTurn := range r.preparedMinisterDrafts {
		if cachedTurn >= turn-1 {
			continue
		}
		delete(r.preparedMinisterDrafts, cachedTurn)
	}
	r.preparedMinisterDraftsMu.Unlock()
}

func (r *Runtime) preparedMinisterDraftsForPlayer(turn int, playerID string) []domain.MinisterDraft {
	if r == nil || turn <= 0 || strings.TrimSpace(playerID) == "" {
		return nil
	}
	r.preparedMinisterDraftsMu.RLock()
	defer r.preparedMinisterDraftsMu.RUnlock()
	playerDrafts := r.preparedMinisterDrafts[turn][strings.TrimSpace(playerID)]
	if len(playerDrafts) == 0 {
		return nil
	}
	out := make([]domain.MinisterDraft, len(playerDrafts))
	copy(out, playerDrafts)
	return out
}

func buildMinisterDraftsFromIntents(turn int, playerID string, intents []planning.Intent) []domain.MinisterDraft {
	if len(intents) == 0 {
		return nil
	}

	out := make([]domain.MinisterDraft, 0, len(intents))
	for _, intent := range intents {
		draft, ok := ministerDraftFromIntent(turn, playerID, intent)
		if !ok {
			continue
		}
		out = append(out, draft)
	}
	return out
}

func ministerDraftFromIntent(turn int, playerID string, intent planning.Intent) (domain.MinisterDraft, bool) {
	switch typed := intent.(type) {
	case planning.SetResearchTargetIntent:
		targetID := strings.TrimSpace(typed.TechnologyID)
		if targetID == "" {
			return domain.MinisterDraft{}, false
		}
		return baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindResearch, targetID, technologyLabel(targetID)), true
	case planning.SetPolicyIntent:
		targetID := strings.TrimSpace(typed.NationalPolicyID)
		if targetID == "" {
			return domain.MinisterDraft{}, false
		}
		return baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindPolicy, targetID, policyLabel(targetID)), true
	case planning.SetInstitutionLoadoutIntent:
		institutionIDs := domain.NormalizeInstitutionIDList(typed.InstitutionIDs)
		if len(institutionIDs) == 0 {
			return domain.MinisterDraft{}, false
		}
		targetID := strings.Join(institutionIDs, ",")
		draft := baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindInstitution, targetID, institutionLabel(institutionIDs))
		draft.InstitutionIDs = institutionIDs
		return draft, true
	case planning.BuildStructureIntent:
		nodeID := strings.TrimSpace(typed.NodeID)
		buildingTypeID := strings.TrimSpace(typed.BuildingTypeID)
		if nodeID == "" || buildingTypeID == "" {
			return domain.MinisterDraft{}, false
		}
		targetID := nodeID + ":" + buildingTypeID
		cityID := strings.TrimSpace(typed.CityID)
		if cityID != "" {
			targetID += ":" + cityID
		}
		draft := baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindBuild, targetID, buildingLabel(buildingTypeID)+" @ "+nodeID)
		draft.NodeID = nodeID
		draft.BuildingTypeID = buildingTypeID
		draft.CityID = cityID
		return draft, true
	case planning.SetBuildingRecipeIntent:
		nodeID := strings.TrimSpace(typed.NodeID)
		recipeID := strings.TrimSpace(typed.RecipeID)
		if nodeID == "" || recipeID == "" {
			return domain.MinisterDraft{}, false
		}
		targetID := nodeID + ":" + recipeID
		draft := baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindRecipe, targetID, recipeLabel(recipeID)+" @ "+nodeID)
		draft.NodeID = nodeID
		draft.RecipeID = recipeID
		return draft, true
	case planning.IssueUnitOrderIntent:
		unitID := strings.TrimSpace(typed.UnitID)
		action := strings.TrimSpace(typed.Action)
		if unitID == "" || action == "" {
			return domain.MinisterDraft{}, false
		}
		role := militaryMinisterRole
		if gameorders.UnitAction(action) == gameorders.ActionSettleCity {
			role = domesticMinisterRole
		}
		targetID := unitID + ":" + action + ":" + strings.TrimSpace(typed.TargetNodeID) + ":" + strings.TrimSpace(typed.TargetUnitID) + ":" + strings.TrimSpace(typed.SecondaryNodeID)
		if pairs := sortedParamPairs(typed.Params); len(pairs) > 0 {
			targetID += ":" + strings.Join(pairs, ",")
		}
		draft := baseMinisterDraft(turn, playerID, role, domain.MinisterDraftKindUnitOrder, targetID, unitOrderLabel(typed))
		draft.UnitID = unitID
		draft.Action = action
		draft.TargetNodeID = strings.TrimSpace(typed.TargetNodeID)
		draft.TargetUnitID = strings.TrimSpace(typed.TargetUnitID)
		draft.SecondaryNodeID = strings.TrimSpace(typed.SecondaryNodeID)
		draft.Params = cloneStringMap(typed.Params)
		return draft, true
	default:
		return domain.MinisterDraft{}, false
	}
}

func baseMinisterDraft(turn int, playerID string, role string, kind domain.MinisterDraftKind, targetID string, targetLabel string) domain.MinisterDraft {
	targetID = strings.TrimSpace(targetID)
	targetLabel = strings.TrimSpace(targetLabel)
	if targetLabel == "" {
		targetLabel = targetID
	}
	title, summary, rationale, riskNote := ministerDraftText(role, string(kind), targetLabel)
	return domain.MinisterDraft{
		DraftID:      fmt.Sprintf("%s:%s:%s:%d", strings.TrimSpace(role), strings.TrimSpace(string(kind)), safeDraftIDPart(targetID), turn),
		PlayerID:     playerID,
		MinisterRole: role,
		Kind:         kind,
		TargetID:     targetID,
		TargetLabel:  targetLabel,
		Title:        title,
		Summary:      summary,
		Rationale:    rationale,
		RiskNote:     riskNote,
		Status:       domain.MinisterDraftStatusPending,
		Available:    true,
		Turn:         turn,
		Source:       domain.MinisterDraftSourceRuleOnly,
	}
}

func ministerDraftText(role string, kind string, targetLabel string) (string, string, string, string) {
	if strings.TrimSpace(role) == militaryMinisterRole {
		return "军事提案", "建议本回合执行：" + targetLabel + "。", "这是一项可批准的军事行动，大臣已将其整理为本回合命令。", "若你希望亲自调整部队命令，可以暂不采纳。"
	}
	switch strings.TrimSpace(kind) {
	case string(domain.MinisterDraftKindPolicy):
		return "国策提案", "建议本回合采纳：" + targetLabel + "。", "大臣认为该国策更能回应当前局势，并已整理为可批准提案。", "若你希望维持现行国策，可以暂不采纳。"
	case string(domain.MinisterDraftKindInstitution):
		return "制度提案", "建议启用：" + targetLabel + "。", "大臣已将当前制度选择整理为一项可批准调整。", "若你希望保持制度槽位不变，可以暂不采纳。"
	case string(domain.MinisterDraftKindBuild):
		return "建设提案", "建议建设：" + targetLabel + "。", "大臣已将该建设目标整理为本回合可批准工程。", "若你希望保留工业给其他项目，可以暂不采纳。"
	case string(domain.MinisterDraftKindRecipe):
		return "生产提案", "建议将生产切换为：" + targetLabel + "。", "大臣已将该生产调整整理为可批准提案。", "若你希望维持当前生产安排，可以暂不采纳。"
	case string(domain.MinisterDraftKindUnitOrder):
		return "行动提案", "建议执行：" + targetLabel + "。", "大臣已将该单位行动整理为本回合可批准命令。", "若你希望亲自调整单位命令，可以暂不采纳。"
	default:
		return "研究提案", "建议将 " + targetLabel + " 作为当前研究目标。", "大臣已将该研究方向整理为可批准提案。", "若你希望改选其他科技，可以暂不采纳。"
	}
}

func technologyLabel(technologyID string) string {
	if tech, ok := staticdata.Default().GetTechnology(strings.TrimSpace(technologyID)); ok && strings.TrimSpace(tech.Name) != "" {
		return strings.TrimSpace(tech.Name)
	}
	return strings.TrimSpace(technologyID)
}

func policyLabel(policyID string) string {
	if policy, ok := staticdata.Default().GetPolicy(strings.TrimSpace(policyID)); ok && strings.TrimSpace(policy.Name) != "" {
		return strings.TrimSpace(policy.Name)
	}
	return strings.TrimSpace(policyID)
}

func institutionLabel(institutionIDs []string) string {
	labels := make([]string, 0, len(institutionIDs))
	for _, institutionID := range institutionIDs {
		if institution, ok := staticdata.Default().GetInstitution(strings.TrimSpace(institutionID)); ok && strings.TrimSpace(institution.Name) != "" {
			labels = append(labels, strings.TrimSpace(institution.Name))
			continue
		}
		labels = append(labels, strings.TrimSpace(institutionID))
	}
	return strings.Join(labels, ", ")
}

func buildingLabel(buildingTypeID string) string {
	if building, ok := staticdata.Default().GetBuilding(strings.TrimSpace(buildingTypeID)); ok && strings.TrimSpace(building.Name) != "" {
		return strings.TrimSpace(building.Name)
	}
	return strings.TrimSpace(buildingTypeID)
}

func recipeLabel(recipeID string) string {
	if recipe, ok := staticdata.Default().GetRecipe(strings.TrimSpace(recipeID)); ok && strings.TrimSpace(recipe.Name) != "" {
		return strings.TrimSpace(recipe.Name)
	}
	return strings.TrimSpace(recipeID)
}

func unitOrderLabel(intent planning.IssueUnitOrderIntent) string {
	action := strings.TrimSpace(intent.Action)
	target := strings.TrimSpace(intent.TargetNodeID)
	if target == "" {
		target = strings.TrimSpace(intent.TargetUnitID)
	}
	if target == "" {
		return strings.TrimSpace(intent.UnitID) + " " + action
	}
	return strings.TrimSpace(intent.UnitID) + " " + action + " -> " + target
}

func safeDraftIDPart(value string) string {
	replacer := strings.NewReplacer(" ", "_", ":", "_", ",", "_", "/", "_", "\\", "_")
	return replacer.Replace(strings.TrimSpace(value))
}

func cloneStringMap(src map[string]string) map[string]string {
	if len(src) == 0 {
		return nil
	}
	dst := make(map[string]string, len(src))
	for key, value := range src {
		dst[key] = value
	}
	return dst
}
