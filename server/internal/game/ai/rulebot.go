package ai

import (
	"context"
	"math/rand"
	"slices"
	"sort"
	"strings"

	"github.com/elebirds/panoptes/internal/domain"
	"github.com/elebirds/panoptes/internal/ecs"
	gameorders "github.com/elebirds/panoptes/internal/game/orders"
	"github.com/elebirds/panoptes/internal/game/planning"
	gamequery "github.com/elebirds/panoptes/internal/game/query"
	pb "github.com/elebirds/panoptes/internal/gen/proto"
	"github.com/elebirds/panoptes/internal/staticdata"
	"github.com/yohamta/donburi"
)

const (
	intMax = int(^uint(0) >> 1)
	intMin = -intMax - 1
)

func (RuleBotProvider) BuildPlanningIntents(_ context.Context, req Request) ([]planning.Intent, error) {
	if req.State == nil || req.Participant.ID == "" {
		return []planning.Intent{planning.SubmitTurnIntent{}}, nil
	}

	planner := newRuleBotPlanner(req)
	intents := make([]planning.Intent, 0, 16)

	if intent, ok := planner.chooseResearchIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseNationalPolicyIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseInstitutionIntent(); ok {
		intents = append(intents, intent)
	}
	if intent, ok := planner.chooseBuildIntent(); ok {
		intents = append(intents, intent)
	}
	intents = append(intents, planner.chooseRecipeIntents()...)
	intents = append(intents, planner.chooseExpansionIntents()...)
	intents = append(intents, planner.chooseCombatIntents()...)
	intents = append(intents, planning.SubmitTurnIntent{})
	return intents, nil
}

type ruleBotPlanner struct {
	req          Request
	rng          *rand.Rand
	player       *domain.PlayerState
	observation  *gamequery.ObservationSnapshot
	playerID     string
	threatLevel  int
	reservedUnit map[string]struct{}
}

type scoredIntent struct {
	key    string
	score  int
	intent planning.Intent
}

type buildCandidate struct {
	key            string
	score          int
	nodeID         string
	buildingTypeID string
	cityID         string
}

type recipeCandidate struct {
	key      string
	score    int
	nodeID   string
	recipeID string
}

type expansionCandidate struct {
	key        string
	score      int
	unitID     string
	targetNode string
}

type combatCandidate struct {
	key    string
	score  int
	intent planning.Intent
}

func newRuleBotPlanner(req Request) *ruleBotPlanner {
	rng := req.RNG
	if rng == nil {
		rng = rand.New(rand.NewSource(1))
	}
	observation := req.Observation
	if observation == nil {
		observation = gamequery.NewObservationStore().BuildObservation(req.State, req.Participant.ID)
	}
	planner := &ruleBotPlanner{
		req:          req,
		rng:          rng,
		player:       req.State.Players[req.Participant.ID],
		observation:  observation,
		playerID:     req.Participant.ID,
		reservedUnit: make(map[string]struct{}),
	}
	planner.threatLevel = planner.computeThreatLevel()
	return planner
}

func (p *ruleBotPlanner) chooseResearchIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if strings.TrimSpace(p.req.State.TurnRuntime.Planning.PendingResearchTarget(p.playerID)) != "" {
		return nil, false
	}
	if strings.TrimSpace(p.player.Research.CurrentTargetTechnologyID) != "" {
		return nil, false
	}

	candidates := make([]scoredIntent, 0)
	for _, tech := range staticdata.Default().Technologies() {
		techID := strings.TrimSpace(tech.ID)
		if techID == "" || p.player.Research.HasTechnology(techID) || p.player.Research.HasCompletedTechnology(techID) {
			continue
		}
		if !prerequisitesMet(p.req.State, p.playerID, tech.Prerequisites) {
			continue
		}
		score := p.scoreTechnology(tech)
		candidates = append(candidates, scoredIntent{
			key:   techID,
			score: score,
			intent: planning.SetResearchTargetIntent{
				TechnologyID: techID,
			},
		})
	}
	intent, ok := pickBestScoredIntent(p.rng, candidates)
	return intent, ok
}

