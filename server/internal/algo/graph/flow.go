package graph

type Edge struct {
	To       int
	Rev      int
	Capacity int
}

type MaxFlow struct {
	adj [][]Edge
}

func NewMaxFlow(n int) *MaxFlow {
	return &MaxFlow{adj: make([][]Edge, n)}
}

func (f *MaxFlow) AddEdge(from, to, capacity int) {
	fwd := Edge{To: to, Rev: len(f.adj[to]), Capacity: capacity}
	rev := Edge{To: from, Rev: len(f.adj[from]), Capacity: 0}
	f.adj[from] = append(f.adj[from], fwd)
	f.adj[to] = append(f.adj[to], rev)
}

func (f *MaxFlow) Solve(source, sink int) int {
	flow := 0
	for {
		parentV := make([]int, len(f.adj))
		parentE := make([]int, len(f.adj))
		for i := range parentV {
			parentV[i] = -1
		}
		queue := []int{source}
		parentV[source] = source
		for len(queue) > 0 && parentV[sink] == -1 {
			v := queue[0]
			queue = queue[1:]
			for ei, e := range f.adj[v] {
				if e.Capacity <= 0 || parentV[e.To] != -1 {
					continue
				}
				parentV[e.To] = v
				parentE[e.To] = ei
				queue = append(queue, e.To)
			}
		}
		if parentV[sink] == -1 {
			break
		}
		aug := int(^uint(0) >> 1)
		for v := sink; v != source; v = parentV[v] {
			e := f.adj[parentV[v]][parentE[v]]
			if e.Capacity < aug {
				aug = e.Capacity
			}
		}
		for v := sink; v != source; v = parentV[v] {
			pv := parentV[v]
			pe := parentE[v]
			rev := f.adj[pv][pe].Rev
			f.adj[pv][pe].Capacity -= aug
			f.adj[v][rev].Capacity += aug
		}
		flow += aug
	}
	return flow
}
