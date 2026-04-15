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
			BuildStructure: &pb.MsgBuildStructure{NodeId: "A2", BuildingTypeId: "farm", CityId: "A1"},
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

func TestBuildStructureReplacesDraftOnSameNodeWithoutChargingExtraToken(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "barracks", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 80, TakeoverMode: "city_capture"},
			{ID: "wall", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 90, TakeoverMode: "city_capture"},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{ID: "default", NodeIndex: map[string]donburi.Entity{}}
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	targetEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N1", X: 1, Y: 0, Terrain: "plain"})
	mapData.NodeIndex["C1"] = cityEntity
	mapData.NodeIndex["N1"] = targetEntity
	cityEntry := world.Entry(cityEntity)
	targetEntry := world.Entry(targetEntity)
	for _, entry := range []*donburi.Entry{cityEntry, targetEntry} {
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", cityEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"
	state.Players["player-1"].TokensLeft = 3
	state.Players["player-1"].Resources.Set(domain.ResourceWood, 5)
	state.Players["player-1"].Resources.Set(domain.ResourceOre, 5)
	state.Players["player-1"].Research.UnlockBuilding("barracks")
	state.Players["player-1"].Research.UnlockBuilding("wall")

	session := newPlanningSessionStub(state)
	service := &Service{}
	first := &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "N1", BuildingTypeId: "wall", CityId: "C1"},
		},
	}
	second := &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "N1", BuildingTypeId: "barracks", CityId: "C1"},
		},
	}
	if err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, first); err != nil {
		t.Fatalf("first HandleCommand() error = %v", err)
	}
	if err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, second); err != nil {
		t.Fatalf("second HandleCommand() error = %v", err)
	}

	firstResult := firstMessage[*pb.MsgBuildStructureResult](session.sent["player-1"])
	lastResult := lastMessage[*pb.MsgBuildStructureResult](session.sent["player-1"])
	if got := state.Players["player-1"].TokensLeft; got != 2 {
		t.Fatalf("tokens left = %d, want 2 (first=%#v last=%#v build_orders=%#v)", got, firstResult, lastResult, state.TurnRuntime.Planning.BuildOrders)
	}
	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 1 {
		t.Fatalf("build order count = %d, want 1", got)
	}
	if got := state.TurnRuntime.Planning.BuildOrders[0].BuildingType; got != "barracks" {
		t.Fatalf("build order type = %q, want barracks", got)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil || len(snapshot.GetBuildOrders()) != 1 || snapshot.GetBuildOrders()[0].GetBuildingTypeId() != "barracks" {
		t.Fatalf("snapshot build orders = %#v, want latest barracks draft", snapshot.GetBuildOrders())
	}
}

func TestSetBuildingRecipeReplacesDraftOnSameNode(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", BuildingScope: "in_city", MaxHP: 80, RecipeIDs: []string{"train_infantry", "train_settler"}},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_infantry", BuildingID: "barracks", WorkAmount: 2, BaseProgress: 1},
			{ID: "train_settler", BuildingID: "barracks", WorkAmount: 3, BaseProgress: 1},
		},
	}))

	world := donburi.NewWorld()
	nodeEntity := ecs.CreateNode(world, ecs.MapNode{ID: "B1", X: 0, Y: 0, Terrain: "plain"})
	nodeEntry := world.Entry(nodeEntity)
	node := ecs.NodeC.Get(nodeEntry)
	node.Owner = "player-1"
	node.TerritoryOwner = "player-1"
	ecs.CreateBuilding(world, "barracks", "player-1", "C1", nodeEntry)

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{
		ID:        "default",
		NodeIndex: map[string]donburi.Entity{"B1": nodeEntity},
	})
	state.World = world
	state.Players["player-1"].Research.UnlockRecipe("train_infantry")
	state.Players["player-1"].Research.UnlockRecipe("train_settler")

	session := newPlanningSessionStub(state)
	service := &Service{}
	for _, recipeID := range []string{"train_infantry", "train_settler"} {
		err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
			Body: &pb.PlanningCommand_SetBuildingRecipe{
				SetBuildingRecipe: &pb.MsgSetBuildingRecipe{NodeId: "B1", RecipeId: recipeID},
			},
		})
		if err != nil {
			t.Fatalf("HandleCommand(%s) error = %v", recipeID, err)
		}
	}

	if got := len(state.TurnRuntime.Planning.RecipeSelections); got != 1 {
		t.Fatalf("recipe selection count = %d, want 1", got)
	}
	if got := state.TurnRuntime.Planning.RecipeSelections[0].RecipeID; got != "train_settler" {
		t.Fatalf("recipe selection = %q, want train_settler", got)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil || len(snapshot.GetRecipeSelections()) != 1 || snapshot.GetRecipeSelections()[0].GetRecipeId() != "train_settler" {
		t.Fatalf("snapshot recipe selections = %#v, want latest train_settler draft", snapshot.GetRecipeSelections())
	}
}