func (p *ruleBotPlanner) chooseNationalPolicyIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if pending := strings.TrimSpace(string(p.req.State.TurnRuntime.Planning.PendingPolicy(p.playerID))); pending != "" {
		return nil, false
	}

	candidates := make([]scoredIntent, 0)
	for _, policy := range staticdata.Default().Policies() {
		if !strings.EqualFold(policy.Layer, "national") {
			continue
		}
		policyID := strings.TrimSpace(policy.ID)
		if policyID == "" || !prerequisitesMet(p.req.State, p.playerID, policy.Prerequisites) {
			continue
		}
		score := p.scoreNationalPolicy(policy)
		if domain.Policy(policyID) == p.player.Policy {
			score -= 20
		}
		candidates = append(candidates, scoredIntent{
			key:   policyID,
			score: score,
			intent: planning.SetPolicyIntent{
				NationalPolicyID: policyID,
			},
		})
	}
	intent, ok := pickBestScoredIntent(p.rng, candidates)
	if !ok {
		return nil, false
	}
	selected, ok := intent.(planning.SetPolicyIntent)
	if !ok || domain.Policy(selected.NationalPolicyID) == p.player.Policy {
		return nil, false
	}
	return intent, true
}

func (p *ruleBotPlanner) chooseInstitutionIntent() (planning.Intent, bool) {
	if p.player == nil || p.req.State == nil {
		return nil, false
	}
	if p.req.State.TurnRuntime.Planning.HasPendingInstitutionLoadout(p.playerID) {
		return nil, false
	}
	slotCount := p.player.Institutions.SlotCount
	if slotCount <= 0 {
		return nil, false
	}

	candidates := make([]struct {
		key   string
		score int
	}, 0)
	for _, policyID := range p.player.Institutions.CandidateIDs() {
		policy, ok := staticdata.Default().GetPolicy(policyID)
		if !ok || !strings.EqualFold(policy.Layer, "institutional") || !prerequisitesMet(p.req.State, p.playerID, policy.Prerequisites) {
			continue
		}
		candidates = append(candidates, struct {
			key   string
			score int
		}{key: policyID, score: p.scoreInstitutionPolicy(policy)})
	}
	if len(candidates) == 0 {
		return nil, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	threshold := candidates[0].score
	tied := candidates[:0]
	for _, candidate := range candidates {
		if candidate.score < threshold {
			break
		}
		tied = append(tied, candidate)
	}
	if len(tied) > slotCount {
		p.rng.Shuffle(len(tied), func(i, j int) {
			tied[i], tied[j] = tied[j], tied[i]
		})
		tied = tied[:slotCount]
	}
	selected := make([]string, 0, min(slotCount, len(candidates)))
	if len(tied) > 0 {
		for _, candidate := range tied {
			selected = append(selected, candidate.key)
		}
	}
	if len(selected) < slotCount {
		for _, candidate := range candidates[len(tied):] {
			selected = append(selected, candidate.key)
			if len(selected) >= slotCount {
				break
			}
		}
	}
	selected = domain.NormalizePolicyIDList(selected)
	if slices.Equal(selected, p.player.Institutions.ActivePolicyIDs) {
		return nil, false
	}
	return planning.SetInstitutionLoadoutIntent{PolicyIDs: selected}, true
}

func (p *ruleBotPlanner) chooseBuildIntent() (planning.Intent, bool) {
	if p.req.State == nil || p.player == nil {
		return nil, false
	}
	candidates := make([]buildCandidate, 0)
	for _, node := range p.observation.VisibleNodes {
		if node == nil || node.GetBuildingTypeId() != "" || node.GetTerritoryOwnerPlayerId() != p.playerID {
			continue
		}
		for _, building := range p.availableBuildingsForNode(node) {
			score := p.scoreBuild(node, building)
			if score <= 0 {
				continue
			}
			candidates = append(candidates, buildCandidate{
				key:            node.GetId() + ":" + building.ID,
				score:          score,
				nodeID:         node.GetId(),
				buildingTypeID: building.ID,
				cityID:         p.closestCityID(node.GetId()),
			})
		}
	}
	chosen, ok := pickBestBuildCandidate(p.rng, candidates)
	if !ok {
		return nil, false
	}
	return planning.BuildStructureIntent{
		NodeID:         chosen.nodeID,
		BuildingTypeID: chosen.buildingTypeID,
		CityID:         chosen.cityID,
	}, true
}

func (p *ruleBotPlanner) chooseRecipeIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	for _, node := range p.observation.VisibleNodes {
		if node == nil || node.GetControllerPlayerId() != p.playerID || node.GetBuildingTypeId() == "" {
			continue
		}
		recipe, ok := p.chooseRecipeForNode(node)
		if !ok {
			continue
		}
		current := ""
		if node.GetOperation() != nil {
			current = node.GetOperation().GetSelectedRecipeId()
		}
		if recipe.recipeID == current {
			continue
		}
		intents = append(intents, planning.SetBuildingRecipeIntent{
			NodeID:   node.GetId(),
			RecipeID: recipe.recipeID,
		})
	}
	return intents
}

