# brainstorm: backend final goal

## Goal

Define and harden the final-version backend target for Panoptes before writing the long-range implementation plan. The goal is not to restate the MVP roadmap, but to clarify what the completed backend must make possible: authoritative state, national machinery, logistics, ministers, information asymmetry, and PVE built on the same rules.

## What I Already Know

* The user wants to target the final version, with backend development first.
* The previous implementation outline suggested a sequence from backend rule foundation through logistics, institutions, war, ministers, information distortion, and PVE.
* The user interrupted implementation planning and asked to follow `/grill-me` first, meaning the final goal should be challenged and refined before execution.
* No local `/grill-me` command definition was found in the repository, so this task will use a grill-style requirement discovery loop: pressure-test assumptions, ask one decisive question at a time, and record answers immediately.

## Assumptions (Temporary)

* "Final version" means the long-term Panoptes identity described in `docs/gdd/2026-04-15-panoptes-gdd-v1.md`, not merely the current MVP baseline.
* Backend-first means core server rules, data, protocol, headless validation, and authoritative projections should be designed before client UI implementation.
* The final backend target should remain staged, so each milestone is playable or at least headless-verifiable.

## Open Questions

* M1 code execution requires child task selection.

## Requirements (Evolving)

* The final backend target must be specific enough to drive a multi-milestone implementation plan.
* The target must distinguish final-version requirements from MVP and mid-term stepping stones.
* The target must define what the backend owns before client work begins.
* The target must identify explicit trade-offs and "not the game" boundaries.
* Final target priority is staged, not parallel: first implement the national machine experience, then implement the monarch dilemma on top of it.
* The national machine layer must be strong enough that ministers and information distortion have real systems to observe, misread, prioritize, and execute through.
* Minister and information-asymmetry implementation should wait until the national machine layer is substantially complete, including economy, institutions, and network warfare.
* Final logistics target should be semi-explicit: a capacity- and cost-constrained logistics network with resource flow allocation, not merely connected-component aggregation.
* Logistics priority is a meaningful gameplay lever. Policies and national stances should influence allocation priority during shortage.
* Final priority control should be policy direction plus minister refinement, but minister-specific behavior is deferred until after the national machine is complete.
* The backend should first implement policy/national-stance priority presets as the substrate for future minister-generated priority plans.
* Current victory implementation should remain conquest-focused.
* Final victory direction should support strategic collapse, with room for later Civilization-like victory categories such as economic victory, but not as the immediate backend scope.
* Strategic collapse is a long-range planning concept only for now; it should not be implemented in the immediate backend milestones.
* Final information asymmetry should combine staged core progression with optional modes. The backend must support both dynamic transparency over game progression and configurable information-asymmetry presets.
* Grill-mode output should include a final target document plus staged backend roadmap, but should not start M0 yet.
* User confirmed entering M0 and M1 on 2026-04-30.

## Acceptance Criteria (Evolving)

* [x] Final target statement is written in terms of player experience and backend responsibilities.
* [x] Final scope includes required systems and excludes tempting but non-core systems.
* [x] Major risks and contradictions are called out before implementation planning.
* [x] User confirms the final target direction before M0 execution begins.
* [x] M0 GDD alignment matrix is archived.
* [x] M0 backend module responsibility matrix is archived.
* [x] M1 execution plan is archived.
* [x] M1 backend rule foundation child tasks are completed and archived.

## Definition of Done

* Requirements are clarified and recorded in this PRD.
* Implementation plan can be derived without guessing product priorities.
* Relevant docs and spec references are captured.
* Follow-up execution can proceed through Trellis Phase 1.3 and Phase 2.

## Out of Scope (Explicit)

* Writing backend feature code during grill-mode.
* Committing the final roadmap before product priorities are resolved.
* Client implementation details, except where backend protocol/projection responsibilities must be reserved.

## Technical Notes

* Primary design sources: `docs/gdd/2026-04-15-panoptes-gdd-v1.md`, `docs/gdd/2026-04-15-panoptes-gdd-v1-detailed.md`, `docs/SERVER_RUNTIME_ARCHITECTURE.md`.
* Current backend tests were previously observed passing with `cd server && go test ./...`.
* Current backend already has MVP-style foundations for planning/resolving, static data, city/building lifecycle, economy, research, policies, combat, and projections.
* Known final-version gaps include logistics, local storage, minister default execution, information distortion, fog, richer war goals, and PVE.
* Existing algorithm support includes `server/internal/algo/graph/flow.go` with a simple integer max-flow implementation and `server/internal/algo/pathfinding/astar.go` plus combat route planning for weighted pathfinding.
* Existing road representation is node-level `HasRoad`; map data has road features, but default resolving currently rejects road actions.
* Archived output documents:
  * `docs/2026-04-30-backend-final-target.md`
  * `docs/2026-04-30-backend-final-roadmap.md`
  * `docs/2026-04-30-backend-final-m0-baseline.md`
  * `docs/2026-04-30-backend-m1-rule-foundation-plan.md`
  * `docs/2026-04-30-backend-m1-regression-gate.md`

## Grill Decisions

### 2026-04-30: Final Experience Priority

**Question**: Which final experience wins when systems conflict: monarch dilemma, national machine, or competitive clarity?

**Answer**: Both monarch dilemma and national machine are required, but the implementation order is national machine first, monarch dilemma second. The national machine is the substrate; minister execution and distorted information should be built on top of it.

