// Copyright (c) 2026 Panoptes Project Authors.
// Project: Panoptes
// Author: elebirds <hhmcn@outlook.com>
// Updated: 2026-04-14 18:45:09 +0800
// Description: 实现回合协调模块的回合推进协调逻辑。

package turn

import (
	"context"
	"errors"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/game/session"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
)

var ErrPhaseMismatch = errors.New("phase_mismatch")

type Host interface {
	planning.Session
	PlayerIDs() []string
	NotifyTurn(phase string)
	RunTurnResolution()
	ShouldStopAfterResolution() bool
	HandleDraw()
	CheckGameOver()
}

type Coordinator struct {
	runtime         *session.Runtime
	host            Host
	planningService *planning.Service
	ministerEngine  *minister.MinisterEngine
}

func NewCoordinator(runtime *session.Runtime, host Host) *Coordinator {
	return &Coordinator{
		runtime:         runtime,
		host:            host,
		planningService: &planning.Service{},
		ministerEngine:  minister.NewMinisterEngine(nil),
	}
}

func (c *Coordinator) Start() {
	if c.runtime == nil || c.host == nil || c.runtime.State() == nil {
		return
	}

	ctx, cancel := context.WithCancel(context.Background())
	c.runtime.SetCancelFunc(cancel)

	rules := staticdata.Default().Rules()
	planningTimeoutSec := rules.TurnTimeLimitPlanning

	for !c.runtime.State().IsOver {
		if ctx.Err() != nil {
			return
		}
		c.planningService.Enter(c.host)
		c.runtime.State().Phase = domain.PhasePlanning.String()
		if !c.runtime.ConsumeBootstrapPlanningStart() {
			c.host.NotifyTurn(domain.PhasePlanning.String())
		}
		if c.ministerEngine != nil {
			c.ministerEngine.GenerateReports(ctx, c.host)
		}
		c.waitAllSubmit(time.Duration(planningTimeoutSec) * time.Second)
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
	}
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

func (c *Coordinator) waitAllSubmit(timeout time.Duration) {
	if c.runtime == nil {
		return
	}
	submitted := make(map[string]bool, c.runtime.PlayerCount())
	timer := time.NewTimer(timeout)
	defer timer.Stop()

	for {
		select {
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
