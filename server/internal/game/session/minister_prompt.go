package session

import (
	"fmt"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	ministerengine "github.com/elebirds/panoptes/internal/engine/minister"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
)

func (r *Runtime) BuildMinisterReportInput(playerID string, role string) ministerengine.ReportPromptInput {
	return ministerengine.ReportPromptInput{
		Turn:               r.currentTurn(),
		Phase:              r.currentPhase(),
		PlayerID:           strings.TrimSpace(playerID),
		ObservationSummary: r.BuildMinisterObservationSummary(playerID, role),
		ActionCandidates:   r.BuildMinisterActionCandidateSummary(playerID, role),
		CurrentPolicy:      currentPolicyValue(r.state, playerID),
		CurrentResearch:    currentResearchValue(r.state, playerID),
	}
}

func (r *Runtime) BuildMinisterObservationSummary(playerID string, role string) string {
	return buildMinisterObservationSummary(r.state, r.BuildObservation(playerID), role)
}

func buildMinisterObservationSummary(state *domain.GameState, observation *gamequery.ObservationSnapshot, role string) string {
	if observation == nil {
		return "(暂无观察摘要)"
	}

	parts := []string{
		fmt.Sprintf("visible_nodes=%d", len(observation.VisibleNodes)),
		fmt.Sprintf("memory_nodes=%d", len(observation.MemoryNodes)),
		fmt.Sprintf("visible_units=%d", len(observation.Units)),
		fmt.Sprintf("memory_units=%d", len(observation.MemoryUnits)),
	}
	report := gamequery.BuildInformationReport(observation)
	if report != nil {
		parts = append(parts,
			"report_mode="+strings.TrimSpace(report.GetMode()),
			"report_confidence="+strings.TrimSpace(report.GetConfidence()),
			fmt.Sprintf("reported_omitted=%d", report.GetOmittedCount()),
			fmt.Sprintf("reported_delayed=%d", report.GetDelayedCount()),
			fmt.Sprintf("reported_misread=%d", report.GetMisreadCount()),
		)
	}

	if state != nil {
		if player := state.Players[strings.TrimSpace(observation.ViewerID)]; player != nil {
			resourceKeys := player.Resources.Keys()
			sort.Slice(resourceKeys, func(i, j int) bool { return resourceKeys[i] < resourceKeys[j] })
			resourceParts := make([]string, 0, len(resourceKeys))
			for _, key := range resourceKeys {
				resourceParts = append(resourceParts, fmt.Sprintf("%s=%d", key, player.Resources.Get(key)))
			}
			if len(resourceParts) > 0 {
				parts = append(parts, "resources={"+strings.Join(resourceParts, ", ")+"}")
			}
		}
	}

	visibleNodeIDs := make([]string, 0, minInt(len(observation.VisibleNodes), 4))
	for idx, node := range observation.VisibleNodes {
		if idx >= 4 || node == nil {
			break
		}
		visibleNodeIDs = append(visibleNodeIDs, strings.TrimSpace(node.GetId()))
	}
	if len(visibleNodeIDs) > 0 {
		parts = append(parts, "sample_visible_nodes="+strings.Join(visibleNodeIDs, ","))
	}
	parts = append(parts, roleObservationFocus(observation, role)...)

	return strings.Join(parts, "; ")
}

func (r *Runtime) BuildMinisterActionCandidateSummary(playerID string, role string) string {
	if r == nil || r.state == nil {
		return "(none)"
	}
	return buildMinisterActionCandidateSummary(r.state, playerID, role)
}

func buildMinisterActionCandidateSummary(state *domain.GameState, playerID string, role string) string {
	if state == nil {
		return "(none)"
	}
	playerID = strings.TrimSpace(playerID)
	role = strings.TrimSpace(role)
	drafts := state.TurnRuntime.Planning.MinisterDraftsForPlayer(playerID)
	if len(drafts) == 0 {
		return "(none)"
	}
	parts := make([]string, 0, len(drafts))
	for _, draft := range drafts {
		if !draft.Available || draft.Status != domain.MinisterDraftStatusPending || draft.Turn != state.Turn {
			continue
		}
		if role != "" && strings.TrimSpace(draft.MinisterRole) != role {
			continue
		}
		parts = append(parts, fmt.Sprintf("candidate_id=%s kind=%s target_id=%s target_label=%s source=%s",
			strings.TrimSpace(draft.DraftID),
			strings.TrimSpace(string(draft.Kind)),
			strings.TrimSpace(draft.TargetID),
			strings.TrimSpace(draft.TargetLabel),
			strings.TrimSpace(string(draft.Source)),
		))
	}
	if len(parts) == 0 {
		return "(none)"
	}
	return strings.Join(parts, "\n")
}

