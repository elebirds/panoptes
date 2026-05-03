# D0 Content Expansion

## Goal

Expand the authored static content so the M2-M9 backend systems have a wider
long-game catalog to exercise: more buildings, recipes, units, technologies,
policies, and generated catalog artifacts without adding new rules code.

## What I already know

- D0 was recommended after M0-M9 as the content expansion lane.
- Current content is intentionally thin: 9 buildings, 10 recipes, 9 technologies,
  5 units, 6 policies, 2 authored maps, and a small minister pool.
- Static data must be edited in `data/registry`, `data/content`, and `data/ui`;
  generated files come from `make data-gen`.
- Authoring validation builds schemas from authored data before validating, so
  adding units/buildings and recipe references can be done in one content pass.
- Clients consume generated static catalog artifacts in
  `client/Assets/Resources/Data/`; service runtime consumes
  `data/generated/server/`.

## Assumptions

- D0 should be a conservative backend-first content pass, not a balance pass.
- No new resource keys or point keys are required in this pass.
- No protocol changes are required.
- Maps can remain as-is for D0 unless new content needs a map-specific fixture.

## Requirements

- Add content that exercises logistics, industry, military, defense, and PVE
  long-game paths.
- Keep all added gameplay values in authored static data, not Go constants.
- Add matching UI catalog entries and technology-tree layout entries.
- Regenerate server and client static catalog artifacts with `make data-gen`.
- Add or update backend tests that prove the expanded real content is present
  and semantically usable.
- Record D0 gate results in docs.

## Acceptance Criteria

- [x] Authored content includes at least 3 new buildings.
- [x] Authored content includes at least 4 new recipes.
- [x] Authored content includes at least 2 new technologies.
- [x] Authored content includes at least 1 new unit.
- [x] Authored content includes at least 1 new policy or institutional option.
- [x] UI catalogs and technology tree cover the new content.
- [x] Generated server and client catalog artifacts are updated.
- [x] D0 gate doc exists.
- [x] `make data-validate`, `make data-gen`, `cd server && go test -count=1 ./internal/debug ./internal/datagen ./internal/staticdata`, `cd server && go test -count=1 ./...`, `make lint`, and `git diff --check` pass.

## Definition of Done

- Tests added/updated.
- Lint and full backend tests pass.
- Static generated artifacts committed.
- D0 gate note committed.

## Out of Scope

- No new rules engine systems.
- No new resource or point registry keys.
- No protocol changes.
- No frontend scene/UI implementation.
- No balance finalization.

## Technical Notes

- Read `docs/STATIC_DATA_EDITING_GUIDE.md`.
- Read `docs/2026-04-30-backend-final-target.md`.
- Read `.trellis/spec/backend/index.md`,
  `.trellis/spec/guides/index.md`, and
  `.trellis/spec/guides/cross-layer-thinking-guide.md`.
- Likely files: `data/content/*`, `data/ui/catalogs/*`,
  `data/ui/layouts/technology_tree.json`,
  generated static catalog outputs, and real-content debug tests.
