package phase

import (
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/encoding/protojson"
	"google.golang.org/protobuf/proto"
)

func TestDomesticPhase_HandleMsgResearchTechnologyQueuesOrderWithoutConsumingToken(t *testing.T) {
	room := newDomesticResearchRoom(t)
	phase := &DomesticPhase{}
	phase.Enter(room)

	payload, err := protojson.Marshal(&pb.MsgResearchTechnology{
		TechnologyId: "agri_unlock_farm",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	if err := phase.HandleMessage(room, "player-1", "MsgResearchTechnology", payload); err != nil {
		t.Fatalf("HandleMessage() error = %v", err)
	}

	if len(room.researchOrders) != 1 {
		t.Fatalf("research orders len = %d, want 1", len(room.researchOrders))
	}
	if room.researchOrders[0].TechnologyID != "agri_unlock_farm" {
		t.Fatalf("queued technology = %q", room.researchOrders[0].TechnologyID)
	}
	if room.state.Players["player-1"].TokensLeft != 3 {
		t.Fatalf("tokens_left = %d, want 3", room.state.Players["player-1"].TokensLeft)
	}
	if len(room.sent) != 1 {
		t.Fatalf("sent len = %d, want 1", len(room.sent))
	}
	result, ok := room.sent[0].(*pb.MsgResearchResult)
	if !ok {
		t.Fatalf("sent type = %T, want MsgResearchResult", room.sent[0])
	}
	if !result.GetSuccess() || result.GetTechnologyId() != "agri_unlock_farm" {
		t.Fatalf("result = %#v", result)
	}
}

func TestDomesticPhase_HandleMsgSetBuildingRecipeRejectsForeignBuilding(t *testing.T) {
	room := newDomesticResearchRoom(t)
	phase := &DomesticPhase{}
	phase.Enter(room)

	payload, err := protojson.Marshal(&pb.MsgSetBuildingRecipe{
		NodeId:   "farm-node",
		RecipeId: "farm_food",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	if err := phase.HandleMessage(room, "player-1", "MsgSetBuildingRecipe", payload); err != nil {
		t.Fatalf("HandleMessage() error = %v", err)
	}

	if len(room.recipeSelections) != 0 {
		t.Fatalf("recipe selections len = %d, want 0", len(room.recipeSelections))
	}
	if len(room.sent) != 1 {
		t.Fatalf("sent len = %d, want 1", len(room.sent))
	}
	result, ok := room.sent[0].(*pb.MsgSetBuildingRecipeResult)
	if !ok {
		t.Fatalf("sent type = %T, want MsgSetBuildingRecipeResult", room.sent[0])
	}
	if result.GetSuccess() {
		t.Fatalf("result should fail: %#v", result)
	}
	if result.GetErrorCode() != "unauthorized" {
		t.Fatalf("error_code = %q, want unauthorized", result.GetErrorCode())
	}
}

func TestDomesticPhase_HandleMsgTokenBuildRejectsLockedBuildingEvenAfterQueuedResearch(t *testing.T) {
	room := newDomesticResearchRoom(t)
	phase := &DomesticPhase{}
	phase.Enter(room)

	researchPayload, err := protojson.Marshal(&pb.MsgResearchTechnology{
		TechnologyId: "agri_unlock_farm",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}
	if err := phase.HandleMessage(room, "player-1", "MsgResearchTechnology", researchPayload); err != nil {
		t.Fatalf("HandleMessage() research error = %v", err)
	}

	buildPayload := []byte(`{"nodeId":"build-node","buildingType":"farm"}`)
	if err := phase.HandleMessage(room, "player-1", "MsgTokenBuild", buildPayload); err != nil {
		t.Fatalf("HandleMessage() build error = %v", err)
	}

	if len(room.buildOrders) != 0 {
		t.Fatalf("build orders len = %d, want 0", len(room.buildOrders))
	}
	if len(room.sent) != 2 {
		t.Fatalf("sent len = %d, want 2", len(room.sent))
	}
	result, ok := room.sent[1].(*pb.MsgTokenResult)
	if !ok {
		t.Fatalf("sent type = %T, want MsgTokenResult", room.sent[1])
	}
	if result.GetSuccess() {
		t.Fatalf("build result should fail: %#v", result)
	}
	if result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("error_code = %q, want invalid_directive", result.GetErrorCode())
	}
}

type domesticResearchRoomStub struct {
	state            *domain.GameState
	buildOrders      []domain.BuildOrder
	researchOrders   []domain.ResearchOrder
	recipeSelections []domain.RecipeSelectionOrder
	sent             []proto.Message
}

func (r *domesticResearchRoomStub) State() *domain.GameState { return r.state }
func (r *domesticResearchRoomStub) NotifyTurn(string)        {}
func (r *domesticResearchRoomStub) Submit(string)            {}
func (r *domesticResearchRoomStub) IsDevMode() bool          { return false }
func (r *domesticResearchRoomStub) QueueBuildOrder(order domain.BuildOrder) {
	r.buildOrders = append(r.buildOrders, order)
}
func (r *domesticResearchRoomStub) QueueResearchOrder(order domain.ResearchOrder) {
	r.researchOrders = append(r.researchOrders, order)
}
func (r *domesticResearchRoomStub) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	r.recipeSelections = append(r.recipeSelections, order)
}
func (r *domesticResearchRoomStub) SetMinisterDirective(string, string) {}
func (r *domesticResearchRoomStub) SetWarDirectives(string, []WarZoneDirective) {
}
func (r *domesticResearchRoomStub) SetVetoUnit(string, string) {}
func (r *domesticResearchRoomStub) SetMicroOrder(string, string, string) {
}
func (r *domesticResearchRoomStub) SetCombatOrder(domain.CombatOrder) {
}
func (r *domesticResearchRoomStub) BuildNodeViewForPlayer(string, string) *pb.NodeView { return nil }
func (r *domesticResearchRoomStub) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return r.state.GetNode(nodeID)
}
func (r *domesticResearchRoomStub) SendToPlayer(_ string, msg proto.Message) error {
	r.sent = append(r.sent, msg)
	return nil
}

func newDomesticResearchRoom(t *testing.T) *domesticResearchRoomStub {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:       3,
			StartingTechPoints:  2,
			TechPointsPerTurn:   1,
			TechPointsMax:       5,
			BuildPointsPerTurn:  10,
			BuildPointsMax:      30,
			CastleBaseHP:        100,
			SafeZoneRadius:      4,
			TurnTimeLimitCombat: 10,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", Category: "production", RecipeIDs: []string{"farm_food"}, DefaultRecipeID: "farm_food", Combat: staticdata.BuildingCombat{MaxHP: 80}},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "farm_food", BuildingID: "farm", Cost: staticdata.ResourceAmounts{}, DurationTurns: 1, DelayPenalty: staticdata.RecipeDelayPenalty{Mode: "add_turns", Value: 1}},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "agri_unlock_farm", Branch: "agriculture", Tier: 1, TechPointCost: 1, Effects: []staticdata.TechnologyEffect{{Type: "unlock_building", TargetID: "farm"}, {Type: "unlock_recipe", TargetID: "farm_food"}}},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "farm-node", X: 1, Y: 1, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	ecs.NodeC.Get(nodeEntry).Owner = "player-2"
	ecs.NodeC.Get(nodeEntry).TerritoryOwner = "player-2"
	ecs.CreateBuilding(world, "farm", "player-2", "castle-b", nodeEntry)
	buildNodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "build-node", X: 2, Y: 2, Terrain: "plain"})
	buildNodeEntry := world.Entry(buildNodeEntity)
	ecs.NodeC.Get(buildNodeEntry).Owner = "player-1"
	ecs.NodeC.Get(buildNodeEntry).TerritoryOwner = "player-1"

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID: "default",
		NodeIndex: map[string]donburi.Entity{
			"farm-node":  nodeEntity,
			"build-node": buildNodeEntity,
		},
	})
	state.World = world

	return &domesticResearchRoomStub{state: state}
}
