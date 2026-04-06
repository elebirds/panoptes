package strategy

// ---------------------------------------------------------------------------
// 世界状态（领域语义）
// ---------------------------------------------------------------------------

type TerrainTile struct {
	X       int    `json:"x"`
	Y       int    `json:"y"`
	Terrain string `json:"terrain"`
	HasRoad bool   `json:"has_road"`
}

type UnitInfo struct {
	ID       string `json:"id"`
	Faction  string `json:"faction"`
	UnitType string `json:"unit_type"`
	HP       int    `json:"hp"`
	MaxHP    int    `json:"max_hp"`
	X        int    `json:"x"`
	Y        int    `json:"y"`
}

type BuildingInfo struct {
	ID           string `json:"id"`
	BuildingType string `json:"building_type"`
	Faction      string `json:"faction"`
	X            int    `json:"x"`
	Y            int    `json:"y"`
}

type WorldState struct {
	Terrain   [][]TerrainTile `json:"terrain"`
	Units     []UnitInfo      `json:"units"`
	Buildings []BuildingInfo  `json:"buildings"`
}

type UnitState struct {
	UnitID     string `json:"unit_id"`
	UnitType   string `json:"unit_type"`
	Faction    string `json:"faction"`
	X          int    `json:"x"`
	Y          int    `json:"y"`
	MovePoints int    `json:"move_points"`
}
