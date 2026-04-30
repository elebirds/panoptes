package query

import (
	"sort"
	"strings"
	"sync"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
	"google.golang.org/protobuf/proto"
)

type NodeObservationView = pb.NodeView

type RememberedUnitView struct {
	View             *pb.UnitView
	LastObservedTurn int32
}

func (r *RememberedUnitView) GetView() *pb.UnitView {
	if r == nil {
		return nil
	}
	return r.View
}

func (r *RememberedUnitView) GetLastObservedTurn() int32 {
	if r == nil {
		return 0
	}
	return r.LastObservedTurn
}

type ObservationSnapshot struct {
	ViewerID       string
	MyPlayer       *pb.PlayerView
	Nodes          []*pb.NodeView
	VisibleNodes   []*pb.NodeView
	MemoryNodes    []*pb.NodeView
	Units          []*pb.UnitView
	MemoryUnits    []*RememberedUnitView
	VisibleNodeIDs map[string]struct{}
}

type ObservationStore struct {
	mu         sync.Mutex
	nodeMemory map[string]map[string]storedNodeObservation
	unitMemory map[string]map[string]storedUnitObservation
	omniscient map[string]bool
}

type storedNodeObservation struct {
	View             *pb.NodeView
	LastObservedTurn int32
}

type storedUnitObservation struct {
	View             *pb.UnitView
	LastObservedTurn int32
}

func NewObservationStore() *ObservationStore {
	return &ObservationStore{
		nodeMemory: make(map[string]map[string]storedNodeObservation),
		unitMemory: make(map[string]map[string]storedUnitObservation),
		omniscient: make(map[string]bool),
	}
}

func (s *ObservationStore) Reset() {
	if s == nil {
		return
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	s.nodeMemory = make(map[string]map[string]storedNodeObservation)
	s.unitMemory = make(map[string]map[string]storedUnitObservation)
	s.omniscient = make(map[string]bool)
}

func (s *ObservationStore) SetOmniscient(viewerID string, enabled bool) {
	if s == nil {
		return
	}
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		return
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	if s.omniscient == nil {
		s.omniscient = make(map[string]bool)
	}
	if enabled {
		s.omniscient[viewerID] = true
		return
	}
	delete(s.omniscient, viewerID)
}

func (s *ObservationStore) IsOmniscient(viewerID string) bool {
	if s == nil {
		return false
	}
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		return false
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	return s.omniscient[viewerID]
}

func (s *ObservationStore) BuildObservation(state *domain.GameState, viewerID string) *ObservationSnapshot {
	snapshot := &ObservationSnapshot{
		ViewerID:       strings.TrimSpace(viewerID),
		MyPlayer:       BuildPlayerView(state, viewerID),
		VisibleNodeIDs: make(map[string]struct{}),
	}
	if state == nil || state.World == nil {
		return snapshot
	}

	visibleNodeIDs := computeVisibleNodeIDs(state, viewerID)
	if s != nil && s.IsOmniscient(viewerID) {
		visibleNodeIDs = allNodeIDs(state)
	}
	for nodeID := range visibleNodeIDs {
		snapshot.VisibleNodeIDs[nodeID] = struct{}{}
	}

	if s == nil {
		s = NewObservationStore()
	}

	s.mu.Lock()
	defer s.mu.Unlock()

	nodeMemory := s.ensureNodeMemory(viewerID)
	unitMemory := s.ensureUnitMemory(viewerID)
	liveUnitIDs := make(map[string]struct{})
	visibleUnitIDs := make(map[string]struct{})

	ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		nodeID := strings.TrimSpace(ecs.NodeC.Get(entry).ID)
		if nodeID == "" {
			return
		}
		if _, ok := visibleNodeIDs[nodeID]; ok {
			view := BuildNodeView(state, entry, viewerID)
			annotateCurrentNodeView(view, int32(state.Turn))
			snapshot.Nodes = append(snapshot.Nodes, view)
			snapshot.VisibleNodes = append(snapshot.VisibleNodes, cloneNodeView(view))
			nodeMemory[nodeID] = storedNodeObservation{
				View:             cloneNodeView(view),
				LastObservedTurn: int32(state.Turn),
			}
			return
		}
		if remembered, ok := nodeMemory[nodeID]; ok && remembered.View != nil {
			view := cloneNodeView(remembered.View)
			annotateMemoryNodeView(view, remembered.LastObservedTurn)
			snapshot.Nodes = append(snapshot.Nodes, view)
			snapshot.MemoryNodes = append(snapshot.MemoryNodes, cloneNodeView(view))
			return
		}
		snapshot.Nodes = append(snapshot.Nodes, buildUnknownNodeView(state, entry, viewerID))
	})

	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		view := buildUnitViewFromEntry(entry)
		if view == nil || strings.TrimSpace(view.GetId()) == "" {
			return
		}
		liveUnitIDs[view.GetId()] = struct{}{}
		if !unitVisibleToPlayer(state, entry, visibleNodeIDs) {
			return
		}
		snapshot.Units = append(snapshot.Units, view)
		visibleUnitIDs[view.GetId()] = struct{}{}
		unitMemory[view.GetId()] = storedUnitObservation{
			View:             cloneUnitView(view),
			LastObservedTurn: int32(state.Turn),
		}
	})

	for unitID, remembered := range unitMemory {
		if _, ok := liveUnitIDs[unitID]; !ok {
			delete(unitMemory, unitID)
			continue
		}
		if _, ok := visibleUnitIDs[unitID]; ok {
			continue
		}
		if remembered.View == nil {
			continue
		}
		snapshot.MemoryUnits = append(snapshot.MemoryUnits, &RememberedUnitView{
			View:             cloneUnitView(remembered.View),
			LastObservedTurn: remembered.LastObservedTurn,
		})
	}

	sort.Slice(snapshot.Nodes, func(i, j int) bool { return snapshot.Nodes[i].GetId() < snapshot.Nodes[j].GetId() })
	sort.Slice(snapshot.VisibleNodes, func(i, j int) bool { return snapshot.VisibleNodes[i].GetId() < snapshot.VisibleNodes[j].GetId() })
	sort.Slice(snapshot.MemoryNodes, func(i, j int) bool { return snapshot.MemoryNodes[i].GetId() < snapshot.MemoryNodes[j].GetId() })
	sort.Slice(snapshot.Units, func(i, j int) bool { return snapshot.Units[i].GetId() < snapshot.Units[j].GetId() })
	sort.Slice(snapshot.MemoryUnits, func(i, j int) bool {
		return snapshot.MemoryUnits[i].View.GetId() < snapshot.MemoryUnits[j].View.GetId()
	})
	return snapshot
}

