package planning

import (
	"context"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	cmddispatch "github.com/elebirds/panoptes/internal/transport/dispatch"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

func TestBuildStructureRejectedOutsideTerritory(t *testing.T) {
	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	state := def.State
	state.Players["player-1"].TokensLeft = 3
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	nodeEntry, ok := state.GetNode("A2")
	if !ok {
		t.Fatalf("missing node A2")
	}
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-2"
	node.TerritoryOwner = "player-2"

	session := newPlanningSessionStub(state)
	service := &Service{}
	err = service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "A2", BuildingTypeId: "farm"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgBuildStructureResult](session.sent["player-1"])
	if result == nil || result.GetErrorCode() != "outside_territory" {
		t.Fatalf("build result = %#v, want outside_territory", result)
	}
	if len(state.TurnRuntime.Planning.BuildOrders) != 0 {
		t.Fatalf("build orders = %#v, want empty", state.TurnRuntime.Planning.BuildOrders)
	}
}

func TestBuildStructureRejectedWhenBuildingAlreadyExists(t *testing.T) {
	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	state := def.State
	state.Players["player-1"].TokensLeft = 3
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Research.UnlockRecipe("farm_food")

	session := newPlanningSessionStub(state)
	service := &Service{}
	err = service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "A1", BuildingTypeId: "farm"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgBuildStructureResult](session.sent["player-1"])
	if result == nil || result.GetErrorCode() != "building_exists" {
		t.Fatalf("build result = %#v, want building_exists", result)
	}
}

func TestIssueUnitOrderEchoesPlanningSnapshot(t *testing.T) {
	def, err := scenario.SettlerFoundCity()
	if err != nil {
		t.Fatalf("SettlerFoundCity() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	session := newPlanningSessionStub(def.State)
	service := &Service{}
	err = service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       "settler-1",
				Action:       "settle_city",
				TargetNodeId: "C3",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil {
		t.Fatalf("planning snapshot not sent")
	}
	if got := len(snapshot.GetUnitOrders()); got != 1 {
		t.Fatalf("snapshot unit orders = %d, want 1", got)
	}
	if snapshot.GetUnitOrders()[0].GetAction() != "settle_city" {
		t.Fatalf("snapshot unit order = %#v", snapshot.GetUnitOrders()[0])
	}
}

type planningSessionStub struct {
	state *domain.GameState
	sent  map[string][]proto.Message
}

func newPlanningSessionStub(state *domain.GameState) *planningSessionStub {
	return &planningSessionStub{
		state: state,
		sent:  make(map[string][]proto.Message),
	}
}

func (s *planningSessionStub) State() *domain.GameState { return s.state }

func (s *planningSessionStub) Submit(string) {}

func (s *planningSessionStub) SendToPlayer(_ context.Context, playerID string, msg proto.Message) error {
	s.sent[playerID] = append(s.sent[playerID], msg)
	return nil
}

func (s *planningSessionStub) IsDevMode() bool { return true }

func (s *planningSessionStub) QueueBuildOrder(order domain.BuildOrder) {
	s.state.TurnRuntime.Planning.BuildOrders = append(s.state.TurnRuntime.Planning.BuildOrders, order)
}

func (s *planningSessionStub) QueueResearchOrder(order domain.ResearchOrder) {
	s.state.TurnRuntime.Planning.ResearchOrders = append(s.state.TurnRuntime.Planning.ResearchOrders, order)
}

func (s *planningSessionStub) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	s.state.TurnRuntime.Planning.RecipeSelections = append(s.state.TurnRuntime.Planning.RecipeSelections, order)
}

func (s *planningSessionStub) SetMinisterDirective(playerID string, directive string) {
	if s.state.TurnRuntime.Planning.MinisterDirectives == nil {
		s.state.TurnRuntime.Planning.MinisterDirectives = make(map[string]string)
	}
	s.state.TurnRuntime.Planning.MinisterDirectives[playerID] = directive
}

func (s *planningSessionStub) SetWarDirectives(playerID string, directives []domain.WarZoneDirective) {
	if s.state.TurnRuntime.Planning.WarDirectives == nil {
		s.state.TurnRuntime.Planning.WarDirectives = make(map[string][]domain.WarZoneDirective)
	}
	s.state.TurnRuntime.Planning.WarDirectives[playerID] = append([]domain.WarZoneDirective(nil), directives...)
}

func (s *planningSessionStub) SetUnitOrder(order gameorders.UnitOrder) {
	if s.state.TurnRuntime.Planning.UnitOrders == nil {
		s.state.TurnRuntime.Planning.UnitOrders = make(map[string]domain.UnitDirective)
	}
	s.state.TurnRuntime.Planning.UnitOrders[order.UnitID] = order.ToDirective()
}

func (s *planningSessionStub) CancelUnitOrder(_ string, unitID string) {
	delete(s.state.TurnRuntime.Planning.UnitOrders, unitID)
}

func (s *planningSessionStub) SendPlanningSnapshot(ctx context.Context, playerID string) error {
	return s.SendToPlayer(ctx, playerID, gamequery.BuildPlanningSnapshot(s.state, playerID))
}

func (s *planningSessionStub) BuildNodeViewForPlayer(nodeID string, viewerID string) *pb.NodeView {
	entry, ok := s.state.GetNode(nodeID)
	if !ok {
		return nil
	}
	return gamequery.BuildNodeView(s.state, entry, viewerID)
}

func (s *planningSessionStub) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return s.state.GetNode(nodeID)
}

func lastMessage[T proto.Message](msgs []proto.Message) T {
	var zero T
	for i := len(msgs) - 1; i >= 0; i-- {
		typed, ok := msgs[i].(T)
		if ok {
			return typed
		}
	}
	return zero
}