**Implication**: Backend planning should not start with LLM ministers, fog, or distorted reports. It should first make cities, roads, warehouses, logistics, institutions, industry, and network warfare mechanically real. Only then should ministers become default executors and unreliable narrators.

### 2026-04-30: Minister Start Gate

**Question**: How complete must the national machine be before minister and information-asymmetry work begins?

**Answer**: Economy, institutions, and war networks should all be formed first. Ministers are central, but precisely because they operate the national machine, they should wait until that machine exists in meaningful depth.

**Implication**: The implementation plan should not include an early parallel minister MVP as a core milestone. A thin technical interface can be reserved, but real minister execution should begin only after logistics, local storage, industrial chains, institutional loadouts, specialized units, infrastructure destruction, and supply/network consequences are in place.

### 2026-04-30: Logistics Algorithm Analysis

**Question**: Before choosing logistics depth, how feasible are the mathematical models behind each option?

**Analysis**:

* Strategic abstract logistics can be implemented as connected-component detection plus per-network capacity allocation. This is the safest first version because it is deterministic, cheap, and easy to explain.
* Semi-explicit logistics can be implemented as a min-cost max-flow or priority flow allocation problem over a road/city graph. This is a strong final target because it supports shortages, bottlenecks, and network warfare without simulating individual carts.
* High-simulation logistics would require persistent shipments, edge occupancy, congestion, delays, losses, and rerouting. This is possible but risks dominating the game and making minister reports too opaque.

**Implication**: The likely backend direction should be progressive: connectivity first, then capacity-constrained allocation, then min-cost/priority flow if needed. Avoid committing to persistent per-shipment simulation as the default final model unless the game intentionally becomes a logistics simulator.

### 2026-04-30: Final Logistics Scope

**Question**: Which logistics depth should be the final target?

**Answer**: Semi-explicit logistics is the right target. The backend should model a capacity- and cost-constrained logistics graph and allocate resource flows by demand priority. Policy/national strategy should be able to affect those priorities.

**Implication**: Final backend planning should include a real logistics allocation engine, likely evolving toward priority-aware min-cost max-flow. Resource allocation is not a hidden implementation detail; it is a political and strategic surface. However, the default target still avoids persistent per-shipment simulation unless a later feature specifically requires it.

### 2026-04-30: Logistics Priority Control

**Question**: How should players influence logistics priorities?

**Answer**: Final target is policy direction plus minister refinement. For now, only the policy/national-stance substrate should be planned; minister-specific priority generation is intentionally deferred.

**Implication**: The implementation plan should include policy-driven priority profiles for logistics allocation, such as military, expansion, recovery, or industrialization. The data and protocol should leave room for future minister-proposed priority plans, but near-term backend work should not require minister AI to make logistics playable.

### 2026-04-30: War and Victory Direction

**Question**: What final war objective should logistics and national-machine systems support?

**Answer**: For now, keep only conquest victory. The final target is strategic collapse rather than merely capital destruction or city-count scoring. Civilization-like multiple victory types are a useful reference, and economic victory may exist eventually, but immediate backend planning should not expand victory types beyond conquest.

**Implication**: Backend conquest work should be designed so it can later feed collapse metrics: supply failure, loss of cities, logistics paralysis, institutional failure, military inability, or economic breakdown. Do not hard-code "capital destroyed" as the only conceptual win condition even if it remains the current rule.

### 2026-04-30: Strategic Collapse Scope

**Question**: What should strategic collapse consist of?

**Answer**: Economic/popular collapse and a broader combined stability model are both acceptable as long-range targets, but they are planning-only for now and should not be implemented in near-term backend work.

**Implication**: The final target can reserve concepts like economic integrity, supply integrity, governance integrity, and military integrity, but M0/M1 implementation should keep conquest as the only active victory rule. Near-term code should avoid painting the backend into a corner where collapse metrics cannot be added later.

### 2026-04-30: Information Asymmetry Mode

**Question**: Should information asymmetry be mandatory core gameplay, staged over progression, or optional mode-based?

**Answer**: Use both staged core progression and optional modes. Players can start with clearer information and lose transparency as the state grows more complex, while game modes can tune or disable parts of the Panoptes pressure layer.

**Implication**: Backend design should explicitly separate `truth`, `observed`, and `reported` state. Visibility and distortion must be parameterized by game mode, difficulty, progression, policies, institutions, and future minister behavior. The standard backend should not assume all players always receive full authoritative truth, even if current MVP still does.

### 2026-04-30: Grill Output Scope

**Question**: After grill-mode, should the output be only a final target document, a target document plus roadmap, or target plus roadmap plus M0 execution?

**Answer**: Produce the final target document plus staged backend roadmap. Do not start M0 yet.

**Implication**: This task now archives `docs/2026-04-30-backend-final-target.md` and `docs/2026-04-30-backend-final-roadmap.md`. M0 should begin only after the user reviews and confirms this target/roadmap.

### 2026-04-30: M0/M1 Entry

**Question**: Should the work proceed into M0 and M1?

**Answer**: Yes. Start M0 and M1.

**Implication**: M0 baseline artifacts were added under `docs/2026-04-30-backend-final-m0-baseline.md`. M1 was entered as a planning/execution-splitting step with `docs/2026-04-30-backend-m1-rule-foundation-plan.md`; code implementation should happen through the M1 child tasks.