func (p *ruleBotPlanner) chooseExpansionIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	candidates := make([]expansionCandidate, 0)
	for _, entry := range p.ownedUnitEntries() {
		stats := ecs.UnitStatsC.Get(entry)
		if stats.Type != domain.UnitTypeSettler {
			continue
		}
		unitID := strings.TrimSpace(stats.ID)
		if unitID == "" {
			continue
		}
		for _, node := range p.observation.VisibleNodes {
			if node == nil || node.GetBuildingTypeId() != "" {
				continue
			}
			nodeEntry, ok := p.req.State.GetNode(node.GetId())
			if !ok || nodeEntry == nil {
				continue
			}
			if canFound, _ := ecs.CanFoundCityAt(p.req.State, nodeEntry); !canFound {
				continue
			}
			score := p.scoreExpansion(stats.ID, node.GetId())
			if score <= 0 {
				continue
			}
			candidates = append(candidates, expansionCandidate{
				key:        unitID + ":" + node.GetId(),
				score:      score,
				unitID:     unitID,
				targetNode: node.GetId(),
			})
		}
	}
	chosen, ok := pickBestExpansionCandidate(p.rng, candidates)
	if !ok {
		return nil
	}
	p.reservedUnit[chosen.unitID] = struct{}{}
	return []planning.Intent{
		planning.IssueUnitOrderIntent{
			UnitID:       chosen.unitID,
			Action:       string(gameorders.ActionSettleCity),
			TargetNodeID: chosen.targetNode,
		},
	}
}

func (p *ruleBotPlanner) chooseCombatIntents() []planning.Intent {
	if p.req.State == nil {
		return nil
	}
	intents := make([]planning.Intent, 0)
	visibleEnemies := p.visibleEnemyUnits()
	memoryEnemies := p.memoryEnemyUnits()
	enemyStructures := p.enemyStructureNodes()

	for _, entry := range p.ownedUnitEntries() {
		stats := ecs.UnitStatsC.Get(entry)
		unitID := strings.TrimSpace(stats.ID)
		if unitID == "" || stats.Type == domain.UnitTypeSettler {
			continue
		}
		if _, reserved := p.reservedUnit[unitID]; reserved {
			continue
		}
		candidate := p.chooseCombatIntentForUnit(entry, visibleEnemies, memoryEnemies, enemyStructures)
		intents = append(intents, candidate.intent)
	}
	return intents
}

func (p *ruleBotPlanner) chooseCombatIntentForUnit(entry *donburi.Entry, visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) combatCandidate {
	stats := ecs.UnitStatsC.Get(entry)
	pos := ecs.PositionC.Get(entry)
	unitPos := domain.Position{X: pos.X, Y: pos.Y}
	unitID := strings.TrimSpace(stats.ID)

	bestAttack := combatCandidate{score: intMin, intent: planning.IssueUnitOrderIntent{UnitID: unitID, Action: string(gameorders.ActionHold)}}
	for _, enemy := range visibleEnemies {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		targetPos := domain.Position{X: int(enemy.GetPos().GetX()), Y: int(enemy.GetPos().GetY())}
		distance := unitPos.DistanceTo(targetPos)
		if distance > stats.AttackRange {
			continue
		}
		score := 100
		if int(enemy.GetHp()) <= stats.Attack {
			score += 80
		}
		if isCivilianUnit(enemy.GetUnitType()) {
			score += 40
		}
		if score > bestAttack.score {
			bestAttack = combatCandidate{
				key:   unitID + ":attack:" + enemy.GetId(),
				score: score,
				intent: planning.IssueUnitOrderIntent{
					UnitID:       unitID,
					Action:       string(gameorders.ActionAttack),
					TargetUnitID: enemy.GetId(),
				},
			}
		}
	}
	if bestAttack.score > intMin {
		return bestAttack
	}

	if entry.HasComponent(ecs.UnitCapabilitiesC) && ecs.UnitCapabilitiesC.Get(entry).CanAttackStructures {
		for _, node := range enemyStructures {
			if node == nil || node.GetPos() == nil {
				continue
			}
			targetPos := domain.Position{X: int(node.GetPos().GetX()), Y: int(node.GetPos().GetY())}
			if unitPos.DistanceTo(targetPos) > stats.AttackRange {
				continue
			}
			score := 70
			if node.GetIsCityCore() {
				score += 40
			}
			return combatCandidate{
				key:   unitID + ":attack_node:" + node.GetId(),
				score: score,
				intent: planning.IssueUnitOrderIntent{
					UnitID:       unitID,
					Action:       string(gameorders.ActionAttack),
					TargetNodeID: node.GetId(),
				},
			}
		}
	}

	targetNodeID := p.closestEnemyTargetNode(unitPos, visibleEnemies, memoryEnemies, enemyStructures)
	if targetNodeID != "" {
		return combatCandidate{
			key:   unitID + ":move:" + targetNodeID,
			score: 50,
			intent: planning.IssueUnitOrderIntent{
				UnitID:       unitID,
				Action:       string(gameorders.ActionMove),
				TargetNodeID: targetNodeID,
			},
		}
	}

	return combatCandidate{
		key:   unitID + ":hold",
		score: 1,
		intent: planning.IssueUnitOrderIntent{
			UnitID: unitID,
			Action: string(gameorders.ActionHold),
		},
	}
}