func TestWarZoneDirectiveReplacesDraftOnSameZone(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}

	for _, directive := range []struct {
		action string
		target string
	}{
		{action: "attack", target: "A1"},
		{action: "hold", target: "B2"},
	} {
		err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
			Body: &pb.PlanningCommand_WarZoneDirective{
				WarZoneDirective: &pb.MsgWarZoneDirective{ZoneId: "north", Directive: directive.action, TargetNode: directive.target},
			},
		})
		if err != nil {
			t.Fatalf("HandleCommand(%s) error = %v", directive.action, err)
		}
	}

	got := state.TurnRuntime.Planning.WarDirectives["player-1"]
	if len(got) != 1 || got[0].Directive != "hold" || got[0].TargetNode != "B2" {
		t.Fatalf("war directives = %#v, want latest hold/B2 only", got)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil || len(snapshot.GetWarZoneDirectives()) != 1 || snapshot.GetWarZoneDirectives()[0].GetDirective() != "hold" {
		t.Fatalf("snapshot war directives = %#v, want latest hold draft", snapshot.GetWarZoneDirectives())
	}
}

func TestSetInstitutionLoadoutRejectsNonInstitutionPolicy(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national", ActivationTiming: "same_turn"},
			{ID: "academy_charter", Layer: "institutional", ActivationTiming: "next_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Players["player-1"].Institutions.SlotCount = 1
	state.Players["player-1"].Institutions.UnlockCandidate("academy_charter")

	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetInstitutionLoadout{
			SetInstitutionLoadout: &pb.MsgSetInstitutionLoadout{PolicyIds: []string{"expansion"}},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetInstitutionLoadoutResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("institution result = %#v, want invalid_directive", result)
	}
	if state.TurnRuntime.Planning.HasPendingInstitutionLoadout("player-1") {
		t.Fatalf("pending institution loadout should stay empty")
	}
}

func TestSetInstitutionLoadoutQueuesDraftAndSnapshot(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Policies: []staticdata.PolicyDefinition{
			{ID: "academy_charter", Layer: "institutional", ActivationTiming: "next_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	state.Players["player-1"].Institutions.SlotCount = 1
	state.Players["player-1"].Institutions.UnlockCandidate("academy_charter")

	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetInstitutionLoadout{
			SetInstitutionLoadout: &pb.MsgSetInstitutionLoadout{PolicyIds: []string{"academy_charter"}},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetInstitutionLoadoutResult](session.sent["player-1"])
	if result == nil || !result.GetSuccess() {
		t.Fatalf("institution result = %#v, want success", result)
	}
	if got := state.TurnRuntime.Planning.PendingInstitutionLoadout("player-1"); len(got) != 1 || got[0] != "academy_charter" {
		t.Fatalf("pending institution loadout = %#v, want [academy_charter]", got)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil || len(snapshot.GetPlannedInstitutionPolicyIds()) != 1 || snapshot.GetPlannedInstitutionPolicyIds()[0] != "academy_charter" {
		t.Fatalf("planned institution ids = %#v, want [academy_charter]", snapshot.GetPlannedInstitutionPolicyIds())
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
	s.state.TurnRuntime.Planning.UpsertBuildOrder(order)
}

func (s *planningSessionStub) QueueRecipeSelection(order domain.RecipeSelectionOrder) {
	s.state.TurnRuntime.Planning.UpsertRecipeSelection(order)
}

func (s *planningSessionStub) SetInstitutionLoadout(playerID string, policyIDs []string) {
	s.state.TurnRuntime.Planning.SetPendingInstitutionLoadout(playerID, policyIDs)
}

func (s *planningSessionStub) SetMinisterDirective(playerID string, directive string) {
	if s.state.TurnRuntime.Planning.MinisterDirectives == nil {
		s.state.TurnRuntime.Planning.MinisterDirectives = make(map[string]string)
	}
	s.state.TurnRuntime.Planning.MinisterDirectives[playerID] = directive
}

func (s *planningSessionStub) SetWarDirectives(playerID string, directives []domain.WarZoneDirective) {
	for _, directive := range directives {
		s.state.TurnRuntime.Planning.UpsertWarDirective(playerID, directive)
	}
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

func firstMessage[T proto.Message](msgs []proto.Message) T {
	var zero T
	for _, msg := range msgs {
		typed, ok := msg.(T)
		if ok {
			return typed
		}
	}
	return zero
}
