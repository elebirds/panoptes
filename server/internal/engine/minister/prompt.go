package minister

import (
	"fmt"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/llm"
)

type MinisterProfile struct {
	ID              string
	Name            string
	Role            string
	Ability         int
	Personality     string
	PersonalityDesc string
	Loyalty         int
	Ambition        int
}

func BuildMinisterPrompt(role string, profile MinisterProfile, state *domain.GameState, playerID string, memory *MinisterMemory) llm.CompletionRequest {
	return llm.CompletionRequest{
		SystemPrompt: buildSystemPrompt(role, profile),
		UserPrompt:   buildUserPrompt(state, playerID, memory, profile),
	}
}

func buildSystemPrompt(role string, profile MinisterProfile) string {
	return fmt.Sprintf(
		"你是Panoptes中的%s部长。姓名:%s。性格:%s。描述:%s。能力:%d。请在职责内给出汇报和行动建议，并严格输出JSON。",
		role,
		profile.Name,
		profile.Personality,
		profile.PersonalityDesc,
		profile.Ability,
	)
}

func buildUserPrompt(state *domain.GameState, playerID string, memory *MinisterMemory, profile MinisterProfile) string {
	var player *domain.PlayerState
	if state != nil {
		player = state.Players[playerID]
	}

	resources := ""
	currentPolicy := ""
	turn := 0
	phase := ""
	if state != nil {
		turn = state.Turn
		phase = state.Phase
	}
	if player != nil {
		keys := player.Resources.Keys()
		sort.Slice(keys, func(i, j int) bool { return keys[i] < keys[j] })
		parts := make([]string, 0, len(keys))
		for _, k := range keys {
			parts = append(parts, fmt.Sprintf("%s=%d", k, player.Resources.Get(k)))
		}
		resources = strings.Join(parts, ", ")
		currentPolicy = string(player.Policy)
	}

	stateDesc := fmt.Sprintf("turn=%d phase=%s player=%s resources={%s}", turn, phase, playerID, resources)
	if profile.Loyalty < 5 {
		stateDesc += "；注意：你掌握的信息可能有偏差，不要给出过度确定表述。"
	}

	return fmt.Sprintf(`
当前局势：%s
历史记忆：
%s
当前国策：%s

输出必须是JSON：
{
  "report": "叙事轨文字",
  "metrics": [{"label":"","value":"","trend":"","confidence":"","is_delayed":false}],
  "actions": [{"type":"","params":{}}],
  "action_id": "uuid"
}
`, stateDesc, memory.ToPromptString(), currentPolicy)
}