func (p *ruleBotPlanner) chooseRecipeForNode(node *pb.NodeView) (recipeCandidate, bool) {
	building, ok := staticdata.Default().GetBuilding(node.GetBuildingTypeId())
	if !ok {
		return recipeCandidate{}, false
	}
	candidates := make([]recipeCandidate, 0)
	for _, recipeID := range building.RecipeIDs {
		recipeID = strings.TrimSpace(recipeID)
		if recipeID == "" || !p.req.State.IsRecipeUnlocked(p.playerID, recipeID) {
			continue
		}
		recipe, ok := staticdata.Default().GetRecipe(recipeID)
		if !ok {
			continue
		}
		score := p.scoreRecipe(node, recipe)
		if score <= 0 {
			continue
		}
		candidates = append(candidates, recipeCandidate{
			key:      node.GetId() + ":" + recipeID,
			score:    score,
			nodeID:   node.GetId(),
			recipeID: recipeID,
		})
	}
	return pickBestRecipeCandidate(p.rng, candidates)
}

func (p *ruleBotPlanner) scoreTechnology(tech staticdata.TechnologyDefinition) int {
	score := 20 - tech.Tier
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"farm", "food", "agri", "agrarian"}, 120)
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"settler", "expan", "city"}, 80)
	score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"barracks", "infantry", "archer", "cavalry", "military", "war"}, 60)
	if p.threatLevel > 0 {
		score += matchKeywordScore(tech.ID, tech.Name, tech.Description, []string{"barracks", "infantry", "archer", "cavalry", "military", "war"}, 60)
	}
	for _, effect := range tech.ExplicitEffects {
		switch effect.Type {
		case "unlock_building":
			score += matchKeywordScore(effect.TargetID, "", "", []string{"farm", "food"}, 100)
			score += matchKeywordScore(effect.TargetID, "", "", []string{"barracks", "stable", "military"}, 55)
		case "unlock_recipe":
			score += matchKeywordScore(effect.TargetID, "", "", []string{"food", "settler"}, 70)
			score += matchKeywordScore(effect.TargetID, "", "", []string{"infantry", "archer", "cavalry"}, 55)
		case "institution_slots":
			score += 35
		}
	}
	return score
}

func (p *ruleBotPlanner) scoreNationalPolicy(policy staticdata.PolicyDefinition) int {
	score := 10
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"expan", "frontier", "colon"}, 50)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"recover", "food", "stability"}, 35)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"war", "military", "prepared"}, 30)
	if p.canExpandSoon() {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"expan", "frontier", "colon"}, 45)
	}
	if p.threatLevel > 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"war", "military", "prepared"}, 60)
	}
	if p.foodAmount() <= 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"recover", "food", "stability"}, 40)
	}
	return score
}

func (p *ruleBotPlanner) scoreInstitutionPolicy(policy staticdata.PolicyDefinition) int {
	score := 10
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"academy", "research", "science", "knowledge"}, 50)
	score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"industry", "forge", "craft"}, 30)
	if p.threatLevel > 0 {
		score += matchKeywordScore(policy.ID, policy.Name, policy.Description, []string{"military", "war", "drill"}, 40)
	}
	return score
}

