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

func TestCombatPhase_HandleMsgCombatOrderRefreshesSnapshot(t *testing.T) {
	room, unitID := newCombatPhaseRoom(t)
	phase := &CombatPhase{}
	phase.Enter(room)

	payload, err := protojson.Marshal(&pb.MsgCombatOrder{
		UnitId:       unitID,
		Action:       "move",
		TargetNodeId: "N2_0",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	if err := phase.HandleMessage(room, "player-1", "MsgCombatOrder", payload); err != nil {
		t.Fatalf("HandleMessage() error = %v", err)
	}

	if len(room.orders) != 1 {
		t.Fatalf("orders len = %d, want 1", len(room.orders))
	}
	if room.snapshotRefreshes != 1 {
		t.Fatalf("snapshot refresh count = %d, want 1", room.snapshotRefreshes)
	}
}

func TestCombatPhase_HandlePathPreviewRequestReturnsPreviewResponse(t *testing.T) {
	room, unitID := newCombatPhaseRoom(t)
	phase := &CombatPhase{}
	phase.Enter(room)

	payload, err := protojson.Marshal(&pb.MsgCombatPathPreviewRequest{
		RequestId:    "req-1",
		UnitId:       unitID,
		Action:       "move",
		TargetNodeId: "N2_0",
	})
	if err != nil {
		t.Fatalf("Marshal() error = %v", err)
	}

	if err := phase.HandleMessage(room, "player-1", "MsgCombatPathPreviewRequest", payload); err != nil {
		t.Fatalf("HandleMessage() error = %v", err)
	}

	if len(room.sent) != 1 {
		t.Fatalf("sent len = %d, want 1", len(room.sent))
	}
	resp, ok := room.sent[0].(*pb.MsgCombatPathPreviewResponse)
	if !ok {
		t.Fatalf("sent type = %T, want MsgCombatPathPreviewResponse", room.sent[0])
	}
	if !resp.GetValid() {
		t.Fatalf("response valid = false, want true")
	}
	if resp.GetFirstTurnNodeId() != "N2_0" {
		t.Fatalf("first_turn_node_id = %q, want %q", resp.GetFirstTurnNodeId(), "N2_0")
	}
	if resp.GetTotalTurns() != 1 {
		t.Fatalf("total_turns = %d, want 1", resp.GetTotalTurns())
	}
}

type combatPhaseRoomStub struct {
	state             *domain.GameState
	orders            []domain.CombatOrder
	snapshotRefreshes int
	sent              []proto.Message
}

func (r *combatPhaseRoomStub) State() *domain.GameState { return r.state }
func (r *combatPhaseRoomStub) NotifyTurn(string)        {}
func (r *combatPhaseRoomStub) Submit(string)            {}
func (r *combatPhaseRoomStub) IsDevMode() bool          { return false }
func (r *combatPhaseRoomStub) QueueBuildOrder(domain.BuildOrder) {
}
func (r *combatPhaseRoomStub) QueueResearchOrder(domain.ResearchOrder) {
}
func (r *combatPhaseRoomStub) QueueRecipeSelection(domain.RecipeSelectionOrder) {
}
func (r *combatPhaseRoomStub) SetMinisterDirective(string, string) {}
func (r *combatPhaseRoomStub) SetWarDirectives(string, []WarZoneDirective) {
}
func (r *combatPhaseRoomStub) SetVetoUnit(string, string) {}
func (r *combatPhaseRoomStub) SetMicroOrder(string, string, string) {
}
func (r *combatPhaseRoomStub) SetCombatOrder(order domain.CombatOrder) {
	r.orders = append(r.orders, order)
}
func (r *combatPhaseRoomStub) BuildNodeViewForPlayer(string, string) *pb.NodeView { return nil }
func (r *combatPhaseRoomStub) NodeByID(nodeID string) (*donburi.Entry, bool) {
	return r.state.GetNode(nodeID)
}
func (r *combatPhaseRoomStub) SendToPlayer(_ string, msg proto.Message) error {
	r.sent = append(r.sent, msg)
	return nil
}
func (r *combatPhaseRoomStub) SendCombatOrdersSnapshot(string) error {
	r.snapshotRefreshes++
	return nil
}

func newCombatPhaseRoom(t *testing.T) (*combatPhaseRoomStub, string) {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Units: []staticdata.UnitDefinition{
			{ID: "warrior", Class: "melee", MaxHP: 30, Attack: 10, AttackRange: 1, MoveRange: 2, VisionRange: 3, TrainCost: staticdata.ResourceAmounts{}, Upkeep: staticdata.ResourceAmounts{"food": 1}, Multipliers: map[string]float64{}, Flags: staticdata.UnitFlags{CanCapture: true}},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", MoveCostNoRoad: 2, Passable: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{
		ID:           "phase-combat-test",
		Width:        3,
		Height:       1,
		SpawnPoints:  map[int]domain.Position{0: {X: 0, Y: 0}},
		PlayerSpawns: map[string]domain.Position{"player-1": {X: 0, Y: 0}},
		NamedNodes:   map[string]string{},
		NodeIndex:    map[string]donburi.Entity{},
	}
	for x := 0; x < 3; x++ {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: combatPhaseNodeID(x), X: x, Y: 0, Terrain: "plain"})
		mapData.NodeIndex[combatPhaseNodeID(x)] = entity
	}

	state := domain.NewGameState("phase-combat-test", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.Players["player-1"] = &domain.PlayerState{PlayerID: "player-1", Username: "alice", TokensLeft: 3}

	entry := world.Entry(ecs.CreateUnit(world, "warrior", "player-1", domain.Position{X: 0, Y: 0}))
	unitID := ecs.UnitStatsC.Get(entry).ID
	return &combatPhaseRoomStub{state: state}, unitID
}

func combatPhaseNodeID(x int) string {
	return "N" + string(rune('0'+x)) + "_0"
}
