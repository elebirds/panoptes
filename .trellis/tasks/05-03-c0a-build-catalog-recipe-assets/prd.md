# C0a Build Catalog and Recipe UI Toolkit Assets

## Goal

Continue C0a by replacing the build catalog and recipe synthesis generated
UIDocument fallbacks with authored UI Toolkit assets and prefab-backed
composition registrations.

## Requirements

- Add authored UXML/USS assets for Build Catalog.
- Add authored UXML/USS assets for Recipe Synthesis.
- Add prefab resources for both binders and wire their serialized UXML/USS
  references.
- Move `BuildCatalogUiToolkitBinder` and `RecipeSynthesisUiToolkitBinder` from
  `RegisterComponentOnNewGameObject` to `RegisterComponentInNewPrefab`.
- Keep command paths unchanged: Build Catalog enters `PlanningToolService`
  build mode; Recipe Synthesis submits through `PlanningIntentService`.
- Preserve Presentation boundaries: no Protocol, no NetworkManager, no legacy
  cache singleton dependency in the new slice.

## Acceptance Criteria

- [x] `BuildCatalog.uxml` / `BuildCatalog.uss` exist and include stable binder
      element names.
- [x] `RecipeSynthesis.uxml` / `RecipeSynthesis.uss` exist and include stable
      `ManagementPanelUiToolkitRenderer` element names.
- [x] `BuildCatalog.prefab` and `RecipeSynthesis.prefab` exist under
      `Resources/Prefabs/UI`.
- [x] Composition loads both binders from prefabs.
- [x] The UI Toolkit generated GameObject exception list shrinks by two.
- [x] Tests verify assets, prefab-backed composition, and no fallback
      registration for these two binders.

## Out of Scope

- TechTree authored asset and command wiring.
- PolicyFocus commands.
- MinisterReport interaction.
- Reworking BuildCatalog/Recipe ViewModel state shape.

## Technical Notes

- Follow `docs/2026-05-03-c0a-ui-inventory-and-integration-plan-zh.md`.
- Use explicit UI Toolkit names and binder rendering, not automatic data binding.