func (p *ruleBotPlanner) availableBuildingsForNode(node *pb.NodeView) []staticdata.BuildingDefinition {
	available := make([]staticdata.BuildingDefinition, 0)
	for _, building := range staticdata.Default().Buildings() {
		if building.ID == "" || !p.req.State.IsBuildingUnlocked(p.playerID, building.ID) {
			continue
		}
		if building.ID == "city_core" {
			continue
		}
		if node.GetIsResourcePoint() && strings.TrimSpace(building.RequiredResourceType) != "" && !strings.EqualFold(building.RequiredResourceType, node.GetResourceType()) {
			continue
		}
		if !node.GetIsResourcePoint() && strings.EqualFold(building.PlacementKind, "resource_node") {
			continue
		}
		available = append(available, building)
	}
	return available
}

func (p *ruleBotPlanner) scoreBuild(node *pb.NodeView, building staticdata.BuildingDefinition) int {
	score := 0
	if node.GetIsResourcePoint() {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{node.GetResourceType()}, 120)
		if strings.EqualFold(node.GetResourceType(), "food") {
			score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "food"}, 90)
			if p.foodAmount() <= 1 {
				score += 40
			}
		}
	}
	if p.threatLevel > 0 {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"barracks", "stable", "tower", "wall"}, 90)
	} else {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "mine", "lumber", "workshop"}, 50)
	}
	if len(p.player.Cities) < 2 {
		score += matchKeywordScore(building.ID, building.Name, building.Description, []string{"farm", "industry", "workshop"}, 30)
	}
	return score
}

func (p *ruleBotPlanner) scoreRecipe(node *pb.NodeView, recipe staticdata.RecipeDefinition) int {
	score := 0
	if node.GetBuildingTypeId() == "city_core" {
		if len(p.player.Cities) < 2 || p.countOwnedUnitType("settler") == 0 {
			score += matchKeywordScore(recipe.ID, recipe.Name, recipe.Description, []string{"settler", "expand", "city"}, 120)
		}
		if p.threatLevel > 0 {
			score += matchKeywordScore(recipe.ID, recipe.Name, recipe.Description, []string{"infantry", "archer", "cavalry"}, 80)
		}
	}
	score += recipe.Outputs.Resources["food"] * 20
	score += recipe.Outputs.Resources["wood"] * 10
	score += recipe.Outputs.Resources["ore"] * 10
	for _, unitID := range recipe.Outputs.Units {
		score += matchKeywordScore(unitID, "", "", []string{"settler"}, 90)
		score += matchKeywordScore(unitID, "", "", []string{"infantry", "archer", "cavalry"}, 70)
	}
	if p.threatLevel > 0 {
		for _, unitID := range recipe.Outputs.Units {
			score += matchKeywordScore(unitID, "", "", []string{"infantry", "archer", "cavalry"}, 50)
		}
	}
	return score
}

func (p *ruleBotPlanner) scoreExpansion(unitID string, nodeID string) int {
	nodeEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || nodeEntry == nil {
		return 0
	}
	node := ecs.NodeC.Get(nodeEntry)
	score := 20
	if node.IsResource {
		score += 40
		if strings.EqualFold(node.ResourceType, "food") {
			score += 20
		}
	}
	if p.closestEnemyDistance(nodeID) <= 2 {
		score -= 40
	}
	if distance := p.closestCityDistance(nodeID); distance >= 2 && distance <= 6 {
		score += 20
	}
	return score
}

func (p *ruleBotPlanner) computeThreatLevel() int {
	if p.req.State == nil {
		return 0
	}
	threat := 0
	for _, enemy := range p.visibleEnemyUnits() {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		for _, city := range p.player.Cities {
			if city == nil {
				continue
			}
			cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
			if !ok || cityEntry == nil {
				continue
			}
			cityPos := ecs.PositionC.Get(cityEntry)
			if (domain.Position{X: int(enemy.GetPos().GetX()), Y: int(enemy.GetPos().GetY())}).DistanceTo(domain.Position{X: cityPos.X, Y: cityPos.Y}) <= 3 {
				threat++
				break
			}
		}
	}
	return threat
}

func (p *ruleBotPlanner) visibleEnemyUnits() []*pb.UnitView {
	if p.observation == nil {
		return nil
	}
	out := make([]*pb.UnitView, 0)
	for _, unit := range p.observation.Units {
		if unit == nil || unit.GetFaction() == p.playerID {
			continue
		}
		out = append(out, unit)
	}
	return out
}

