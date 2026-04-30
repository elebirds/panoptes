// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-30 00:00:00 +0800
// Description: 承载 planning 命令处理、端口拆分与响应投递相关逻辑。

package planning

import (
	"context"

	"github.com/elebirds/panoptes/internal/domain"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/participant"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"google.golang.org/protobuf/proto"
)

type planningStatePort interface {
	State() *domain.GameState
}

type participantPort interface {
	Participant(participantID string) (participant.Participant, bool)
}

type planningDeliveryPort interface {
	SendToPlayer(ctx context.Context, playerID string, msg proto.Message) error
	SendPlanningSnapshot(ctx context.Context, playerID string) error
}

type planningDraftPort interface {
	QueueBuildOrder(order domain.BuildOrder)
	QueueRecipeSelection(order domain.RecipeSelectionOrder)
	SetInstitutionLoadout(playerID string, policyIDs []string)
	SetUnitOrder(order gameorders.UnitOrder)
	CancelUnitOrder(playerID string, unitID string)
}

type planningViewPort interface {
	BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView
}

type planningModePort interface {
	IsDevMode() bool
}

type planningSubmitPort interface {
	Submit(playerID string)
}

type Session interface {
	planningStatePort
	participantPort
	planningDeliveryPort
	planningDraftPort
	planningViewPort
	planningModePort
	planningSubmitPort
}
