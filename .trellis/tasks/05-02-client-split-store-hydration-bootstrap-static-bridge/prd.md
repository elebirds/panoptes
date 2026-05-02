# client split store hydration bootstrap and static bridge

## Goal

Finish the next cleanup step after narrowing `StoreHydrationCacheBridge`: split
its mixed responsibility into explicit classes so game/planning bootstrap
seeding and static catalog legacy event bridging are no longer hidden behind one
generic cache bridge.

## What I Already Know

- Commit `98e5b26` made `StoreHydrationCacheBridge` stop subscribing to
  `GameStateCache` and `PlanningDraftCache` events.
- The same class still performs one-shot game/planning/turn seeding and static
  catalog `CatalogChanged` bridging.
- Static catalog chunk/bundle sync still depends on `StaticCatalogCache`, so a
  legacy static bridge is still required until a direct section hydrator exists.

## Requirements

- Replace the mixed `StoreHydrationCacheBridge` name with narrower classes.
- Keep game/planning/turn cache hydration as one-shot bootstrap seeding only.
- Keep static catalog legacy event bridging isolated and visibly temporary.
- Preserve existing Store hydration behavior.
- Keep generated protocol files untouched.
- Keep Presentation composition free of legacy cache names and singleton calls.

## Acceptance Criteria

- [x] No runtime code references `StoreHydrationCacheBridge`.
- [x] Game/planning/turn bootstrap seeding has a dedicated class and test.
- [x] Static catalog legacy bridging has a dedicated class and test.
- [x] Architecture plan documents the split.
- [x] Build/test/boundary checks pass.

## Definition of Done

- Updated tests pass in Unity EditMode or via available project test commands.
- Static boundary checks pass.
- Trellis task validates.
- Changes are committed.

## Out of Scope

- Direct static catalog section/chunk protocol hydration.
- Removing legacy caches for unmigrated UI.
