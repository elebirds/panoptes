package game

import (
	"strings"

	"github.com/elebirds/panoptes/internal/game/ai"
	"github.com/elebirds/panoptes/internal/game/participant"
	gamesession "github.com/elebirds/panoptes/internal/game/session"
)

type ParticipantKind = participant.Kind

const (
	ParticipantKindHuman = participant.KindHuman
	ParticipantKindBot   = participant.KindBot
	ParticipantKindAI    = participant.KindAI
)

type ParticipantSpec = participant.Spec

func NewHumanParticipantSpec(participantID string, username string) ParticipantSpec {
	return ParticipantSpec{
		ID:       strings.TrimSpace(participantID),
		Username: username,
		Kind:     ParticipantKindHuman,
	}
}

func NewBotParticipantSpec(participantID string, username string) ParticipantSpec {
	return ParticipantSpec{
		ID:       strings.TrimSpace(participantID),
		Username: username,
		Kind:     ParticipantKindBot,
	}
}

func NewAIParticipantSpec(participantID string, username string) ParticipantSpec {
	return ParticipantSpec{
		ID:       strings.TrimSpace(participantID),
		Username: username,
		Kind:     ParticipantKindAI,
	}
}

func buildParticipantBindings(specs []ParticipantSpec) []gamesession.ParticipantBinding {
	bindings := make([]gamesession.ParticipantBinding, 0, len(specs))
	for _, spec := range specs {
		participantID := strings.TrimSpace(spec.ID)
		if participantID == "" {
			continue
		}
		currentParticipant := participant.Participant{
			ID:       participantID,
			Username: spec.Username,
			Kind:     spec.Kind,
		}
		if currentParticipant.Kind == "" {
			currentParticipant.Kind = participant.KindHuman
		}

		var controller gamesession.Controller = gamesession.HumanController{}
		if currentParticipant.IsAutonomous() {
			controller = gamesession.NewAutonomousController(ai.RuleBotProvider{})
		}

		bindings = append(bindings, gamesession.ParticipantBinding{
			Participant: currentParticipant,
			Controller:  controller,
		})
	}
	return bindings
}
