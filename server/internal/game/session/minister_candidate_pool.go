package session

import (
	"fmt"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

const ministerOperationCandidateLimit = 8

type ministerOperationCandidate struct {
	key         string
	score       int
	role        string
	operationID string
	objective   string
	title       string
	summary     string
	rationale   string
	riskNote    string
	intents     []planning.IssueUnitOrderIntent
}

func buildMinisterDraftsFromLegalCandidates(turn int, playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []domain.MinisterDraft {
	intents := enumerateLegalMinisterCandidateIntents(playerID, state, observation)
	drafts := buildMinisterDraftsFromIntents(turn, playerID, intents)
	drafts = append(drafts, enumerateMinisterOperationDrafts(turn, playerID, state, observation)...)
	return drafts
}

func enumerateLegalMinisterCandidateIntents(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []planning.Intent {
	if state == nil || strings.TrimSpace(playerID) == "" {
		return nil
	}
	if observation == nil {
		observation = gamequery.NewObservationStore().BuildObservation(state, playerID)
	}
	var intents []planning.Intent
	intents = append(intents, enumerateResearchCandidateIntents(playerID, state)...)
	intents = append(intents, enumeratePolicyCandidateIntents(playerID, state)...)
	intents = append(intents, enumerateInstitutionCandidateIntents(playerID, state)...)
	intents = append(intents, enumerateBuildCandidateIntents(playerID, state, observation)...)
	intents = append(intents, enumerateRecipeCandidateIntents(playerID, state, observation)...)
	return dedupeCandidateIntents(intents)
}

func enumerateResearchCandidateIntents(playerID string, state *domain.GameState) []planning.Intent {
	intents := make([]planning.Intent, 0)
	for _, tech := range staticdata.Default().Technologies() {
		technologyID := strings.TrimSpace(tech.ID)
		if technologyID == "" {
			continue
		}
		if validation := economy.ValidateResearchTarget(state, playerID, technologyID); !validation.OK {
			continue
		}
		intents = append(intents, planning.SetResearchTargetIntent{TechnologyID: technologyID})
	}
	return intents
}

func enumeratePolicyCandidateIntents(playerID string, state *domain.GameState) []planning.Intent {
	playerState := state.Players[playerID]
	intents := make([]planning.Intent, 0)
	for _, policy := range staticdata.Default().Policies() {
		policyID := strings.TrimSpace(policy.ID)
		if policyID == "" {
			continue
		}
		if _, errCode := planning.ValidatePolicySelection(state, playerID, policyID, "national"); errCode != "" {
			continue
		}
		if playerState != nil && domain.Policy(policyID) == playerState.Policy {
			continue
		}
		intents = append(intents, planning.SetPolicyIntent{NationalPolicyID: policyID})
	}
	return intents
}

func enumerateInstitutionCandidateIntents(playerID string, state *domain.GameState) []planning.Intent {
	playerState := state.Players[playerID]
	if playerState == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	for _, institutionID := range playerState.Institutions.CandidateIDs() {
		institution, ok := staticdata.Default().GetInstitution(institutionID)
		if !ok {
			continue
		}
		selected := replaceInstitutionInCategory(playerState.Institutions.ActiveInstitutionIDs, institutionID, institution.Category)
		normalized, errCode := planning.ValidateInstitutionLoadout(state, playerID, playerState, selected)
		if errCode != "" || sameStringSet(normalized, playerState.Institutions.ActiveInstitutionIDs) {
			continue
		}
		intents = append(intents, planning.SetInstitutionLoadoutIntent{InstitutionIDs: normalized})
	}
	return intents
}

func replaceInstitutionInCategory(active []string, institutionID string, category string) []string {
	selected := make([]string, 0, len(active)+1)
	replaced := false
	for _, activeID := range active {
		activeInstitution, ok := staticdata.Default().GetInstitution(activeID)
		if ok && strings.EqualFold(activeInstitution.Category, category) {
			if !replaced {
				selected = append(selected, institutionID)
				replaced = true
			}
			continue
		}
		selected = append(selected, activeID)
	}
	if !replaced {
		selected = append(selected, institutionID)
	}
	return domain.NormalizeInstitutionIDList(selected)
}

func enumerateBuildCandidateIntents(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []planning.Intent {
	playerState := state.Players[playerID]
	if playerState == nil || observation == nil {
		return nil
	}
	cityIDs := candidateCityIDs(playerState)
	intents := make([]planning.Intent, 0)
	for _, node := range observation.VisibleNodes {
		if node == nil || strings.TrimSpace(node.GetBuildingTypeId()) != "" || strings.TrimSpace(node.GetTerritoryOwnerPlayerId()) != playerID {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		for _, building := range staticdata.Default().Buildings() {
			buildingID := strings.TrimSpace(building.ID)
			if buildingID == "" || domain.NormalizeBuildingScope(building.BuildingScope) == domain.BuildingScopeCityCore || !state.IsBuildingUnlocked(playerID, buildingID) {
				continue
			}
			for _, cityID := range cityIDs {
				if validation := economy.ValidateBuildOrder(state, playerID, nodeID, buildingID, cityID); !validation.OK {
					continue
				}
				intents = append(intents, planning.BuildStructureIntent{NodeID: nodeID, BuildingTypeID: buildingID, CityID: cityID})
			}
		}
	}
	return intents
}

func enumerateRecipeCandidateIntents(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []planning.Intent {
	if observation == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	for _, node := range observation.VisibleNodes {
		if node == nil || strings.TrimSpace(node.GetControllerPlayerId()) != playerID || strings.TrimSpace(node.GetBuildingTypeId()) == "" {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		building, ok := staticdata.Default().GetBuilding(strings.TrimSpace(node.GetBuildingTypeId()))
		if !ok {
			continue
		}
		currentRecipeID := ""
		if node.GetOperation() != nil {
			currentRecipeID = strings.TrimSpace(node.GetOperation().GetSelectedRecipeId())
		}
		for _, recipeID := range building.RecipeIDs {
			recipeID = strings.TrimSpace(recipeID)
			if recipeID == "" || recipeID == currentRecipeID {
				continue
			}
			if validation := economy.ValidateRecipeSelection(state, playerID, nodeID, recipeID); !validation.OK {
				continue
			}
			intents = append(intents, planning.SetBuildingRecipeIntent{NodeID: nodeID, RecipeID: recipeID})
		}
	}
	return intents
}

func enumerateMinisterOperationDrafts(turn int, playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []domain.MinisterDraft {
	if state == nil || observation == nil {
		return nil
	}
	candidates := make([]ministerOperationCandidate, 0)
	candidates = append(candidates, engageVisibleEnemyOperations(playerID, state, observation)...)
	candidates = append(candidates, developResourceOperations(playerID, state, observation)...)
	candidates = append(candidates, repairInfrastructureOperations(playerID, state, observation)...)
	candidates = append(candidates, scoutPressureOperations(playerID, state, observation)...)
	if len(candidates) == 0 {
		return nil
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	limit := ministerOperationCandidateLimit
	if limit > len(candidates) {
		limit = len(candidates)
	}
	drafts := make([]domain.MinisterDraft, 0, limit)
	for _, candidate := range candidates[:limit] {
		if draft, ok := ministerOperationDraftFromCandidate(turn, playerID, candidate); ok {
			drafts = append(drafts, draft)
		}
	}
	return drafts
}

func engageVisibleEnemyOperations(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []ministerOperationCandidate {
	out := make([]ministerOperationCandidate, 0)
	for _, enemy := range observation.Units {
		if enemy == nil || strings.TrimSpace(enemy.GetFaction()) == "" || strings.TrimSpace(enemy.GetFaction()) == playerID {
			continue
		}
		enemyID := strings.TrimSpace(enemy.GetId())
		enemyNodeID := nodeIDForProtoPosition(state, enemy.GetPos())
		intents := make([]planning.IssueUnitOrderIntent, 0, 3)
		for _, unit := range observation.Units {
			if len(intents) >= 3 || unit == nil || strings.TrimSpace(unit.GetFaction()) != playerID {
				continue
			}
			unitID := strings.TrimSpace(unit.GetId())
			if unitID == "" {
				continue
			}
			attack := planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionAttack), TargetUnitID: enemyID}
			if validateUnitOrderIntent(state, playerID, attack) == "" {
				intents = append(intents, attack)
				continue
			}
			if enemyNodeID != "" {
				move := planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionMove), TargetNodeID: enemyNodeID}
				if validateUnitOrderIntent(state, playerID, move) == "" {
					intents = append(intents, move)
				}
			}
		}
		if len(intents) == 0 {
			continue
		}
		out = append(out, ministerOperationCandidate{
			key:         "engage:" + enemyID,
			score:       300 + len(intents)*20,
			role:        militaryMinisterRole,
			operationID: "engage_" + safeDraftIDPart(enemyID),
			objective:   "压制可见敌军 " + enemyID,
			title:       "军事方案",
			summary:     "建议集中可用部队压制 " + enemyID + "。",
			rationale:   "该方案把可见敌军转化为少量批量命令，避免逐格列出所有战术可能。",
			riskNote:    "若敌军位置或单位状态变化，批准时会重新校验每条命令。",
			intents:     intents,
		})
	}
	return out
}

func developResourceOperations(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []ministerOperationCandidate {
	out := make([]ministerOperationCandidate, 0)
	for _, node := range observation.VisibleNodes {
		if node == nil || !node.GetIsResourcePoint() || strings.TrimSpace(node.GetBuildingTypeId()) != "" {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		if nodeID == "" {
			continue
		}
		for _, unit := range observation.Units {
			if unit == nil || strings.TrimSpace(unit.GetFaction()) != playerID {
				continue
			}
			intent := planning.IssueUnitOrderIntent{
				UnitID:       strings.TrimSpace(unit.GetId()),
				Action:       string(gameorders.ActionBuildImprovement),
				TargetNodeID: nodeID,
				Params: map[string]string{
					"building_type_id": defaultImprovementBuildingType(node.GetResourceType()),
				},
			}
			if validateUnitOrderIntent(state, playerID, intent) != "" {
				continue
			}
			out = append(out, ministerOperationCandidate{
				key:         "develop:" + nodeID,
				score:       230 + resourceOperationBonus(node),
				role:        domesticMinisterRole,
				operationID: "develop_" + safeDraftIDPart(nodeID),
				objective:   "开发资源点 " + nodeID,
				title:       "开发方案",
				summary:     "建议开发 " + nodeID + " 的资源点。",
				rationale:   "该方案把资源开发作为一个明确任务，而不是暴露每个单位的全部地图动作。",
				riskNote:    "资源点控制权或工程单位状态变化时，批准可能失败。",
				intents:     []planning.IssueUnitOrderIntent{intent},
			})
			break
		}
	}
	return out
}

func repairInfrastructureOperations(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []ministerOperationCandidate {
	out := make([]ministerOperationCandidate, 0)
	for _, node := range observation.VisibleNodes {
		if node == nil {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		if nodeID == "" {
			continue
		}
		action := ""
		score := 0
		switch {
		case node.GetHasRoad() && (strings.TrimSpace(node.GetRoadStatus()) == string(domain.RoadStatusDamaged) || strings.TrimSpace(node.GetRoadStatus()) == string(domain.RoadStatusDestroyed)):
			action = string(gameorders.ActionRepairRoad)
			score = 190
		case strings.TrimSpace(node.GetControllerPlayerId()) == playerID && strings.TrimSpace(node.GetBuildingTypeId()) != "" &&
			(strings.TrimSpace(node.GetBuildingStatus()) == domain.BuildingStatusRuined || strings.TrimSpace(node.GetBuildingStatus()) == domain.BuildingStatusDisabled):
			action = string(gameorders.ActionRepairImprovement)
			score = 210
		default:
			continue
		}
		for _, unit := range observation.Units {
			if unit == nil || strings.TrimSpace(unit.GetFaction()) != playerID {
				continue
			}
			intent := planning.IssueUnitOrderIntent{UnitID: strings.TrimSpace(unit.GetId()), Action: action, TargetNodeID: nodeID}
			if validateUnitOrderIntent(state, playerID, intent) != "" {
				continue
			}
			out = append(out, ministerOperationCandidate{
				key:         "repair:" + action + ":" + nodeID,
				score:       score,
				role:        domesticMinisterRole,
				operationID: "repair_" + safeDraftIDPart(nodeID),
				objective:   "修复 " + nodeID,
				title:       "修复方案",
				summary:     "建议优先修复 " + nodeID + "。",
				rationale:   "该方案把道路或设施修复折叠为一个可批准任务。",
				riskNote:    "如果目标已被修复或失去控制，批准时会被规则层拒绝。",
				intents:     []planning.IssueUnitOrderIntent{intent},
			})
			break
		}
	}
	return out
}

func scoutPressureOperations(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []ministerOperationCandidate {
	out := make([]ministerOperationCandidate, 0)
	for _, node := range observation.VisibleNodes {
		if node == nil || strings.TrimSpace(node.GetId()) == "" || strings.TrimSpace(node.GetBuildingTypeId()) != "" {
			continue
		}
		score := 40
		if node.GetEnemyUnitCount() > 0 {
			score += 120
		}
		if ownerKnownAgainstPlayer(node, playerID) {
			score += 60
		}
		if node.GetIsResourcePoint() {
			score += 25
		}
		if score < 80 {
			continue
		}
		intents := make([]planning.IssueUnitOrderIntent, 0, 2)
		for _, unit := range observation.Units {
			if len(intents) >= 2 || unit == nil || strings.TrimSpace(unit.GetFaction()) != playerID || strings.TrimSpace(unit.GetUnitType()) == string(domain.UnitTypeSettler) {
				continue
			}
			intent := planning.IssueUnitOrderIntent{UnitID: strings.TrimSpace(unit.GetId()), Action: string(gameorders.ActionMove), TargetNodeID: strings.TrimSpace(node.GetId())}
			if validateUnitOrderIntent(state, playerID, intent) == "" {
				intents = append(intents, intent)
			}
		}
		if len(intents) == 0 {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		out = append(out, ministerOperationCandidate{
			key:         "secure:" + nodeID,
			score:       score + len(intents)*10,
			role:        militaryMinisterRole,
			operationID: "secure_" + safeDraftIDPart(nodeID),
			objective:   "控制或侦察 " + nodeID,
			title:       "机动方案",
			summary:     "建议向 " + nodeID + " 机动，处理压力或争夺前沿。",
			rationale:   "该方案用少量单位形成明确战术任务，而不是列出整张地图的移动候选。",
			riskNote:    "机动会改变单位站位；批准前仍按当前地图重新校验。",
			intents:     intents,
		})
	}
	return out
}

func ministerOperationDraftFromCandidate(turn int, playerID string, candidate ministerOperationCandidate) (domain.MinisterDraft, bool) {
	if len(candidate.intents) == 0 {
		return domain.MinisterDraft{}, false
	}
	operationID := strings.TrimSpace(candidate.operationID)
	if operationID == "" {
		operationID = safeDraftIDPart(candidate.key)
	}
	role := strings.TrimSpace(candidate.role)
	if role == "" {
		role = militaryMinisterRole
	}
	objective := strings.TrimSpace(candidate.objective)
	if objective == "" {
		objective = operationID
	}
	draft := baseMinisterDraft(turn, playerID, role, domain.MinisterDraftKindOperation, operationID, objective)
	if strings.TrimSpace(candidate.title) != "" {
		draft.Title = strings.TrimSpace(candidate.title)
	}
	if strings.TrimSpace(candidate.summary) != "" {
		draft.Summary = strings.TrimSpace(candidate.summary)
	}
	if strings.TrimSpace(candidate.rationale) != "" {
		draft.Rationale = strings.TrimSpace(candidate.rationale)
	}
	if strings.TrimSpace(candidate.riskNote) != "" {
		draft.RiskNote = strings.TrimSpace(candidate.riskNote)
	}
	draft.OperationID = operationID
	draft.Objective = strings.TrimSpace(candidate.objective)
	draft.OperationSteps = make([]domain.MinisterDraft, 0, len(candidate.intents))
	for idx, intent := range candidate.intents {
		step, ok := ministerDraftFromIntent(turn, playerID, intent)
		if !ok {
			continue
		}
		step.DraftID = draft.DraftID + ":step_" + fmt.Sprint(idx+1)
		step.MinisterRole = role
		step.Source = domain.MinisterDraftSourceRuleOnly
		draft.OperationSteps = append(draft.OperationSteps, step)
	}
	return draft, len(draft.OperationSteps) > 0
}

func validateUnitOrderIntent(state *domain.GameState, playerID string, intent planning.IssueUnitOrderIntent) string {
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          strings.TrimSpace(intent.UnitID),
		Action:          gameorders.UnitAction(strings.TrimSpace(intent.Action)),
		TargetNodeID:    strings.TrimSpace(intent.TargetNodeID),
		TargetUnitID:    strings.TrimSpace(intent.TargetUnitID),
		SecondaryNodeID: strings.TrimSpace(intent.SecondaryNodeID),
		Params:          cloneStringMap(intent.Params),
	}
	return gameorders.ValidatePlanningUnitOrder(state, playerID, order)
}

func nodeIDForProtoPosition(state *domain.GameState, pos *pb.Position) string {
	if state == nil || pos == nil {
		return ""
	}
	entry, ok := domain.GetNodeAt(state.World, domain.Position{Q: int(pos.GetQ()), R: int(pos.GetR())})
	if !ok || entry == nil {
		return ""
	}
	return strings.TrimSpace(domain.NodeC.Get(entry).ID)
}

func resourceOperationBonus(node *pb.NodeView) int {
	switch strings.TrimSpace(node.GetResourceType()) {
	case string(domain.ResourceFood):
		return 30
	case string(domain.ResourceOre), string(domain.ResourceWood):
		return 20
	default:
		return 0
	}
}

func ownerKnownAgainstPlayer(node *pb.NodeView, playerID string) bool {
	if node == nil {
		return false
	}
	if owner := strings.TrimSpace(node.GetControllerPlayerId()); owner != "" && owner != playerID {
		return true
	}
	if owner := strings.TrimSpace(node.GetTerritoryOwnerPlayerId()); owner != "" && owner != playerID {
		return true
	}
	return false
}

func candidateCityIDs(playerState *domain.PlayerState) []string {
	ids := []string{""}
	if playerState == nil {
		return ids
	}
	for _, city := range playerState.Cities {
		if city == nil {
			continue
		}
		cityID := strings.TrimSpace(city.CityID)
		if cityID != "" {
			ids = append(ids, cityID)
		}
	}
	return ids
}

func defaultImprovementBuildingType(resourceType string) string {
	switch strings.TrimSpace(resourceType) {
	case "food":
		return "farm"
	case "ore":
		return "mine"
	case "wood":
		return "lumber"
	default:
		return ""
	}
}

func sameStringSet(left []string, right []string) bool {
	left = domain.NormalizePolicyIDList(left)
	right = domain.NormalizePolicyIDList(right)
	if len(left) != len(right) {
		return false
	}
	for idx := range left {
		if left[idx] != right[idx] {
			return false
		}
	}
	return true
}

func dedupeCandidateIntents(intents []planning.Intent) []planning.Intent {
	if len(intents) == 0 {
		return nil
	}
	seen := make(map[string]struct{}, len(intents))
	out := make([]planning.Intent, 0, len(intents))
	for _, intent := range intents {
		key := candidateIntentKey(intent)
		if key == "" {
			continue
		}
		if _, ok := seen[key]; ok {
			continue
		}
		seen[key] = struct{}{}
		out = append(out, intent)
	}
	return out
}

func candidateIntentKey(intent planning.Intent) string {
	switch typed := intent.(type) {
	case planning.SetResearchTargetIntent:
		return "research:" + strings.TrimSpace(typed.TechnologyID)
	case planning.SetPolicyIntent:
		return "policy:" + strings.TrimSpace(typed.NationalPolicyID)
	case planning.SetInstitutionLoadoutIntent:
		return "institution:" + strings.Join(domain.NormalizeInstitutionIDList(typed.InstitutionIDs), ",")
	case planning.BuildStructureIntent:
		return "build:" + strings.TrimSpace(typed.NodeID) + ":" + strings.TrimSpace(typed.BuildingTypeID) + ":" + strings.TrimSpace(typed.CityID)
	case planning.SetBuildingRecipeIntent:
		return "recipe:" + strings.TrimSpace(typed.NodeID) + ":" + strings.TrimSpace(typed.RecipeID)
	case planning.IssueUnitOrderIntent:
		return "unit:" + strings.TrimSpace(typed.UnitID) + ":" + strings.TrimSpace(typed.Action) + ":" + strings.TrimSpace(typed.TargetNodeID) + ":" + strings.TrimSpace(typed.TargetUnitID) + ":" + strings.TrimSpace(typed.SecondaryNodeID) + ":" + strings.Join(sortedParamPairs(typed.Params), ",")
	default:
		return ""
	}
}

func sortedParamPairs(params map[string]string) []string {
	if len(params) == 0 {
		return nil
	}
	keys := make([]string, 0, len(params))
	for key := range params {
		keys = append(keys, key)
	}
	// domain.NormalizePolicyIDList sorts and trims strings; reuse for a small deterministic list.
	keys = domain.NormalizePolicyIDList(keys)
	out := make([]string, 0, len(keys))
	for _, key := range keys {
		out = append(out, key+"="+strings.TrimSpace(params[key]))
	}
	return out
}
