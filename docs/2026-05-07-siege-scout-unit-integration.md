# Siege Engine And Scout Unit Integration Notes

Date: 2026-05-07

## Scope

This note records the client/data integration work for the `siege_engine` and
`scout` units so later AI agents can safely continue or replace the temporary
assets.

## Unit IDs

- `siege_engine`
- `scout`

The gameplay authoring source already defines both units in
`data/content/units/units.json`, and their production recipes already exist in
`data/content/recipes/recipes.json`:

- `engineer_camp_siege_engine` produces `siege_engine`
- `watchtower_scout` produces `scout`

No protocol field or new server rule was added for this task.

## Catalog Changes

The UI catalog now gives both units dedicated presentation keys:

| Unit | icon_key | prefab_key |
| --- | --- | --- |
| `siege_engine` | `unit_siege_engine` | `SiegeEngine` |
| `scout` | `unit_scout` | `Scout` |

Recipe icon keys were also aligned:

| Recipe | icon_key |
| --- | --- |
| `engineer_camp_siege_engine` | `unit_siege_engine` |
| `watchtower_scout` | `unit_scout` |

The same values were copied into:

- `data/ui/catalogs/units.json`
- `data/ui/catalogs/recipes.json`
- `client/Assets/Resources/Data/sections/units.json`
- `client/Assets/Resources/Data/sections/recipes.json`
- `client/Assets/Resources/Data/catalog.bundle.json`
- `data/generated/server/sections/units.json`
- `data/generated/server/sections/recipes.json`
- `data/generated/server/catalog.bundle.json`

## Client Prefabs

### Scout

Path: `client/Assets/Resources/Prefabs/Units/Scout.prefab`

The scout currently reuses the existing warrior visual from
`client/Assets/Prefabs/Fighter.prefab`, copied into `Resources/Prefabs/Units`
so `MapRenderer` can load it through the `Scout` catalog `prefab_key`.

When a proper scout model exists, replace `Scout.prefab` in place and keep the
`UnitView` component on the prefab root.

### Siege Engine

Path: `client/Assets/Resources/Prefabs/Units/SiegeEngine.prefab`

There is no suitable finished siege model yet. The current asset is a simple
placeholder built from Unity cube mesh parts with a `UnitView` on the prefab
root and `VisualRoot` assigned. It is intentionally named and keyed as
`SiegeEngine` so later art replacement does not require catalog changes.

When final art exists, replace the placeholder visuals or the prefab itself,
but preserve:

- prefab path or `prefab_key`
- root `UnitView`
- a valid visual root transform for facing/move rotation

## Icons

New placeholder unit icons live under:

- `client/Assets/Resources/Icons/Units/unit_siege_engine.png`
- `client/Assets/Resources/Icons/Units/unit_scout.png`

These are production-safe placeholders, not final art. Replace them in place
when final UI art is available so catalog keys remain stable.

## Maintenance Notes

- Do not add client-side gameplay validation for these units. The client should
  only display server/cache state and send player intents.
- If unit stats or production costs change, update `data/content/**` authoring
  data first, then regenerate/sync generated client and server catalog JSON.
- If only visuals change, update `data/ui/catalogs/**` and Resources assets.
- `siege_engine` should remain tagged as `siege`; `scout` should remain tagged
  as `visibility` unless gameplay design changes.