func (s *ObservationStore) RevealNodeView(state *domain.GameState, viewerID string, nodeID string) *pb.NodeView {
	if state == nil {
		return nil
	}
	entry, ok := state.GetNode(strings.TrimSpace(nodeID))
	if !ok || entry == nil {
		return nil
	}
	view := BuildNodeView(state, entry, viewerID)
	if view == nil {
		return nil
	}
	if s != nil && s.IsOmniscient(viewerID) {
		annotateCurrentNodeView(view, int32(state.Turn))
		return view
	}
	annotateMemoryNodeView(view, int32(state.Turn))
	if s == nil {
		return view
	}
	s.mu.Lock()
	defer s.mu.Unlock()
	nodeMemory := s.ensureNodeMemory(viewerID)
	nodeMemory[strings.TrimSpace(nodeID)] = storedNodeObservation{
		View:             cloneNodeView(view),
		LastObservedTurn: int32(state.Turn),
	}
	return view
}

func (s *ObservationStore) ensureNodeMemory(viewerID string) map[string]storedNodeObservation {
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		viewerID = "__omniscient__"
	}
	if s.nodeMemory == nil {
		s.nodeMemory = make(map[string]map[string]storedNodeObservation)
	}
	if s.nodeMemory[viewerID] == nil {
		s.nodeMemory[viewerID] = make(map[string]storedNodeObservation)
	}
	return s.nodeMemory[viewerID]
}

func (s *ObservationStore) ensureUnitMemory(viewerID string) map[string]storedUnitObservation {
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		viewerID = "__omniscient__"
	}
	if s.unitMemory == nil {
		s.unitMemory = make(map[string]map[string]storedUnitObservation)
	}
	if s.unitMemory[viewerID] == nil {
		s.unitMemory[viewerID] = make(map[string]storedUnitObservation)
	}
	return s.unitMemory[viewerID]
}

func computeVisibleNodeIDs(state *domain.GameState, viewerID string) map[string]struct{} {
	visible := make(map[string]struct{})
	if state == nil || state.World == nil {
		return visible
	}
	viewerID = strings.TrimSpace(viewerID)
	if viewerID == "" {
		ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
			if entry == nil {
				return
			}
			visible[strings.TrimSpace(ecs.NodeC.Get(entry).ID)] = struct{}{}
		})
		return visible
	}

	type visionSource struct {
		pos    domain.Position
		range_ int
	}
	sources := make([]visionSource, 0)

	ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		node := ecs.NodeC.Get(entry)
		nodeID := strings.TrimSpace(node.ID)
		if nodeID == "" {
			return
		}
		if node.Owner == viewerID || node.TerritoryOwner == viewerID {
			visible[nodeID] = struct{}{}
			return
		}
		if entry.HasComponent(ecs.BuildingC) && ecs.BuildingC.Get(entry).Owner == viewerID {
			visible[nodeID] = struct{}{}
		}
	})

	ecs.AllUnits(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Faction != viewerID {
			return
		}
		pos := ecs.PositionC.Get(entry)
		sources = append(sources, visionSource{
			pos:    domain.Position{Q: pos.Q, R: pos.R},
			range_: unitVisionRange(stats.Type),
		})
	})

	if len(sources) == 0 {
		return visible
	}

	ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		nodeID := strings.TrimSpace(ecs.NodeC.Get(entry).ID)
		if nodeID == "" {
			return
		}
		pos := ecs.PositionC.Get(entry)
		nodePos := domain.Position{Q: pos.Q, R: pos.R}
		for _, source := range sources {
			if nodePos.DistanceTo(source.pos) <= source.range_ {
				visible[nodeID] = struct{}{}
				return
			}
		}
	})

	return visible
}

