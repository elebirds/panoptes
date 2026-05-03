# M4 Priority Logistics And Industrial Chains

## Goal

Upgrade M3 logistics from "reachable inventory can be used" to
policy-influenced allocation across competing recipe demands.

M4 should make shortages produce different results under different national
policies while staying smaller than a full min-cost flow implementation.

## Scope

- Add a logistics demand ordering model for recipe progress.
- Add policy-owned logistics priority profiles in static data.
- Sort competing recipe demands by priority before consuming shared budgets.
- Track road capacity as a shared per donor/target city budget during the
  recipe progress stage.
- Emit existing resource flow events for priority allocations.
- Cover industrial-chain shortage propagation with tests.

## Non-Goals

- No minister-specific priorities.
- No full graph solver or path-cost optimization.
- No new resource taxonomy beyond existing resource keys.
- No frontend protocol changes.

## Acceptance

- War preparedness prioritizes military recipes under shared resource/capacity
  shortage.
- Expansion prioritizes expansion recipes under the same shortage.
- The same map/storage state produces different outcomes under different
  policies.
- Upstream production shortage blocks downstream recipe progress until the
  input exists in reachable storage.
- `cd server && go test -count=1 ./...` and `make lint` pass.

## Technical Notes

- M3 already introduced city storage, road base capacity, and `resource_flowed`.
- M4 extends `recipeProgressBudget` instead of adding a separate engine stage so
  state mutation still flows through events.
- `PolicyDefinition.logistics_priority` maps recipe IDs or recipe tags to
  integer priority values. Higher wins; ties remain deterministic by node ID.
