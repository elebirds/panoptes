package session

import (
	"context"
	"fmt"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/ai"
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
	jobs := make([]preparedMinisterDraftPolishJob, 0, len(r.participants))
	for _, binding := range r.participants {
		playerID := strings.TrimSpace(binding.Participant.ID)
		if playerID == "" {
			continue
		}
		observation := r.BuildObservation(playerID)
		req := ai.Request{
			Participant: binding.Participant,
			State:       r.state,
			Observation: observation,
		}
		intents, _ := (ai.RuleBotProvider{}).BuildPlanningIntents(context.Background(), req)
		drafts := buildMinisterDraftsFromIntents(turn, playerID, intents)
		draftsByPlayer[playerID] = drafts
		for _, draft := range drafts {
			r.RecordMinisterMemory(playerID, draft.MinisterRole, ministerengine.MemoryEntry{
				Turn:       turn,
				Type:       "draft",
				Content:    fmt.Sprintf("%s:%s", draft.Kind, strings.TrimSpace(draft.TargetLabel)),
				Outcome:    "generated",
				PlayerResp: "pending",
			})
			if r.ministerEngine == nil || draft.MinisterRole != domesticMinisterRole {
				continue
			}
			jobs = append(jobs, preparedMinisterDraftPolishJob{
				Turn:     turn,
				PlayerID: playerID,
				Draft:    draft,
				Input: ministerengine.DraftPromptInput{
					Turn:               turn,
					PlayerID:           playerID,
					ObservationSummary: buildMinisterObservationSummary(r.state, observation),
					CurrentPolicy:      currentPolicyValue(r.state, playerID),
					CurrentResearch:    currentResearchValue(r.state, playerID),
				},
			})
		}
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

	for _, job := range jobs {
		go r.polishPreparedMinisterDraft(job)
	}
}

func (r *Runtime) ApplyPreparedMinisterDrafts(turn int) {
	if r == nil || r.state == nil || turn <= 0 || r.state.Turn != turn {
		return
	}
	if len(r.state.TurnRuntime.Planning.MinisterDrafts) > 0 {
		return
	}
	r.preparedMinisterDraftsMu.RLock()
	_, ok := r.preparedMinisterDrafts[turn]
	r.preparedMinisterDraftsMu.RUnlock()
	if !ok {
		r.PrepareMinisterDraftCacheForTurn(turn)
	}

	r.state.TurnRuntime.Planning.EnsureDraftMaps()
	clear(r.state.TurnRuntime.Planning.MinisterDrafts)
	r.preparedMinisterDraftsMu.RLock()
	for playerID, drafts := range r.preparedMinisterDrafts[turn] {
		r.state.TurnRuntime.Planning.SetMinisterDrafts(playerID, drafts)
	}
	r.preparedMinisterDraftsMu.RUnlock()
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
		policyIDs := domain.NormalizePolicyIDList(typed.PolicyIDs)
		if len(policyIDs) == 0 {
			return domain.MinisterDraft{}, false
		}
		targetID := strings.Join(policyIDs, ",")
		draft := baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindInstitution, targetID, institutionLabel(policyIDs))
		draft.PolicyIDs = policyIDs
		return draft, true
	case planning.BuildStructureIntent:
		nodeID := strings.TrimSpace(typed.NodeID)
		buildingTypeID := strings.TrimSpace(typed.BuildingTypeID)
		if nodeID == "" || buildingTypeID == "" {
			return domain.MinisterDraft{}, false
		}
		targetID := nodeID + ":" + buildingTypeID
		draft := baseMinisterDraft(turn, playerID, domesticMinisterRole, domain.MinisterDraftKindBuild, targetID, buildingLabel(buildingTypeID)+" @ "+nodeID)
		draft.NodeID = nodeID
		draft.BuildingTypeID = buildingTypeID
		draft.CityID = strings.TrimSpace(typed.CityID)
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
		targetID := unitID + ":" + action + ":" + strings.TrimSpace(typed.TargetNodeID) + ":" + strings.TrimSpace(typed.TargetUnitID)
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
		return "Military recommendation", "Execute " + targetLabel + " this turn.", "This plan is generated from the rule planner's current tactical evaluation.", "Reject it if you want to issue military orders manually."
	}
	switch strings.TrimSpace(kind) {
	case string(domain.MinisterDraftKindPolicy):
		return "Policy recommendation", targetLabel + " fits the current situation.", "This recommendation comes from the rule planner's policy evaluation.", "Reject it if you want to keep the current national policy."
	case string(domain.MinisterDraftKindInstitution):
		return "Institution recommendation", "Activate " + targetLabel + ".", "This loadout uses the strongest available institutional policies for this turn.", "Reject it if you want to keep institution slots unchanged."
	case string(domain.MinisterDraftKindBuild):
		return "Construction recommendation", "Build " + targetLabel + ".", "This construction was selected from visible controlled nodes and unlocked buildings.", "Reject it if you want to reserve industry for another build."
	case string(domain.MinisterDraftKindRecipe):
		return "Production recommendation", "Set production to " + targetLabel + ".", "This recipe was selected from the building's available production options.", "Reject it if you want to keep production unchanged."
	case string(domain.MinisterDraftKindUnitOrder):
		return "Expansion recommendation", "Execute " + targetLabel + ".", "This order supports civilian expansion this turn.", "Reject it if you want to move the unit manually."
	default:
		return "Research recommendation", targetLabel + " is the preferred research target.", "This recommendation comes from the rule planner's research evaluation.", "Reject it if you want to choose a different technology."
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

func institutionLabel(policyIDs []string) string {
	labels := make([]string, 0, len(policyIDs))
	for _, policyID := range policyIDs {
		labels = append(labels, policyLabel(policyID))
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

type preparedMinisterDraftPolishJob struct {
	Turn     int
	PlayerID string
	Draft    domain.MinisterDraft
	Input    ministerengine.DraftPromptInput
}

func (r *Runtime) polishPreparedMinisterDraft(job preparedMinisterDraftPolishJob) {
	if r == nil || r.ministerEngine == nil {
		return
	}
	output, ok := r.ministerEngine.PolishDraft(context.Background(), job.PlayerID, job.Draft, job.Input)
	if !ok || output == nil {
		return
	}
	r.applyPreparedMinisterDraftPolish(job, output)
}

func (r *Runtime) applyPreparedMinisterDraftPolish(job preparedMinisterDraftPolishJob, output *ministerengine.DraftOutput) {
	if r == nil || output == nil {
		return
	}
	r.preparedMinisterDraftsMu.Lock()
	defer r.preparedMinisterDraftsMu.Unlock()
	if r.planningStartPreparedTurn == job.Turn {
		return
	}
	playerDrafts, ok := r.preparedMinisterDrafts[job.Turn]
	if !ok {
		return
	}
	drafts := playerDrafts[job.PlayerID]
	for idx := range drafts {
		if strings.TrimSpace(drafts[idx].DraftID) != strings.TrimSpace(job.Draft.DraftID) {
			continue
		}
		drafts[idx].Title = strings.TrimSpace(output.Title)
		drafts[idx].Summary = strings.TrimSpace(output.Summary)
		drafts[idx].Rationale = strings.TrimSpace(output.Rationale)
		drafts[idx].RiskNote = strings.TrimSpace(output.RiskNote)
		drafts[idx].Source = domain.MinisterDraftSourceRuleLLM
		playerDrafts[job.PlayerID] = drafts
		return
	}
}
