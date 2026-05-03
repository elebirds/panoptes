# D1 Authored Content Expansion

## Goal

Continue expanding authored static content after D0, focused on buildings,
recipes, technologies, policies, and units. This pass should broaden the long
game catalog without adding new rules code or changing protocol.

## What I already know

- User explicitly wants more authored source coverage for buildings, recipes,
  technologies, policies, and units.
- D0 ended with 13 buildings, 15 recipes, 13 technologies, 6 units, and 7
  policies.
- Static content source of truth is `data/content/` and `data/ui/`; generated
  runtime catalog files must come from `make data-gen`.
- Existing content systems already support resource outputs, unit outputs,
  building/technology/policy modifier effects, logistics priority, charge,
  siege, road destruction, and technology tree prerequisite validation.

## Assumptions

- D1 should remain a content-only pass.
- No new resource keys or point keys are needed.
- No new map is required for this pass.
- Balance can stay provisional; semantic coverage and long-game variety matter
  more than final numbers.

## Requirements

- Add another content layer across storage/logistics, research, trade, defense,
  and professional military roles.
- Add matching UI catalog entries and technology tree layout nodes/edges.
- Regenerate server and client static catalog outputs.
- Update real-content tests to prove the new authored content loads and at
  least one new unit ability path is ECS-usable.
- Record D1 gate results in docs.

## Acceptance Criteria

- [x] Authored content includes at least 4 new buildings.
- [x] Authored content includes at least 5 new recipes.
- [x] Authored content includes at least 4 new technologies.
- [x] Authored content includes at least 2 new policies.
- [x] Authored content includes at least 2 new units.
- [x] UI catalogs and technology tree cover the new content.
- [x] Generated server and client catalog artifacts are updated.
- [x] D1 gate doc exists.
- [x] `make data-validate`, `make data-gen`, focused real-content tests, full backend tests, `make lint`, and `git diff --check` pass.

## Definition of Done

- Tests added/updated.
- Generated artifacts committed.
- Gate note committed.
- Trellis task marked completed.

## Out of Scope

- No new engine systems.
- No new protocol or generated proto.
- No frontend scene/UI implementation.
- No final balance pass.

## Technical Notes

- Read `docs/STATIC_DATA_EDITING_GUIDE.md`.
- Read `.trellis/spec/backend/index.md`,
  `.trellis/spec/guides/cross-layer-thinking-guide.md`, and
  `.trellis/spec/guides/code-reuse-thinking-guide.md`.
- Likely files: `data/content/{buildings,recipes,technologies,policies,units}`,
  `data/ui/catalogs/*`, `data/ui/layouts/technology_tree.json`, generated
  server/client catalog output, and `server/internal/debug/real_content_catalog_test.go`.
