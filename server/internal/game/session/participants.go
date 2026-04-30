// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现对局会话参与者查找辅助函数。

package session

import (
	"strings"

	"github.com/elebirds/panoptes/internal/game/participant"
)

func (r *Runtime) participantIDs() []string {
	ids := make([]string, 0, len(r.participants))
	for _, binding := range r.participants {
		if strings.TrimSpace(binding.Participant.ID) == "" {
			continue
		}
		ids = append(ids, binding.Participant.ID)
	}
	return ids
}

func (r *Runtime) participantUsernames() []string {
	usernames := make([]string, 0, len(r.participants))
	for _, binding := range r.participants {
		usernames = append(usernames, binding.Participant.Username)
	}
	return usernames
}

func (r *Runtime) findParticipant(playerID string) (participant.Participant, bool) {
	for _, binding := range r.participants {
		if binding.Participant.ID == playerID {
			return binding.Participant, true
		}
	}
	return participant.Participant{}, false
}

func (r *Runtime) findParticipantBinding(participantID string) (ParticipantBinding, bool) {
	for _, binding := range r.participants {
		if binding.Participant.ID == participantID {
			return binding, true
		}
	}
	return ParticipantBinding{}, false
}
