# Panoptes Client Code Quality Audit

> Baseline captured on branch `codex/axial-coordinates` after commit `0c4ada8`.
> Scope: `client/Assets/Scripts/Runtime`.

## Summary

The client runtime currently has 122 C# files and 42,487 lines. The Core /
Presentation assembly boundary is mostly intact: Presentation does not reference
`Panoptes.Protocol`, and UI scripts do not call `NetworkManager.Instance`
directly. The highest risks are concentrated in a small set of very large
Presentation components that combine scene bootstrapping, data lookup, runtime
UI construction, input modes, and presentation state.

## High-Risk Files

| File | Lines | Primary risk |
| --- | ---: | --- |
| `Presentation/Map/MapInputHandler.cs` | 3565 | Input modes, raycast, previews, pending command state, combat selection, build placement, and feedback are mixed in one MonoBehaviour. |
| `Presentation/Map/MapRenderer.cs` | 2320 | Map source selection, debug map generation, node rendering, unit rendering, and camera context publishing are mixed. |
| `Presentation/UI/HUD/UnitInfoPanelController.cs` | 2169 | Panel orchestration, portrait rendering, runtime layout repair, action buttons, and planning summary are mixed. |
| `Presentation/UI/Domestic/BuildCommandPanel.cs` | 1881 | Catalog reading, fallback JSON parsing, list rendering, scroll state, tooltip binding, and command dispatch are mixed. |
| `Presentation/Map/SquadUnitVisualController.cs` | 1788 | Squad visual state and animation concerns are large enough to need a later focused pass. |
| `Core/Application/Cache/GameStateCache.cs` | 1542 | Central state cache is large, but it is the intended state mirror boundary and should be split only after Presentation risk is reduced. |

## Scene Lookup Hotspots

Presentation still contains scattered fallback scene lookup:

- `Resources.FindObjectsOfTypeAll<T>()` appears in `ResourceHUD`,
  `CityCoreBuildingActionRegistrar`, `TurnHUD`, and `TechTreePanelBootstrap`.
- `FindAnyObjectByType` / `FindObjectsByType` appears across map, HUD, domestic
  UI, and game scene bootstrap classes.
- Several callers need the same rule: fallback lookup may return only valid
  scene instances, never prefab/assets. This rule should live in one helper.

Priority: centralize scene-object lookup in `Panoptes.Presentation.Common` and
replace local loops before larger refactors.

## Layer Boundary Checks

- `Presentation` to `Protocol`: no direct `Panoptes.Protocol` references found
  under `client/Assets/Scripts/Runtime/Presentation`.
- UI to network: no `NetworkManager.Instance` usage found under
  `client/Assets/Scripts/Runtime/Presentation/UI`.
- Existing command flow is still routed through Core services/intents such as
  `MessageSender`, `LobbyService`, and `GameIntents`.

These are healthy constraints and must be preserved during restructuring.

## Subscription and Lifecycle Risks

Several Presentation components subscribe directly to singleton cache events in
`OnEnable` and unsubscribe in `OnDisable`. The pattern is correct in intent but
implemented manually in many files, which increases the chance of duplicate
subscriptions or stale source references when singleton objects are recreated in
tests.

Priority targets:

- `UnitInfoPanelController`
- `BuildCommandPanel`
- `ResourceHUD`
- `TurnHUD`
- `RecipeSynthesisPanel`

## Test Baseline

Current EditMode coverage exists under `client/Assets/Scripts/Tests/EditMode`
and includes integration coverage for runtime scene bindings, build panels,
common overlays, tech tree state, feedback, fonts, map/grid behavior, and lobby
state reduction.

The restructuring should keep the existing Unity EditMode suite as the primary
regression gate. New tests should be added for new reusable helpers and for any
extracted collaborator with non-trivial behavior.

## Recommended Order

1. Centralize scene lookup helpers and replace duplicated fallback lookup.
2. Stabilize UI subscription/lifecycle patterns where they are already being
   touched.
3. Split `UnitInfoPanelController` into portrait, planning summary, action
   button, and layout collaborators.
4. Split `BuildCommandPanel` into model building, list rendering, scroll state,
   and fallback config parsing.
5. Split `MapInputHandler` by input modes and preview/pending state.
6. Split `MapRenderer` by source resolution, debug data generation, node
   rendering, unit rendering, and camera context publishing.

Every step must preserve public MonoBehaviour entry points and serialized field
compatibility unless scenes/prefabs are updated and verified in the same commit.
