# M3 Local Storage And Capacity Logistics

## Goal

Move the backend economy from a purely player-global resource pool toward
city-local storage and a capacity-limited national road network.

M3 is the first logistics slice. It should make resource location matter without
implementing M4 policy priorities or full min-cost flow allocation.

## Scope

- Add authoritative city storage state.
- Route resource production and recipe completion outputs into the service city
  or primary city storage.
- Make recipe inputs consume from the service city's reachable inventory.
- Treat road-connected cities as reachable and disconnected cities as isolated.
- Add a static-data backed base road capacity rule.
- Emit `resource_flowed` audit events when remote city storage is used.
- Keep `Player.Resources` as a compatibility aggregate/view for existing
  callers.

## Non-Goals

- No policy-aware logistics priority tiers.
- No minister demand planning.
- No explicit per-edge shipment objects.
- No frontend protocol expansion unless strictly required for M3 acceptance.
- No client-side validation or gameplay calculation.

## Acceptance

- If city A has resources but city B is disconnected, a recipe in B cannot
  consume city A storage.
- If city A is connected to city B but road capacity is lower than the recipe's
  full input need, the recipe advances inefficiently instead of at full speed.
- A recipe that imports resources produces at least one `resource_flowed` event.
- Recipe output lands in city storage and remains visible through the existing
  player resource compatibility view.
- `cd server && go test ./...` passes.

## Design Notes

- Authoritative writes continue to flow through event `Apply()`.
- Domain helpers own city-storage mutation and aggregate resource projection.
- Engine systems simulate budgets first and then emit events, matching the
  existing resolving pattern.
- The first road capacity model is intentionally simple: each connected donor
  city can contribute up to `rules.road_base_capacity` resource units per recipe
  demand. M4 will replace allocation order with policy-aware demand priority.
