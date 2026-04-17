package debug

import (
	"fmt"
	"path/filepath"
	"runtime"
	"testing"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	"github.com/elebirds/panoptes/internal/engine/maploader"
	"github.com/elebirds/panoptes/internal/game/scenario"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

func loadRealContentCatalog(t *testing.T) *staticdata.Catalog {
	t.Helper()

	_, currentFile, _, ok := runtime.Caller(0)
	if !ok {
		t.Fatalf("runtime.Caller() failed")
	}
	repoRoot := filepath.Clean(filepath.Join(filepath.Dir(currentFile), "..", "..", ".."))
	catalog, err := staticdata.LoadDir(filepath.Join(repoRoot, "data", "generated", "server"))
	if err != nil {
		t.Fatalf("LoadDir(real content) error = %v", err)
	}
	return catalog
}

func newRealContentHappyPathDefinition(t *testing.T) *scenario.Definition {
	t.Helper()

	catalog := loadRealContentCatalog(t)
	staticdata.SetDefault(catalog)

	playerIDs := []string{"player-1", "player-2"}
	usernames := []string{"alice", "bob"}
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, realContentHappyPathMap(), playerIDs)
	state := domain.NewGameState("real-content-mvp-happy-path", playerIDs, usernames, mapData)
	state.World = world

	player := state.Players["player-1"]
	player.Resources.Set(domain.ResourceFood, 2)
	player.Resources.Set(domain.ResourceWood, 3)
	player.Resources.Set(domain.ResourceOre, 2)
	player.Research.UnlockRecipe("city_core_settler")
	player.Research.UnlockBuilding("barracks")
	player.Research.UnlockRecipe("barracks_infantry")

	frontierOfficeEntry, ok := state.GetNode("B1")
	if !ok {
		t.Fatalf("missing node B1")
	}
	ecs.CreateBuilding(state.World, "frontier_office", "player-1", "A2", frontierOfficeEntry)
	if !frontierOfficeEntry.HasComponent(ecs.BuildingOperationC) {
		t.Fatalf("frontier office B1 missing BuildingOperationC")
	}
	frontierOperation := ecs.BuildingOperationC.Get(frontierOfficeEntry)
	frontierOperation.ProgressRemainder = 500

	clearSelectedRecipe(t, state, "G2")

	return &scenario.Definition{
		Name:      "real_content_mvp_happy_path",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}
}

func newRealContentFacilityTakeoverDefinition(t *testing.T) *scenario.Definition {
	t.Helper()

	catalog := loadRealContentCatalog(t)
	staticdata.SetDefault(catalog)

	playerIDs := []string{"player-1", "player-2"}
	usernames := []string{"alice", "bob"}
	world := donburi.NewWorld()
	mapData := maploader.InitWorldFromMap(world, realContentFacilityTakeoverMap(), playerIDs)
	state := domain.NewGameState("real-content-facility-takeover", playerIDs, usernames, mapData)
	state.World = world

	for _, playerID := range playerIDs {
		state.Players[playerID].Research.UnlockBuilding("farm")
		state.Players[playerID].Research.UnlockRecipe("farm_food")
		clearSelectedRecipe(t, state, capitalNodeID(playerID))
	}

	farmEntry, ok := state.GetNode("C2")
	if !ok {
		t.Fatalf("missing node C2")
	}
	ecs.CreateBuilding(state.World, "farm", "player-1", "A2", farmEntry)
	unitEntry := state.World.Entry(ecs.CreateUnit(state.World, "infantry", "player-2", domain.Position{X: 2, Y: 1}))
	ecs.UnitStatsC.Get(unitEntry).ID = "enemy-infantry-1"

	return &scenario.Definition{
		Name:      "real_content_facility_takeover",
		Catalog:   catalog,
		State:     state,
		PlayerIDs: []string{"player-1"},
		Usernames: []string{"alice"},
	}
}

func realContentHappyPathMap() *staticdata.MapRuntimeBundle {
	zero := 0
	one := 1
	nodes := realContentAllPlainNodes(7, 3)
	setNodeNodeConfig(nodes, "A2", func(node *staticdata.MapRuntimeNode) {
		node.OwnerSlot = &zero
		node.TerritoryOwnerSlot = &zero
		node.BuildingType = "city_core"
	})
	setNodeNodeConfig(nodes, "B2", func(node *staticdata.MapRuntimeNode) {
		node.IsResourcePoint = true
		node.ResourceType = "food"
		node.NodeName = "农田"
	})
	setNodeNodeConfig(nodes, "B1", func(node *staticdata.MapRuntimeNode) {
		node.NodeName = "拓土司位"
	})
	setNodeNodeConfig(nodes, "D2", func(node *staticdata.MapRuntimeNode) {
		node.NodeName = "新城选址"
	})
	setNodeNodeConfig(nodes, "E2", func(node *staticdata.MapRuntimeNode) {
		node.NodeName = "兵营位"
	})
	setNodeNodeConfig(nodes, "F2", func(node *staticdata.MapRuntimeNode) {
		node.NodeName = "前线"
	})
	setNodeNodeConfig(nodes, "G2", func(node *staticdata.MapRuntimeNode) {
		node.OwnerSlot = &one
		node.TerritoryOwnerSlot = &one
		node.BuildingType = "city_core"
		node.BuildingHP = 10
	})

	return &staticdata.MapRuntimeBundle{
		ID:     "real-content-mvp-happy-path",
		Name:   "Real Content MVP Happy Path",
		Width:  7,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 1},
			{Slot: 1, X: 6, Y: 1},
		},
		Nodes: nodes,
		NamedNodes: map[string]string{
			"B1": "拓土司位",
			"B2": "农田",
			"D2": "新城选址",
			"E2": "兵营位",
			"F2": "前线",
		},
	}
}