func unitVisionRange(unitType domain.UnitType) int {
	cfg, ok := staticdata.Default().GetUnit(string(unitType))
	if !ok {
		return 1
	}
	if cfg.VisionRange <= 0 {
		return 1
	}
	return cfg.VisionRange
}

func unitVisibleToPlayer(state *domain.GameState, entry *donburi.Entry, visibleNodeIDs map[string]struct{}) bool {
	if state == nil || entry == nil {
		return false
	}
	pos := ecs.PositionC.Get(entry)
	nodeEntry, ok := domain.GetNodeAt(state.World, domain.Position{Q: pos.Q, R: pos.R})
	if !ok || nodeEntry == nil {
		return false
	}
	nodeID := strings.TrimSpace(ecs.NodeC.Get(nodeEntry).ID)
	_, ok = visibleNodeIDs[nodeID]
	return ok
}

func allNodeIDs(state *domain.GameState) map[string]struct{} {
	visible := make(map[string]struct{})
	if state == nil || state.World == nil {
		return visible
	}
	ecs.AllNodes(state.World).Each(state.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		nodeID := strings.TrimSpace(ecs.NodeC.Get(entry).ID)
		if nodeID == "" {
			return
		}
		visible[nodeID] = struct{}{}
	})
	return visible
}

func buildUnknownNodeView(state *domain.GameState, entry *donburi.Entry, viewerID string) *pb.NodeView {
	if state == nil || entry == nil {
		return nil
	}
	node := ecs.NodeC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	return &pb.NodeView{
		Id:                     node.ID,
		Pos:                    &pb.Position{Q: int32(pos.Q), R: int32(pos.R)},
		Terrain:                string(node.Terrain),
		HasRoad:                node.HasRoad,
		RoadStatus:             string(domain.RoadStatusForNode(state, node.ID)),
		NetworkStatus:          domain.NetworkStatusUnknown,
		IsResourcePoint:        node.IsResource,
		ResourceType:           node.ResourceType,
		IsSafeZone:             domain.IsInSafeZone(state, domain.Position{Q: pos.Q, R: pos.R}, viewerID),
		BuildingStatus:         "unknown",
		IsCurrentlyVisible:     false,
		IsMemory:               false,
		LastObservedTurn:       0,
		MyUnitCount:            0,
		EnemyUnitCount:         0,
		ControllerPlayerId:     "",
		TerritoryOwnerPlayerId: "",
	}
}

func buildUnitViewFromEntry(entry *donburi.Entry) *pb.UnitView {
	if entry == nil {
		return nil
	}
	stats := ecs.UnitStatsC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	return &pb.UnitView{
		Id:       stats.ID,
		Faction:  stats.Faction,
		UnitType: string(stats.Type),
		Hp:       int32(stats.HP),
		MaxHp:    int32(stats.MaxHP),
		Pos:      &pb.Position{Q: int32(pos.Q), R: int32(pos.R)},
	}
}

func annotateCurrentNodeView(view *pb.NodeView, turn int32) {
	if view == nil {
		return
	}
	view.IsCurrentlyVisible = true
	view.IsMemory = false
	view.LastObservedTurn = turn
}

func annotateMemoryNodeView(view *pb.NodeView, turn int32) {
	if view == nil {
		return
	}
	view.IsCurrentlyVisible = false
	view.IsMemory = true
	view.LastObservedTurn = turn
}

func cloneNodeView(view *pb.NodeView) *pb.NodeView {
	if view == nil {
		return nil
	}
	cloned, ok := proto.Clone(view).(*pb.NodeView)
	if !ok {
		return nil
	}
	return cloned
}

func cloneUnitView(view *pb.UnitView) *pb.UnitView {
	if view == nil {
		return nil
	}
	cloned, ok := proto.Clone(view).(*pb.UnitView)
	if !ok {
		return nil
	}
	return cloned
}
