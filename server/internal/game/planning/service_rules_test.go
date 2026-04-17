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
	state.Map.PlayerSpawns["player-1"] = domain.Position{X: 99, Y: 99}

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
	if got := result.GetFeedbackMessage(); got != "该节点不在你的有效辖区内，当前不能建造。" {
		t.Fatalf("feedback_message = %q, want outside territory message", got)
	}
	assertFeedbackDetailValue(t, result.GetFeedbackDetails(), "node_id", "A2")
	assertFeedbackDetailValue(t, result.GetFeedbackDetails(), "building_type_id", "farm")
	assertFeedbackDetailValue(t, result.GetFeedbackDetails(), "city_id", "A1")
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
	if got := result.GetFeedbackMessage(); got != "该节点已经有建筑，不能重复建造。" {
		t.Fatalf("feedback_message = %q, want building exists message", got)
	}
}

func TestBuildStructurePreviewRejectedOutsideTerritoryDoesNotMutateState(t *testing.T) {
	def, err := scenario.ResearchUnlockBuild()
	if err != nil {
		t.Fatalf("ResearchUnlockBuild() error = %v", err)
	}
	staticdata.SetDefault(def.Catalog)

	state := def.State
	state.Players["player-1"].TokensLeft = 3
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Map.PlayerSpawns["player-1"] = domain.Position{X: 99, Y: 99}

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
		Body: &pb.PlanningCommand_BuildStructurePreview{
			BuildStructurePreview: &pb.MsgBuildStructurePreviewRequest{
				RequestId:      "preview-build-1",
				NodeId:         "A2",
				BuildingTypeId: "farm",
				CityId:         "A1",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgBuildStructurePreviewResponse](session.sent["player-1"])
	if result == nil || result.GetValid() || result.GetErrorCode() != "outside_territory" {
		t.Fatalf("build preview result = %#v, want invalid outside_territory", result)
	}
	if got := result.GetFeedbackMessage(); got != "该节点不在你的有效辖区内，当前不能建造。" {
		t.Fatalf("feedback_message = %q, want outside territory message", got)
	}
	if got := state.Players["player-1"].TokensLeft; got != 3 {
		t.Fatalf("tokens left = %d, want unchanged 3", got)
	}
	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 0 {
		t.Fatalf("build order count = %d, want 0", got)
	}
	if snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"]); snapshot != nil {
		t.Fatalf("planning snapshot = %#v, want nil for preview", snapshot)
	}
}

