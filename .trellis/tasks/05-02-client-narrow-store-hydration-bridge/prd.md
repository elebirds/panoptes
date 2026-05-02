# client narrow store hydration bridge

## Goal

Continue the client reactive architecture migration by narrowing legacy cache
bridge responsibilities after direct game and static catalog message hydrators
were introduced. The bridge should stop looking like the final read-model
pipeline and instead have an explicit, temporary role around bootstrapping or
legacy-only gaps.

## What I Already Know

- `StoreMessageHydrator` now hydrates game runtime stores directly from
  `MsgGameInit`, `MsgGameSnapshot`, `MsgPlanningDraft`, `MsgPlanningPreview`,
  `MsgTurnStarted`, `MsgTurnSummary`, and phase messages.
- `StaticCatalogMessageHydrator` now hydrates `StaticCatalogStore` directly
  from `MsgStaticCatalogSnapshot`.
- `ClientCompositionInstaller` no longer directly references legacy cache
  singletons in composition code.
- `StoreHydrationCacheBridge` still exists and may still mirror legacy caches.
- Static catalog chunk sync is still assembled by legacy `StaticCatalogCache`,
  so removing all catalog bridge behavior in one step could regress catalog
  hydration for non-snapshot sync paths.

## Assumptions

- One-shot seeding from legacy caches remains useful when a message arrived
  before the relevant VContainer game scope existed.
- Runtime message families already covered by direct hydrators should not also
  rely on cache event subscriptions as their primary ongoing path.
- Any retained bridge behavior should be named and tested as temporary legacy
  support, not hidden final architecture.

## Requirements

- Preserve startup/runtime behavior for migrated stores.
- Reduce or clarify `StoreHydrationCacheBridge` so direct hydrators remain the
  primary path for game runtime messages.
- Keep static catalog chunk support intact unless a direct section hydrator is
  implemented in this task.
- Keep Presentation free of generated protocol references and direct
  `NetworkManager.Instance`.
- Do not edit generated protocol files.

## Acceptance Criteria

- [x] Game runtime store hydration can be seeded from existing caches without
      ongoing cache-event dependency for message types already handled directly.
- [x] Static catalog cache bridging remains only where needed for legacy
      section/chunk sync, or is replaced by direct hydration with tests.
- [x] EditMode tests cover the narrowed bridge behavior.
- [x] Architecture implementation plan documents the new boundary.
- [x] Build/test/boundary checks pass.

## Definition of Done

- Tests added or updated for changed store hydration behavior.
- `dotnet build` and relevant Unity/EditMode checks pass or limitations are
  recorded.
- Static boundary checks pass.
- Trellis task validates.
- Changes are committed.

## Out of Scope

- Rewriting static catalog chunk assembly unless it is the smallest safe path.
- Migrating UI panels.
- Removing legacy caches used by unmigrated modules.

## Technical Notes

- Relevant specs:
  - `.trellis/spec/frontend/state-management.md`
  - `.trellis/spec/frontend/quality-guidelines.md`
  - `.trellis/spec/frontend/directory-structure.md`
  - `.trellis/spec/frontend/component-guidelines.md`
  - `.trellis/spec/guides/cross-layer-thinking-guide.md`
  - `.trellis/spec/guides/code-reuse-thinking-guide.md`
- Architecture plan:
  - `docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`
