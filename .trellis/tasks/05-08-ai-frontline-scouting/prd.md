# AI frontline scouting and pressure

## Goal

Redesign the rule-based bot's enemy-search behavior so units no longer collapse onto the same deterministic next tile, and instead behave like a real front:
explore unknown space with spread, press known enemy territory/buildings, and converge into attacks or encirclement once enemies are found.

## What I already know

* The current AI lives in `server/internal/game/ai/` and selects a single move target per unit by scoring visible, memory, and pressure nodes.
* The current fallback is too deterministic: when multiple units start from the same position, they often pick the same next node.
* The game already has authoritative pathing and movement preview support, so target selection is the right layer to change.
* Fog-of-war is already authoritative on the server side through `game/query`, including remembered nodes and remembered enemy units.
* The map is hex-based, with `domain.Position.Neighbors()` already available.

## Research references

* `research/design-notes.md`
* [Influence maps for tactical decisions](https://www.sharcnet.ca/my/publications/show/2188)
* [Kiting in RTS games using influence maps](https://www.researchgate.net/publication/289646419_Kiting_in_RTS_games_using_influence_maps)
* [Spatial reasoning for strategic decision making](https://www.gameaipro.com/GameAIPro2/GameAIPro2_Chapter31_Spatial_Reasoning_for_Strategic_Decision_Making.pdf)
* [Fog of war systems in strategy games](https://www.designthegame.com/learning/tutorial/the-art-science-fog-war-systems-video-games)
* [DefogGAN: hidden-information prediction in StarCraft fog of war](https://arxiv.org/abs/2003.01927)

## Assumptions

* This task only changes backend AI decision-making, not protocol, client UI, or combat resolution rules.
* "No enemy found" means no currently visible enemy unit, while remembered enemy units and known enemy structures can still create pressure.
* The MVP can stay heuristic-driven; no ML layer is required.

## Requirements

* Add a frontier-aware exploration pass so units prefer uncovered edges instead of the single nearest unexplored node.
* Add pressure scoring around known enemy buildings and remembered enemy presence so armies advance toward a front, not just toward one point.
* Add target diversity so multiple same-role units with the same origin do not repeatedly choose the same next node.
* Preserve current authoritative movement, attack, and planning validation flows.
* Keep behavior deterministic enough for tests, but with controlled tie-breaking and spread.

## Acceptance Criteria

* [x] Two or more friendly units starting from the same position no longer always pick the same move target when multiple equivalent front options exist.
* [x] With no visible enemies, units prefer frontier/exploration moves over idle holds when reachable frontier exists.
* [x] With known enemy buildings or remembered enemy units, units pressure the enemy side instead of drifting to the same exploratory node.
* [x] When a visible enemy is in attack range, attack still wins over exploration or pressure.
* [x] Existing AI regression tests still pass, and new tests cover spread/frontier/pressure behavior.

## Definition of Done

* Tests added/updated.
* Lint / Go test / regression checks green.
* Notes updated if the new heuristics reveal a reusable pattern.

## Out of Scope

* No protocol changes.
* No client-side logic.
* No combat resolver changes.
* No new third-party dependencies.

## Technical Notes

* The likely touchpoints are `server/internal/game/ai/*.go` and `server/internal/game/ai/*_test.go`.
* The current selection path is `RuleBotProvider -> ruleBotPlanner -> chooseCombatIntents`.
* The best place to introduce spreading is before final move intent emission, not in pathfinding or combat resolution.
* `go test ./internal/game/ai` passes.
* `go test ./internal/game/...` is currently blocked by pre-existing strong-mode test drift in `internal/game/turn` and `internal/game/planning`.