func (p *ruleBotPlanner) memoryEnemyUnits() []*gamequery.RememberedUnitView {
	if p.observation == nil {
		return nil
	}
	out := make([]*gamequery.RememberedUnitView, 0)
	for _, unit := range p.observation.MemoryUnits {
		if unit == nil || unit.View == nil || unit.View.GetFaction() == p.playerID {
			continue
		}
		out = append(out, unit)
	}
	return out
}

func (p *ruleBotPlanner) enemyStructureNodes() []*pb.NodeView {
	if p.observation == nil {
		return nil
	}
	out := make([]*pb.NodeView, 0)
	for _, node := range p.observation.Nodes {
		if node == nil || node.GetBuildingTypeId() == "" || node.GetControllerPlayerId() == "" || node.GetControllerPlayerId() == p.playerID {
			continue
		}
		out = append(out, node)
	}
	return out
}

func (p *ruleBotPlanner) ownedUnitEntries() []*donburi.Entry {
	if p.req.State == nil || p.req.State.World == nil {
		return nil
	}
	out := make([]*donburi.Entry, 0)
	ecs.AllUnits(p.req.State.World).Each(p.req.State.World, func(entry *donburi.Entry) {
		if entry == nil {
			return
		}
		if ecs.UnitStatsC.Get(entry).Faction == p.playerID {
			out = append(out, entry)
		}
	})
	sort.Slice(out, func(i, j int) bool {
		return ecs.UnitStatsC.Get(out[i]).ID < ecs.UnitStatsC.Get(out[j]).ID
	})
	return out
}

func (p *ruleBotPlanner) countOwnedUnitType(unitType string) int {
	count := 0
	for _, entry := range p.ownedUnitEntries() {
		if strings.EqualFold(string(ecs.UnitStatsC.Get(entry).Type), unitType) {
			count++
		}
	}
	return count
}

func (p *ruleBotPlanner) canExpandSoon() bool {
	return p.countOwnedUnitType("settler") > 0 || len(p.player.Cities) < 2
}

func (p *ruleBotPlanner) foodAmount() int {
	if p.player == nil {
		return 0
	}
	return p.player.Resources.Get(domain.ResourceFood)
}

func (p *ruleBotPlanner) closestCityID(nodeID string) string {
	if p.player == nil || len(p.player.Cities) == 0 {
		return ""
	}
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		if primary := p.req.State.PrimaryCityState(p.playerID); primary != nil {
			return primary.CityID
		}
		return ""
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	bestCityID := ""
	bestDistance := intMax
	for _, city := range p.player.Cities {
		if city == nil {
			continue
		}
		cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
		if !ok || cityEntry == nil {
			continue
		}
		cityPos := ecs.PositionC.Get(cityEntry)
		distance := (domain.Position{X: targetPos.X, Y: targetPos.Y}).DistanceTo(domain.Position{X: cityPos.X, Y: cityPos.Y})
		if distance < bestDistance {
			bestDistance = distance
			bestCityID = city.CityID
		}
	}
	if bestCityID == "" {
		if primary := p.req.State.PrimaryCityState(p.playerID); primary != nil {
			return primary.CityID
		}
	}
	return bestCityID
}

func (p *ruleBotPlanner) closestCityDistance(nodeID string) int {
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		return intMax
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	best := intMax
	for _, city := range p.player.Cities {
		if city == nil {
			continue
		}
		cityEntry, ok := p.req.State.GetNode(city.CoreNodeID)
		if !ok || cityEntry == nil {
			continue
		}
		cityPos := ecs.PositionC.Get(cityEntry)
		distance := (domain.Position{X: targetPos.X, Y: targetPos.Y}).DistanceTo(domain.Position{X: cityPos.X, Y: cityPos.Y})
		if distance < best {
			best = distance
		}
	}
	return best
}

func (p *ruleBotPlanner) closestEnemyDistance(nodeID string) int {
	targetEntry, ok := p.req.State.GetNode(nodeID)
	if !ok || targetEntry == nil {
		return intMax
	}
	targetPos := ecs.PositionC.Get(targetEntry)
	best := intMax
	for _, enemy := range p.visibleEnemyUnits() {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		distance := (domain.Position{X: targetPos.X, Y: targetPos.Y}).DistanceTo(domain.Position{X: int(enemy.GetPos().GetX()), Y: int(enemy.GetPos().GetY())})
		if distance < best {
			best = distance
		}
	}
	return best
}