func TestBuildStructureRejectedInsufficientPointsIncludesFeedback(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             100,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 1,
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "city_territory", BuildingScope: "out_of_city", PointCosts: staticdata.PointAmounts{"industry_output": 1}, MaxHP: 60, TakeoverMode: "delayed"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{ID: "point-preview", NodeIndex: map[string]donburi.Entity{}}
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	targetEntity := ecs.CreateNode(world, ecs.MapNode{ID: "N1", X: 1, Y: 0, Terrain: "plain"})
	mapData.NodeIndex["C1"] = cityEntity
	mapData.NodeIndex["N1"] = targetEntity
	for _, entity := range []donburi.Entity{cityEntity, targetEntity} {
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", world.Entry(cityEntity))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.Players["player-1"].TokensLeft = 3
	state.Players["player-1"].Research.UnlockBuilding("farm")
	state.Players["player-1"].Resources.Set(domain.ResourceWood, 10)
	state.EnsureCityState("player-1", "C1")
	state.Players["player-1"].CapitalCityID = "C1"

	session := newPlanningSessionStub(state)
	session.devMode = false
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{NodeId: "N1", BuildingTypeId: "farm", CityId: "C1"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgBuildStructureResult](session.sent["player-1"])
	if result == nil || result.GetErrorCode() != "insufficient_points" {
		t.Fatalf("build result = %#v, want insufficient_points", result)
	}
	if got := result.GetFeedbackMessage(); got != "工业点数不足，无法提交这条建造。" {
		t.Fatalf("feedback_message = %q, want insufficient points message", got)
	}
	assertFeedbackDetailValue(t, result.GetFeedbackDetails(), "missing_point.industry_output", "1")
	if got := len(state.TurnRuntime.Planning.BuildOrders); got != 0 {
		t.Fatalf("build order count = %d, want 0", got)
	}
}

func TestSetBuildingRecipePreviewWarnsWhenBuildingBlocked(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "farm", PlacementKind: "city_territory", BuildingScope: "out_of_city", MaxHP: 60, RecipeIDs: []string{"farm_food"}, TakeoverMode: "delayed"},
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 100, TakeoverMode: "disabled"},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "farm_food", BuildingID: "farm", WorkAmount: 2, BaseProgress: 1},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	mapData := &domain.MapData{ID: "recipe-preview", NodeIndex: map[string]donburi.Entity{}}
	cityEntity := ecs.CreateNode(world, ecs.MapNode{ID: "C1", X: 0, Y: 0, Terrain: "plain"})
	farmEntity := ecs.CreateNode(world, ecs.MapNode{ID: "F1", X: 1, Y: 0, Terrain: "plain"})
	mapData.NodeIndex["C1"] = cityEntity
	mapData.NodeIndex["F1"] = farmEntity
	for _, entity := range []donburi.Entity{cityEntity, farmEntity} {
		entry := world.Entry(entity)
		node := ecs.NodeC.Get(entry)
		node.Owner = "player-1"
		node.TerritoryOwner = "player-1"
	}
	ecs.CreateBuilding(world, "city_core", "player-1", "C1", world.Entry(cityEntity))
	farmEntry := world.Entry(farmEntity)
	ecs.CreateBuilding(world, "farm", "player-1", "C1", farmEntry)
	farmEntry.AddComponent(ecs.BuildingOperationC)
	ecs.BuildingOperationC.SetValue(farmEntry, ecs.BuildingOperationComp{
		SelectedRecipeID: "farm_food",
		ProgressTurns:    1,
		RequiredTurns:    2,
		BlockedReason:    "insufficient_resources",
	})

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, mapData)
	state.World = world
	state.Players["player-1"].Research.UnlockRecipe("farm_food")
	state.EnsureCityState("player-1", "C1")

	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetBuildingRecipePreview{
			SetBuildingRecipePreview: &pb.MsgSetBuildingRecipePreviewRequest{
				RequestId: "preview-recipe-1",
				NodeId:    "F1",
				RecipeId:  "farm_food",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetBuildingRecipePreviewResponse](session.sent["player-1"])
	if result == nil || !result.GetValid() || result.GetErrorCode() != "" {
		t.Fatalf("recipe preview result = %#v, want valid warning", result)
	}
	if got := result.GetFeedbackMessage(); got != "生产所需资源不足，本回合无法推进。" {
		t.Fatalf("feedback_message = %q, want blocked warning", got)
	}
	if got := len(state.TurnRuntime.Planning.RecipeSelections); got != 0 {
		t.Fatalf("recipe selection count = %d, want 0", got)
	}
	if snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"]); snapshot != nil {
		t.Fatalf("planning snapshot = %#v, want nil for preview", snapshot)
	}
}

