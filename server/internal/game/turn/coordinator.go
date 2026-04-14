package turn

import (
	"context"
	"errors"
	"time"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/engine/minister"
	"github.com/elebirds/panoptes/internal/game/planning"
	"github.com/elebirds/panoptes/internal/game/session"
	"github.com/elebirds/panoptes/internal/staticdata"
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
	planningTimeoutSec := rules.PlanningTimeoutSeconds()
	if planningTimeoutSec <= 0 {
		planningTimeoutSec = 35
	}

	for !c.runtime.State().IsOver {
		if ctx.Err() != nil {
			return
		}
		if c.ministerEngine != nil {
			c.ministerEngine.GenerateReports(ctx, c.host)
		}

		c.planningService.Enter(c.host)
		c.runtime.State().Phase = domain.PhasePlanning.String()
		c.host.NotifyTurn(domain.PhasePlanning.String())
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

func (c *Coordinator) HandleMessage(playerID, msgType string, payload []byte) error {
	if c.runtime == nil || c.runtime.State() == nil || !c.isMessageAllowed(msgType) {
		return ErrPhaseMismatch
	}
	return c.planningService.HandleMessage(c.host, playerID, msgType, payload)
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

func (c *Coordinator) isMessageAllowed(msgType string) bool {
	if c.runtime == nil || c.runtime.State() == nil {
		return false
	}

	switch c.runtime.State().Phase {
	case domain.PhasePlanning.String():
		switch msgType {
		case "MsgSetPolicy",
			"MsgBuildStructure",
			"MsgRevealNode",
			"MsgSetMinisterDirective",
			"MsgSetResearchTarget",
			"MsgSetBuildingRecipe",
			"MsgSetWarZone",
			"MsgWarZoneDirective",
			"MsgIssueUnitOrder",
			"MsgCancelUnitOrder",
			"MsgPlanningPathPreviewRequest",
			"MsgSubmitTurn":
			return true
		}
	}

	return false
}
