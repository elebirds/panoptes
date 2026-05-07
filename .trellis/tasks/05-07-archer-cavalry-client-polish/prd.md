# Archer And Cavalry Client Polish

## Goal

Make archer and cavalry feel like distinct client units instead of both falling back to the generic infantry-like presentation.

## Requirements

- Static UI unit catalog points archer and cavalry at distinct unit prefab keys.
- Archer uses an archer-specific unit icon key and client Resources icon asset.
- Map unit instantiation can resolve a unit's `prefab_key` from the static catalog and load a matching Resources prefab, while retaining existing base vehicle and generic unit fallbacks.
- Prefabs that do not already carry `UnitView` still work by receiving a runtime `UnitView` component after instantiation.
- Archer attacks show lightweight ranged visual feedback using presentation-only tracer effects.
- Cavalry charge orders show immediate charge feedback and still submit through `PlanningIntentService.ChargeUnit`.

## Boundaries

- Do not edit generated protobuf files.
- Do not add client-side gameplay legality checks.
- Do not change command payloads or server rules.
- Keep changes scoped to static UI data, client Resources assets, and map presentation.

## Verification

- Search confirms generated protocol files are untouched.
- Client C# compiles under the existing Unity project conventions as far as local tooling allows.
- Static catalog copies used by the client stay in sync with the authored unit catalog changes.