func TestSetBuildingRecipeRejectedIncludesSpecificFeedback(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Buildings: []staticdata.BuildingDefinition{
			{ID: "barracks", BuildingScope: "in_city", MaxHP: 80, RecipeIDs: []string{"train_infantry"}},
		},
		Recipes: []staticdata.RecipeDefinition{
			{ID: "train_infantry", BuildingID: "barracks", WorkAmount: 2, BaseProgress: 1},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{
				ID: "militia_mobilization",
				ExplicitEffects: []staticdata.ExplicitEffect{
					{Type: "unlock_recipe", TargetID: "train_infantry"},
				},
			},
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

	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetBuildingRecipe{
			SetBuildingRecipe: &pb.MsgSetBuildingRecipe{NodeId: "B1", RecipeId: "train_infantry"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetBuildingRecipeResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("recipe result = %#v, want invalid_directive", result)
	}
	if got := result.GetFeedbackMessage(); got != "该配方尚未解锁，当前不能设置。" {
		t.Fatalf("feedback_message = %q, want recipe unlock message", got)
	}
}

func TestIssueUnitOrderEchoesPlanningSnapshot(t *testing.T) {
	state := newStructureAttackPlanningState(t)
	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       "infantry-1",
				Action:       "attack",
				TargetNodeId: "A2",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := firstMessage[*pb.MsgIssueUnitOrderResult](session.sent["player-1"])
	if result == nil || !result.GetSuccess() || result.GetAction() != "attack" || result.GetTargetNodeId() != "A2" {
		t.Fatalf("unit order result = %#v, want accepted attack target A2", result)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil {
		t.Fatalf("planning snapshot not sent")
	}
	if got := len(snapshot.GetUnitOrders()); got != 1 {
		t.Fatalf("snapshot unit orders = %d, want 1", got)
	}
	if snapshot.GetUnitOrders()[0].GetAction() != "attack" || snapshot.GetUnitOrders()[0].GetTargetNodeId() != "A2" {
		t.Fatalf("snapshot unit order = %#v", snapshot.GetUnitOrders()[0])
	}
}

func TestIssueUnitOrderRejectsInvalidStructureTargetKeepsExistingDraft(t *testing.T) {
	state := newStructureAttackPlanningState(t)
	state.TurnRuntime.Planning.UnitOrders["infantry-1"] = domain.UnitDirective{
		PlayerID:     "player-1",
		UnitID:       "infantry-1",
		Action:       "hold",
		TargetNodeID: "",
	}

	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:       "infantry-1",
				Action:       "attack",
				TargetNodeId: "A3",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgIssueUnitOrderResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_target" {
		t.Fatalf("unit order result = %#v, want invalid_target failure", result)
	}
	if snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"]); snapshot != nil {
		t.Fatalf("planning snapshot = %#v, want nil on failure", snapshot)
	}
	if directive := state.TurnRuntime.Planning.UnitOrders["infantry-1"]; directive.Action != "hold" {
		t.Fatalf("queued directive = %#v, want preserved hold order", directive)
	}
	if len(session.sent["player-1"]) != 1 {
		t.Fatalf("sent messages = %d, want only result", len(session.sent["player-1"]))
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

func TestSetWarZoneRejectedAsNonMVP(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}

	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetWarZone{
			SetWarZone: &pb.MsgSetWarZone{ZoneId: "north", Name: "North Front", NodeIds: []string{"A1", "A2"}},
		},
	})
	if err == nil {
		t.Fatalf("HandleCommand() error = nil, want invalid_directive problem")
	}
	problem, ok := cmddispatch.AsProblem(err)
	if !ok || problem == nil || problem.GetCode() != "invalid_directive" {
		t.Fatalf("problem = %#v, want invalid_directive", problem)
	}
	if got := state.Players["player-1"].WarZones; len(got) != 0 {
		t.Fatalf("war zones = %#v, want empty", got)
	}
	if got := state.TurnRuntime.Planning.WarDirectives["player-1"]; len(got) != 0 {
		t.Fatalf("planning war directives = %#v, want empty", got)
	}
	if len(session.sent["player-1"]) != 0 {
		t.Fatalf("sent messages = %d, want 0", len(session.sent["player-1"]))
	}
}

func TestSetMinisterDirectiveRejectedAsNonMVP(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}

	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetMinisterDirective{
			SetMinisterDirective: &pb.MsgSetMinisterDirective{Content: "build more farms"},
		},
	})
	if err == nil {
		t.Fatalf("HandleCommand() error = nil, want invalid_directive problem")
	}
	problem, ok := cmddispatch.AsProblem(err)
	if !ok || problem == nil || problem.GetCode() != "invalid_directive" {
		t.Fatalf("problem = %#v, want invalid_directive", problem)
	}
	if got := state.TurnRuntime.Planning.MinisterDirectives["player-1"]; got != "" {
		t.Fatalf("minister directive = %q, want empty", got)
	}
	if len(session.sent["player-1"]) != 0 {
		t.Fatalf("sent messages = %d, want 0", len(session.sent["player-1"]))
	}
}

func TestWarZoneDirectiveRejectedAsNonMVP(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}

	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_WarZoneDirective{
			WarZoneDirective: &pb.MsgWarZoneDirective{ZoneId: "north", Directive: "attack", TargetNode: "A1"},
		},
	})
	if err == nil {
		t.Fatalf("HandleCommand() error = nil, want invalid_directive problem")
	}
	problem, ok := cmddispatch.AsProblem(err)
	if !ok || problem == nil || problem.GetCode() != "invalid_directive" {
		t.Fatalf("problem = %#v, want invalid_directive", problem)
	}
	if got := state.TurnRuntime.Planning.WarDirectives["player-1"]; len(got) != 0 {
		t.Fatalf("planning war directives = %#v, want empty", got)
	}
	if len(session.sent["player-1"]) != 0 {
		t.Fatalf("sent messages = %d, want 0", len(session.sent["player-1"]))
	}
}

func TestIssueUnitOrderRejectsNonMVPRoadAction(t *testing.T) {
	state := newStructureAttackPlanningState(t)
	session := newPlanningSessionStub(state)
	service := &Service{}

	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_IssueUnitOrder{
			IssueUnitOrder: &pb.MsgIssueUnitOrder{
				UnitId:          "infantry-1",
				Action:          "build_road",
				TargetNodeId:    "A2",
				SecondaryNodeId: "A3",
			},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgIssueUnitOrderResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("unit order result = %#v, want invalid_directive", result)
	}
	if snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"]); snapshot != nil {
		t.Fatalf("planning snapshot = %#v, want nil on failure", snapshot)
	}
	if _, ok := state.TurnRuntime.Planning.UnitOrders["infantry-1"]; ok {
		t.Fatalf("planning unit orders = %#v, want no road draft recorded", state.TurnRuntime.Planning.UnitOrders)
	}
}