func (p *ruleBotPlanner) closestEnemyTargetNode(unitPos domain.Position, visibleEnemies []*pb.UnitView, memoryEnemies []*gamequery.RememberedUnitView, enemyStructures []*pb.NodeView) string {
	bestNodeID := ""
	bestDistance := intMax
	for _, enemy := range visibleEnemies {
		if enemy == nil || enemy.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(domain.Position{X: int(enemy.GetPos().GetX()), Y: int(enemy.GetPos().GetY())})
		if distance < bestDistance {
			bestDistance = distance
			if nodeID := p.nodeIDAtPosition(int(enemy.GetPos().GetX()), int(enemy.GetPos().GetY())); nodeID != "" {
				bestNodeID = nodeID
			}
		}
	}
	for _, enemy := range memoryEnemies {
		if enemy == nil || enemy.View == nil || enemy.View.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(domain.Position{X: int(enemy.View.GetPos().GetX()), Y: int(enemy.View.GetPos().GetY())})
		if distance < bestDistance {
			bestDistance = distance
			if nodeID := p.nodeIDAtPosition(int(enemy.View.GetPos().GetX()), int(enemy.View.GetPos().GetY())); nodeID != "" {
				bestNodeID = nodeID
			}
		}
	}
	for _, node := range enemyStructures {
		if node == nil || node.GetPos() == nil {
			continue
		}
		distance := unitPos.DistanceTo(domain.Position{X: int(node.GetPos().GetX()), Y: int(node.GetPos().GetY())})
		if distance < bestDistance {
			bestDistance = distance
			bestNodeID = node.GetId()
		}
	}
	return bestNodeID
}

func (p *ruleBotPlanner) nodeIDAtPosition(x int, y int) string {
	if p.req.State == nil || p.req.State.World == nil {
		return ""
	}
	entry, ok := domain.GetNodeAt(p.req.State.World, domain.Position{X: x, Y: y})
	if !ok || entry == nil {
		return ""
	}
	return ecs.NodeC.Get(entry).ID
}

func prerequisitesMet(state *domain.GameState, playerID string, prerequisites []staticdata.Prerequisite) bool {
	for _, prereq := range prerequisites {
		switch prereq.Type {
		case "technology_unlocked":
			if !state.HasTechnologyUnlocked(playerID, prereq.TargetID) {
				return false
			}
		case "policy_active":
			if !state.IsPolicyActive(playerID, prereq.TargetID) {
				return false
			}
		}
	}
	return true
}

func isCivilianUnit(unitType string) bool {
	normalized := strings.ToLower(strings.TrimSpace(unitType))
	switch normalized {
	case "settler", "pioneer", "expander", "engineer":
		return true
	default:
		return false
	}
}

func matchKeywordScore(id string, name string, description string, keywords []string, value int) int {
	target := strings.ToLower(strings.Join([]string{id, name, description}, " "))
	for _, keyword := range keywords {
		if strings.Contains(target, strings.ToLower(keyword)) {
			return value
		}
	}
	return 0
}

func pickBestScoredIntent(rng *rand.Rand, candidates []scoredIntent) (planning.Intent, bool) {
	if len(candidates) == 0 {
		return nil, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)].intent, true
}

func pickBestBuildCandidate(rng *rand.Rand, candidates []buildCandidate) (buildCandidate, bool) {
	if len(candidates) == 0 {
		return buildCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}

func pickBestRecipeCandidate(rng *rand.Rand, candidates []recipeCandidate) (recipeCandidate, bool) {
	if len(candidates) == 0 {
		return recipeCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}

func pickBestExpansionCandidate(rng *rand.Rand, candidates []expansionCandidate) (expansionCandidate, bool) {
	if len(candidates) == 0 {
		return expansionCandidate{}, false
	}
	sort.Slice(candidates, func(i, j int) bool {
		if candidates[i].score == candidates[j].score {
			return candidates[i].key < candidates[j].key
		}
		return candidates[i].score > candidates[j].score
	})
	best := candidates[0].score
	end := 1
	for end < len(candidates) && candidates[end].score == best {
		end++
	}
	return candidates[rng.Intn(end)], true
}

func min(a int, b int) int {
	if a < b {
		return a
	}
	return b
}