func realContentFacilityTakeoverMap() *staticdata.MapRuntimeBundle {
	zero := 0
	one := 1
	nodes := realContentAllPlainNodes(5, 3)
	setNodeNodeConfig(nodes, "A2", func(node *staticdata.MapRuntimeNode) {
		node.OwnerSlot = &zero
		node.TerritoryOwnerSlot = &zero
		node.BuildingType = "city_core"
	})
	setNodeNodeConfig(nodes, "C2", func(node *staticdata.MapRuntimeNode) {
		node.IsResourcePoint = true
		node.ResourceType = "food"
		node.NodeName = "争议农田"
	})
	setNodeNodeConfig(nodes, "E2", func(node *staticdata.MapRuntimeNode) {
		node.OwnerSlot = &one
		node.TerritoryOwnerSlot = &one
		node.BuildingType = "city_core"
	})

	return &staticdata.MapRuntimeBundle{
		ID:     "real-content-facility-takeover",
		Name:   "Real Content Facility Takeover",
		Width:  5,
		Height: 3,
		SpawnPoints: []staticdata.SpawnPoint{
			{Slot: 0, X: 0, Y: 1},
			{Slot: 1, X: 4, Y: 1},
		},
		Nodes: nodes,
		NamedNodes: map[string]string{
			"C2": "争议农田",
		},
	}
}

func clearSelectedRecipe(t *testing.T, state *domain.GameState, nodeID string) {
	t.Helper()

	entry, ok := state.GetNode(nodeID)
	if !ok {
		t.Fatalf("missing node %s", nodeID)
	}
	if !entry.HasComponent(ecs.BuildingOperationC) {
		return
	}
	operation := ecs.BuildingOperationC.Get(entry)
	operation.SelectedRecipeID = ""
	operation.RequiredTurns = 0
	operation.ProgressTurns = 0
	operation.ProgressRemainder = 0
	operation.ConsumedResources = domain.NewResourceBag()
	operation.ConsumedPoints = domain.NewPointBag()
}

func capitalNodeID(playerID string) string {
	switch playerID {
	case "player-1":
		return "A2"
	case "player-2":
		return "E2"
	default:
		return ""
	}
}

func findOwnedUnitIDByType(t *testing.T, state *domain.GameState, owner string, unitType string) string {
	t.Helper()

	unitID := findOwnedUnitIDByTypeIfExists(state, owner, unitType)
	if unitID == "" {
		t.Fatalf("unit not found owner=%s type=%s", owner, unitType)
	}
	return unitID
}

func findOwnedUnitIDByTypeIfExists(state *domain.GameState, owner string, unitType string) string {
	unitID := ""
	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if unitID != "" || entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction == owner && string(stats.Type) == unitType {
			unitID = stats.ID
		}
	})
	return unitID
}

func assertUnitAtNode(t *testing.T, state *domain.GameState, unitID string, wantNodeID string) {
	t.Helper()

	entry, ok := findUnitEntryByIDForTest(state.World, unitID)
	if !ok {
		t.Fatalf("unit %s not found", unitID)
	}
	pos := ecs.PositionC.Get(entry)
	nodeEntry, ok := domain.GetNodeAt(state.World, domain.Position{X: pos.X, Y: pos.Y})
	if !ok {
		t.Fatalf("node for unit %s not found", unitID)
	}
	if got := ecs.NodeC.Get(nodeEntry).ID; got != wantNodeID {
		t.Fatalf("unit %s node = %q, want %q", unitID, got, wantNodeID)
	}
}

func settlementNodeView(t *testing.T, msg *pb.MsgTurnSettlement, nodeID string) *pb.NodeView {
	t.Helper()

	if msg == nil {
		t.Fatalf("turn settlement is nil")
	}
	for _, node := range msg.GetNodes() {
		if node.GetId() == nodeID {
			return node
		}
	}
	t.Fatalf("node %s not found in settlement", nodeID)
	return nil
}

func findUnitEntryByIDForTest(world donburi.World, unitID string) (*donburi.Entry, bool) {
	var found *donburi.Entry
	ecs.AllUnits(world).Each(world, func(entry *donburi.Entry) {
		if found != nil || entry == nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).ID == unitID {
			found = entry
		}
	})
	return found, found != nil
}

func realContentAllPlainNodes(width int, height int) []staticdata.MapRuntimeNode {
	nodes := make([]staticdata.MapRuntimeNode, 0, width*height)
	for y := 0; y < height; y++ {
		for x := 0; x < width; x++ {
			nodes = append(nodes, staticdata.MapRuntimeNode{
				ID:      realContentNodeID(x, y),
				X:       x,
				Y:       y,
				Terrain: "plain",
			})
		}
	}
	return nodes
}

func setNodeNodeConfig(nodes []staticdata.MapRuntimeNode, nodeID string, apply func(node *staticdata.MapRuntimeNode)) {
	for idx := range nodes {
		if nodes[idx].ID != nodeID {
			continue
		}
		apply(&nodes[idx])
		return
	}
	panic(fmt.Sprintf("node %s not found", nodeID))
}

func realContentNodeID(x int, y int) string {
	return string(rune('A'+x)) + fmt.Sprintf("%d", y+1)
}