func roleObservationFocus(observation *gamequery.ObservationSnapshot, role string) []string {
	role = strings.ToLower(strings.TrimSpace(role))
	switch role {
	case "military":
		return militaryObservationFocus(observation)
	case "domestic":
		return domesticObservationFocus(observation)
	default:
		return []string{"role_focus=general"}
	}
}

func domesticObservationFocus(observation *gamequery.ObservationSnapshot) []string {
	resourceNodes := make([]string, 0, 4)
	buildingNodes := make([]string, 0, 4)
	for _, node := range observation.VisibleNodes {
		if node == nil {
			continue
		}
		nodeID := strings.TrimSpace(node.GetId())
		if node.GetIsResourcePoint() && len(resourceNodes) < 4 {
			resourceNodes = append(resourceNodes, nodeID+":"+strings.TrimSpace(node.GetResourceType()))
		}
		if strings.TrimSpace(node.GetBuildingTypeId()) != "" && len(buildingNodes) < 4 {
			buildingNodes = append(buildingNodes, nodeID+":"+strings.TrimSpace(node.GetBuildingTypeId()))
		}
	}
	return []string{
		"role_focus=domestic",
		"domestic_resource_nodes=" + joinOrNone(resourceNodes),
		"domestic_building_nodes=" + joinOrNone(buildingNodes),
	}
}

func militaryObservationFocus(observation *gamequery.ObservationSnapshot) []string {
	visibleUnits := make([]string, 0, 4)
	enemyPressureNodes := make([]string, 0, 4)
	for _, unit := range observation.Units {
		if unit == nil || len(visibleUnits) >= 4 {
			continue
		}
		visibleUnits = append(visibleUnits, strings.TrimSpace(unit.GetId())+":"+strings.TrimSpace(unit.GetFaction())+":"+strings.TrimSpace(unit.GetUnitType()))
	}
	for _, node := range observation.VisibleNodes {
		if node == nil || len(enemyPressureNodes) >= 4 {
			continue
		}
		if node.GetEnemyUnitCount() > 0 {
			enemyPressureNodes = append(enemyPressureNodes, strings.TrimSpace(node.GetId()))
		}
	}
	return []string{
		"role_focus=military",
		"military_visible_units=" + joinOrNone(visibleUnits),
		"military_enemy_pressure_nodes=" + joinOrNone(enemyPressureNodes),
	}
}

func joinOrNone(values []string) string {
	if len(values) == 0 {
		return "(none)"
	}
	return strings.Join(values, ",")
}

func currentPolicyValue(state *domain.GameState, playerID string) string {
	if state == nil {
		return ""
	}
	playerID = strings.TrimSpace(playerID)
	pending := strings.TrimSpace(string(state.TurnRuntime.Planning.PendingPolicy(playerID)))
	if pending != "" {
		return pending
	}
	if player := state.Players[playerID]; player != nil {
		return strings.TrimSpace(string(player.Policy))
	}
	return ""
}

func currentResearchValue(state *domain.GameState, playerID string) string {
	if state == nil {
		return ""
	}
	playerID = strings.TrimSpace(playerID)
	pending := strings.TrimSpace(state.TurnRuntime.Planning.PendingResearchTarget(playerID))
	if pending != "" {
		return pending
	}
	if player := state.Players[playerID]; player != nil {
		return strings.TrimSpace(player.Research.CurrentTargetTechnologyID)
	}
	return ""
}

func (r *Runtime) currentTurn() int {
	if r == nil || r.state == nil {
		return 0
	}
	return r.state.Turn
}

func (r *Runtime) currentPhase() string {
	if r == nil || r.state == nil {
		return ""
	}
	return r.state.Phase
}

func minInt(a int, b int) int {
	if a < b {
		return a
	}
	return b
}
