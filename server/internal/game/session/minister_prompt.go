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
		ObservationSummary: r.BuildMinisterObservationSummary(playerID),
		CurrentPolicy:      currentPolicyValue(r.state, playerID),
		CurrentResearch:    currentResearchValue(r.state, playerID),
	}
}

func (r *Runtime) BuildMinisterObservationSummary(playerID string) string {
	return buildMinisterObservationSummary(r.state, r.BuildObservation(playerID))
}

func buildMinisterObservationSummary(state *domain.GameState, observation *gamequery.ObservationSnapshot) string {
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

	return strings.Join(parts, "; ")
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
