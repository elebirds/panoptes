// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: Implements turn coordinator game command dispatch.

package turn

import (
	"context"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/planning"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

func (c *Coordinator) HandleGameCommand(ctx cmddispatch.InboundContext, cmd *pb.GameCommand) error {
	if c.runtime == nil || c.runtime.State() == nil || cmd == nil || cmd.Body == nil {
		return ErrPhaseMismatch
	}
	switch cmd.Body.(type) {
	case *pb.GameCommand_StaticCatalogSyncRequest, *pb.GameCommand_Chat:
		return cmddispatch.DispatchGameCommand(ctx, cmd, gameCommandHandler{coordinator: c})
	}
	if c.runtime.State().Phase != domain.PhasePlanning.String() {
		return ErrPhaseMismatch
	}

	return cmddispatch.DispatchGameCommand(ctx, cmd, gameCommandHandler{coordinator: c})
}

type gameCommandHandler struct {
	coordinator *Coordinator
}

func (h gameCommandHandler) Planning(ctx cmddispatch.InboundContext, cmd *pb.PlanningCommand) error {
	if h.coordinator == nil {
		return ErrPhaseMismatch
	}
	return h.coordinator.planningService.HandleCommand(h.coordinator.host, ctx, cmd)
}

func (h gameCommandHandler) Chat(ctx cmddispatch.InboundContext, cmd *pb.ChatCommand) error {
	if h.coordinator == nil {
		return ErrPhaseMismatch
	}
	return h.coordinator.chatService.HandleCommand(h.coordinator.host, ctx, cmd)
}

func (h gameCommandHandler) StaticCatalogSyncRequest(ctx cmddispatch.InboundContext, cmd *pb.MsgStaticCatalogSyncRequest) error {
	if h.coordinator == nil || h.coordinator.runtime == nil {
		return ErrPhaseMismatch
	}
	return h.coordinator.runtime.HandleStaticCatalogSyncRequest(context.Background(), ctx.PlayerID, cmd)
}

func (h gameCommandHandler) CommandBatch(ctx cmddispatch.InboundContext, cmd *pb.MsgGameCommandBatch) error {
	if h.coordinator == nil {
		return ErrPhaseMismatch
	}
	commands, ok := planning.AdaptCommandBatch(ctx, cmd)
	if !ok {
		return ErrPhaseMismatch
	}
	for _, command := range commands {
		if err := h.Planning(command.Context, command.Command); err != nil {
			return err
		}
	}
	return nil
}
