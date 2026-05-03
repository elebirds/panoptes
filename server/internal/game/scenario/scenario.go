// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载后端测试/调试场景的构造与布置辅助逻辑。

package scenario

import (
	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/participant"
	"github.com/elebirds/panoptes/internal/staticdata"
)

type Definition struct {
	Name         string
	Catalog      *staticdata.Catalog
	State        *domain.GameState
	PlayerIDs    []string
	Usernames    []string
	Participants []participant.Spec
}

func (d *Definition) ParticipantSpecs() []participant.Spec {
	if d == nil {
		return nil
	}
	if len(d.Participants) > 0 {
		out := make([]participant.Spec, 0, len(d.Participants))
		for _, spec := range d.Participants {
			if spec.Kind == "" {
				spec.Kind = participant.KindHuman
			}
			out = append(out, spec)
		}
		return out
	}
	out := make([]participant.Spec, 0, len(d.PlayerIDs))
	for idx, playerID := range d.PlayerIDs {
		username := playerID
		if idx < len(d.Usernames) && d.Usernames[idx] != "" {
			username = d.Usernames[idx]
		}
		out = append(out, participant.Spec{
			ID:       playerID,
			Username: username,
			Kind:     participant.KindHuman,
		})
	}
	return out
}

func (d *Definition) HumanPlayerIDs() []string {
	specs := d.ParticipantSpecs()
	out := make([]string, 0, len(specs))
	for _, spec := range specs {
		if spec.Kind == "" || spec.Kind == participant.KindHuman {
			out = append(out, spec.ID)
		}
	}
	return out
}
