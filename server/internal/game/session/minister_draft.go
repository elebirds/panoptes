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
		return "军事建议", "建议本回合执行：" + targetLabel + "。", "该方案基于规则规划器当前的战术评估生成。", "如果你想手动下达军事命令，可以不采纳这条建议。"
	}
	switch strings.TrimSpace(kind) {
	case string(domain.MinisterDraftKindPolicy):
		return "国策建议", targetLabel + " 更符合当前局势。", "这条建议来自规则规划器对国策收益的评估。", "如果你想保持当前国策不变，可以不采纳这条建议。"
	case string(domain.MinisterDraftKindInstitution):
		return "制度建议", "建议启用：" + targetLabel + "。", "该搭配使用了本回合当前可用的较优制度组合。", "如果你想保持制度槽位不变，可以不采纳这条建议。"
	case string(domain.MinisterDraftKindBuild):
		return "建设建议", "建议建设：" + targetLabel + "。", "该建设方案从当前可见且已控制的节点与已解锁建筑中筛选得出。", "如果你想把工业留给其他建设项目，可以不采纳这条建议。"
	case string(domain.MinisterDraftKindRecipe):
		return "生产建议", "建议将生产切换为：" + targetLabel + "。", "该配方从建筑当前可用的生产选项中筛选得出。", "如果你想保持当前生产不变，可以不采纳这条建议。"
	case string(domain.MinisterDraftKindUnitOrder):
		return "扩张建议", "建议执行：" + targetLabel + "。", "这条命令有助于本回合的民用扩张推进。", "如果你想手动移动单位，可以不采纳这条建议。"
	default:
		return "研究建议", targetLabel + " 是当前优先研究目标。", "这条建议来自规则规划器对科研收益的评估。", "如果你想改选其他科技，可以不采纳这条建议。"
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
