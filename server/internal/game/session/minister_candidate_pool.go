package session

import (
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/economy"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
)

func buildMinisterDraftsFromLegalCandidates(turn int, playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []domain.MinisterDraft {
	intents := enumerateLegalMinisterCandidateIntents(playerID, state, observation)
	return buildMinisterDraftsFromIntents(turn, playerID, intents)
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
	intents = append(intents, enumerateUnitOrderCandidateIntents(playerID, state, observation)...)
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
	if playerState == nil || playerState.Institutions.SlotCount <= 0 {
		return nil
	}
	validPolicyIDs := make([]string, 0)
	for _, policyID := range playerState.Institutions.CandidateIDs() {
		if _, errCode := planning.ValidatePolicySelection(state, playerID, policyID, "institutional"); errCode != "" {
			continue
		}
		if !playerState.Institutions.HasCandidate(policyID) {
			continue
		}
		validPolicyIDs = append(validPolicyIDs, policyID)
	}
	if len(validPolicyIDs) == 0 {
		return nil
	}
	limit := playerState.Institutions.SlotCount
	if limit > len(validPolicyIDs) {
		limit = len(validPolicyIDs)
	}
	intents := make([]planning.Intent, 0)
	var walk func(start int, picked []string)
	walk = func(start int, picked []string) {
		if len(picked) > 0 {
			normalized, errCode := planning.ValidateInstitutionLoadout(state, playerID, playerState, picked)
			if errCode == "" && !sameStringSet(normalized, playerState.Institutions.ActivePolicyIDs) {
				intents = append(intents, planning.SetInstitutionLoadoutIntent{PolicyIDs: normalized})
			}
		}
		if len(picked) >= limit {
			return
		}
		for idx := start; idx < len(validPolicyIDs); idx++ {
			next := append(append([]string(nil), picked...), validPolicyIDs[idx])
			walk(idx+1, next)
		}
	}
	walk(0, nil)
	return intents
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

func enumerateUnitOrderCandidateIntents(playerID string, state *domain.GameState, observation *gamequery.ObservationSnapshot) []planning.Intent {
	if observation == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	for _, unit := range observation.Units {
		if unit == nil || strings.TrimSpace(unit.GetFaction()) != playerID {
			continue
		}
		unitID := strings.TrimSpace(unit.GetId())
		if unitID == "" {
			continue
		}
		intents = appendIfValidUnitIntent(intents, state, playerID, planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionHold)})
		if strings.TrimSpace(unit.GetUnitType()) == string(domain.UnitTypeSettler) {
			intents = appendIfValidUnitIntent(intents, state, playerID, planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionSettleCity)})
		}
		for _, node := range observation.VisibleNodes {
			if node == nil {
				continue
			}
			nodeID := strings.TrimSpace(node.GetId())
			if nodeID == "" {
				continue
			}
			for _, intent := range unitNodeCandidateIntents(playerID, unitID, nodeID, node) {
				intents = appendIfValidUnitIntent(intents, state, playerID, intent)
			}
		}
		for _, target := range observation.Units {
			if target == nil || strings.TrimSpace(target.GetFaction()) == playerID {
				continue
			}
			targetID := strings.TrimSpace(target.GetId())
			if targetID == "" {
				continue
			}
			intents = appendIfValidUnitIntent(intents, state, playerID, planning.IssueUnitOrderIntent{
				UnitID:       unitID,
				Action:       string(gameorders.ActionAttack),
				TargetUnitID: targetID,
			})
		}
	}
	return intents
}

func unitNodeCandidateIntents(playerID string, unitID string, nodeID string, node *pb.NodeView) []planning.IssueUnitOrderIntent {
	intents := []planning.IssueUnitOrderIntent{
		{UnitID: unitID, Action: string(gameorders.ActionMove), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionAttack), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionBuildRoad), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionRepairRoad), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionDestroyRoad), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionRepairImprovement), TargetNodeID: nodeID},
		{UnitID: unitID, Action: string(gameorders.ActionRaidStorage), TargetNodeID: nodeID},
	}
	if node.GetIsResourcePoint() {
		intents = append(intents, planning.IssueUnitOrderIntent{
			UnitID:       unitID,
			Action:       string(gameorders.ActionBuildImprovement),
			TargetNodeID: nodeID,
			Params: map[string]string{
				"building_type_id": defaultImprovementBuildingType(node.GetResourceType()),
			},
		})
	}
	return intents
}

func appendIfValidUnitIntent(intents []planning.Intent, state *domain.GameState, playerID string, intent planning.IssueUnitOrderIntent) []planning.Intent {
	order := gameorders.UnitOrder{
		PlayerID:        playerID,
		UnitID:          strings.TrimSpace(intent.UnitID),
		Action:          gameorders.UnitAction(strings.TrimSpace(intent.Action)),
		TargetNodeID:    strings.TrimSpace(intent.TargetNodeID),
		TargetUnitID:    strings.TrimSpace(intent.TargetUnitID),
		SecondaryNodeID: strings.TrimSpace(intent.SecondaryNodeID),
		Params:          cloneStringMap(intent.Params),
	}
	if errCode := gameorders.ValidatePlanningUnitOrder(state, playerID, order); errCode != "" {
		return intents
	}
	return append(intents, intent)
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
		return "institution:" + strings.Join(domain.NormalizePolicyIDList(typed.PolicyIDs), ",")
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