func TestSetPolicyRejectsInstitutionLayer(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national", ActivationTiming: "same_turn"},
			{ID: "academy_charter", Layer: "institutional", ActivationTiming: "next_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetPolicy{
			SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "academy_charter"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetPolicyResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("set policy result = %#v, want invalid_directive", result)
	}
	if got := state.TurnRuntime.Planning.PendingPolicy("player-1"); got != "" {
		t.Fatalf("pending policy = %q, want empty", got)
	}
}

func TestSetPolicyRejectsUnmetPrerequisite(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Policies: []staticdata.PolicyDefinition{
			{
				ID:    "centralization",
				Layer: "national",
				Prerequisites: []staticdata.Prerequisite{
					{Type: "technology_unlocked", TargetID: "civic_institutions"},
				},
			},
		},
		Technologies: []staticdata.TechnologyDefinition{
			{ID: "civic_institutions", ResearchCost: 2},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetPolicy{
			SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "centralization"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetPolicyResult](session.sent["player-1"])
	if result == nil || result.GetSuccess() || result.GetErrorCode() != "invalid_directive" {
		t.Fatalf("set policy result = %#v, want invalid_directive", result)
	}
	if got := state.TurnRuntime.Planning.PendingPolicy("player-1"); got != "" {
		t.Fatalf("pending policy = %q, want empty", got)
	}
}

func TestSetPolicyQueuesDraftAndSnapshot(t *testing.T) {
	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Policies: []staticdata.PolicyDefinition{
			{ID: "expansion", Layer: "national", ActivationTiming: "same_turn"},
		},
	}))

	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleCommand(session, cmddispatch.InboundContext{PlayerID: "player-1"}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_SetPolicy{
			SetPolicy: &pb.MsgSetPolicy{NationalPolicyId: "expansion"},
		},
	})
	if err != nil {
		t.Fatalf("HandleCommand() error = %v", err)
	}

	result := lastMessage[*pb.MsgSetPolicyResult](session.sent["player-1"])
	if result == nil || !result.GetSuccess() {
		t.Fatalf("set policy result = %#v, want success", result)
	}
	if got := state.TurnRuntime.Planning.PendingPolicy("player-1"); got != domain.Policy("expansion") {
		t.Fatalf("pending policy = %q, want expansion", got)
	}
	snapshot := lastMessage[*pb.MsgPlanningSnapshot](session.sent["player-1"])
	if snapshot == nil || snapshot.GetPlannedNationalPolicyId() != "expansion" {
		t.Fatalf("planned national policy = %#v, want expansion", snapshot)
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

func TestEnvelopeFromPlanningCommandBuildStructure(t *testing.T) {
	envelope, handled, err := EnvelopeFromPlanningCommand(cmddispatch.InboundContext{
		PlayerID:  "player-1",
		RequestID: "req-build",
	}, &pb.PlanningCommand{
		Body: &pb.PlanningCommand_BuildStructure{
			BuildStructure: &pb.MsgBuildStructure{
				NodeId:         "A2",
				BuildingTypeId: "farm",
				CityId:         "A1",
			},
		},
	})
	if err != nil {
		t.Fatalf("EnvelopeFromPlanningCommand() error = %v", err)
	}
	if !handled {
		t.Fatalf("handled = false, want true")
	}
	if envelope.ParticipantID != "player-1" {
		t.Fatalf("participant id = %q, want player-1", envelope.ParticipantID)
	}
	if envelope.RequestID != "req-build" {
		t.Fatalf("request id = %q, want req-build", envelope.RequestID)
	}
	intent, ok := envelope.Intent.(BuildStructureIntent)
	if !ok {
		t.Fatalf("intent type = %T, want BuildStructureIntent", envelope.Intent)
	}
	if intent.NodeID != "A2" || intent.BuildingTypeID != "farm" || intent.CityID != "A1" {
		t.Fatalf("build intent = %#v", intent)
	}
}

func TestHandleIntentSubmitTurnCallsSessionSubmit(t *testing.T) {
	state := domain.NewGameState("game-1", []string{"player-1"}, []string{"alice"}, &domain.MapData{ID: "default"})
	session := newPlanningSessionStub(state)
	service := &Service{}
	err := service.HandleIntent(session, IntentEnvelope{
		ParticipantID: "player-1",
		RequestID:     "req-submit",
		Intent:        SubmitTurnIntent{},
	})
	if err != nil {
		t.Fatalf("HandleIntent() error = %v", err)
	}
	if len(session.submitted) != 1 || session.submitted[0] != "player-1" {
		t.Fatalf("submitted = %#v, want [player-1]", session.submitted)
	}
}

type planningSessionStub struct {
	state     *domain.GameState
	sent      map[string][]proto.Message
	devMode   bool
	submitted []string
}

func newPlanningSessionStub(state *domain.GameState) *planningSessionStub {
	return &planningSessionStub{
		state:   state,
		sent:    make(map[string][]proto.Message),
		devMode: true,
	}
}

func newStructureAttackPlanningState(t *testing.T) *domain.GameState {
	t.Helper()

	staticdata.SetDefault(staticdata.NewCatalog(staticdata.CatalogBundle{
		Rules: staticdata.Rules{
			TokensPerTurn:             3,
			CityCoreMaxHP:             20,
			BaseResearchOutputPerTurn: 1,
			BaseIndustryOutputPerTurn: 2,
		},
		Units: []staticdata.UnitDefinition{
			{
				ID:          "infantry",
				Class:       "melee",
				MaxHP:       30,
				Attack:      10,
				AttackRange: 1,
				MoveRange:   2,
				VisionRange: 3,
				TrainCost:   staticdata.ResourceAmounts{},
				Upkeep:      staticdata.ResourceAmounts{"food": 1},
				Multipliers: map[string]float64{},
				Flags: staticdata.UnitFlags{
					CanCapture:          true,
					CanAttackStructures: true,
				},
			},
		},
		Buildings: []staticdata.BuildingDefinition{
			{ID: "city_core", PlacementKind: "city_foundation_center", BuildingScope: "city_core", MaxHP: 20, TakeoverMode: "disabled"},
			{ID: "farm", PlacementKind: "city_territory", BuildingScope: "in_city", MaxHP: 15, TakeoverMode: "city_capture"},
		},
		Terrains: []staticdata.TerrainDefinition{
			{ID: "plain", Passable: true, Buildable: true},
		},
	}))

	world := donburi.NewWorld()
	nodeIndex := map[string]donburi.Entity{}
	for idx, nodeID := range []string{"A1", "A2", "A3"} {
		entity := ecs.CreateNode(world, ecs.MapNode{ID: nodeID, X: idx, Y: 0, Terrain: "plain"})
		nodeIndex[nodeID] = entity
	}
	state := domain.NewGameState("planning-structure-attack", []string{"player-1", "player-2"}, []string{"alice", "bob"}, &domain.MapData{
		ID:        "planning-structure-attack",
		Width:     3,
		Height:    1,
		NodeIndex: nodeIndex,
	})
	state.World = world
	state.NodeIndex = nodeIndex

	enemyNode := world.Entry(nodeIndex["A2"])
	enemyNodeState := ecs.NodeC.Get(enemyNode)
	enemyNodeState.Owner = "player-2"
	enemyNodeState.TerritoryOwner = "player-2"
	ecs.CreateBuilding(world, "farm", "player-2", "A2", enemyNode)

	allyNode := world.Entry(nodeIndex["A1"])
	allyNodeState := ecs.NodeC.Get(allyNode)
	allyNodeState.Owner = "player-1"
	allyNodeState.TerritoryOwner = "player-1"

	unitEntry := world.Entry(ecs.CreateUnit(world, "infantry", "player-1", domain.Position{X: 0, Y: 0}))
	ecs.UnitStatsC.Get(unitEntry).ID = "infantry-1"
	return state
}

func (s *planningSessionStub) State() *domain.GameState { return s.state }

func (s *planningSessionStub) Submit(playerID string) {
	s.submitted = append(s.submitted, playerID)
}

func (s *planningSessionStub) SendToPlayer(_ context.Context, playerID string, msg proto.Message) error {
	s.sent[playerID] = append(s.sent[playerID], msg)
	return nil
}

func (s *planningSessionStub) IsDevMode() bool { return s.devMode }

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

func assertFeedbackDetailValue(t *testing.T, details []*pb.FeedbackDetail, key string, want string) {
	t.Helper()
	for _, detail := range details {
		if detail == nil || detail.GetKey() != key {
			continue
		}
		if got := detail.GetValue(); got != want {
			t.Fatalf("feedback detail %q = %q, want %q", key, got, want)
		}
		return
	}
	t.Fatalf("feedback detail %q missing", key)
}
