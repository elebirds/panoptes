// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现回合协调模块的回合推进协调逻辑。

package turn

import (
	"context"
	"errors"
	"log/slog"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/game/chat"
	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/game/session"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

var ErrPhaseMismatch = errors.New("phase_mismatch")

type Host interface {
	chat.Session
	planning.Session
	RunTurnResolution()
	ShouldStopAfterResolution() bool
	HandleDraw()
	CheckGameOver()
}

type Coordinator struct {
	runtime         *session.Runtime
	host            Host
	chatService     *chat.Service
	planningService *planning.Service
}

func NewCoordinator(runtime *session.Runtime, host Host) *Coordinator {
	return &Coordinator{
		runtime:         runtime,
		host:            host,
		chatService:     &chat.Service{},
		planningService: &planning.Service{},
	}
}

func (c *Coordinator) Start() {
	if c.runtime == nil || c.host == nil || c.runtime.State() == nil {
		return
	}

	ctx, cancel := context.WithCancel(context.Background())
	c.runtime.SetCancelFunc(cancel)
	if !c.runtime.WaitBootstrapReady(ctx) {
		return
	}

	rules := staticdata.Default().Rules()
	planningTimeoutSec := rules.TurnTimeLimitPlanning
	skipNotify := c.runtime.ConsumeBootstrapPlanningStart()

	for !c.runtime.State().IsOver {
		if ctx.Err() != nil {
			return
		}
		c.planningService.Enter(c.host)
		c.runtime.State().Phase = domain.PhasePlanning.String()
		c.runtime.PreparePlanningStartStateIfNeeded()
		c.beginPlanning(ctx, !skipNotify)
		if skipNotify {
			skipNotify = false
		}
		c.waitAllSubmit(ctx, time.Duration(planningTimeoutSec)*time.Second)
		if ctx.Err() != nil {
			return
		}
		c.runtime.State().Phase = domain.PhaseResolving.String()
		c.host.RunTurnResolution()
		if c.runtime.State().IsOver {
			break
		}

		if rules.MaxTurns > 0 && c.runtime.State().Turn >= rules.MaxTurns {
			c.host.HandleDraw()
			break
		}
		c.runtime.State().Turn++
		c.runtime.PrepareMinisterDraftCacheForTurn(c.runtime.State().Turn)
	}
}

func (c *Coordinator) beginPlanning(ctx context.Context, notifyHumans bool) {
	if c.runtime == nil {
		return
	}
	if notifyHumans {
		for _, human := range c.runtime.HumanParticipants() {
			if err := c.runtime.SendPlanningStart(ctx, human.ID); err != nil {
				slog.Warn("send planning start failed", "participant_id", human.ID, "err", err)
			}
		}
	}

	submitter := coordinatorIntentSubmitter{coordinator: c}
	for _, currentParticipant := range c.runtime.Participants() {
		controller, ok := c.runtime.Controller(currentParticipant.ID)
		if !ok || controller == nil || !controller.IsAutonomous() {
			continue
		}
		if err := controller.BeginPlanning(ctx, currentParticipant, c.runtime.State(), c.runtime.BuildObservation(currentParticipant.ID), submitter); err != nil {
			slog.Warn("controller begin planning failed", "participant_id", currentParticipant.ID, "err", err)
		}
	}
}

type coordinatorIntentSubmitter struct {
	coordinator *Coordinator
}

func (s coordinatorIntentSubmitter) SubmitIntent(_ context.Context, envelope planning.IntentEnvelope) error {
	if s.coordinator == nil || s.coordinator.planningService == nil {
		return nil
	}
	return s.coordinator.planningService.HandleIntent(s.coordinator.host, envelope)
}

func (c *Coordinator) Submit(playerID string) {
	if c.runtime == nil {
		return
	}
	c.runtime.SubmitChannel() <- playerID
}

func (c *Coordinator) SubmitChecked(playerID string) error {
	if c.runtime == nil || c.runtime.State() == nil || c.runtime.State().Phase != domain.PhasePlanning.String() {
		return ErrPhaseMismatch
	}
	c.Submit(playerID)
	return nil
}

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

func (c *Coordinator) waitAllSubmit(ctx context.Context, timeout time.Duration) {
	if c.runtime == nil {
		return
	}
	submitted := make(map[string]bool, c.runtime.PlayerCount())
	timer := time.NewTimer(timeout)
	defer timer.Stop()

	for {
		select {
		case <-ctx.Done():
			return
		case playerID := <-c.runtime.SubmitChannel():
			if playerID == "timeout" {
				return
			}
			submitted[playerID] = true
			if len(submitted) >= c.runtime.PlayerCount() {
				return
			}
		case <-timer.C:
			return
		}
	}
}
