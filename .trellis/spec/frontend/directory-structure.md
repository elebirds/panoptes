# Directory Structure

> How frontend code is organized in this project.

---

## Overview

<!--
Document your project's frontend directory structure here.

Questions to answer:
- Where do components live?
- How are features/modules organized?
- Where are shared utilities?
- How are assets organized?
-->

Panoptes client code is a Unity project under `client/Assets/Scripts`. Runtime
code is split by dependency direction:

- `Protocol/`: generated protobuf C# files. Do not edit manually.
- `Runtime/Core/`: DTOs, caches, mappers, services, and network/application
  code. Core may reference `Panoptes.Protocol`.
- `Runtime/Presentation/`: Unity views, presenters, binders, input adapters,
  and animation. Presentation references Core and must not reference Protocol.
- `Tests/EditMode/`: static boundary tests and helper/presenter tests.

The long-term target is documented in
`docs/2026-05-01-client-reactive-ui-architecture-plan.md`: Core stores feed
ViewModels, and explicit Binders render either uGUI prefabs or UI Toolkit
UXML/USS. This is a migration target; do not install new packages or move
existing prefabs until the dependency policy is updated.

---

## Directory Layout

```
client/Assets/Scripts/
├── Protocol/
├── Runtime/
│   ├── Core/
│   │   ├── Application/
│   │   ├── Foundation/
│   │   └── Infrastructure/
│   └── Presentation/
│       ├── Common/
│       ├── Map/
│       │   └── InputAdapter/
│       ├── Planning/
│       │   ├── Feedback/
│       │   └── Input/
│       └── UI/
└── Tests/
    └── EditMode/
```

Target additions for C0a+:

```
client/Assets/Scripts/Runtime/Presentation/
├── Composition/
├── ViewModels/
├── Binders/
│   ├── Ugui/
│   └── UiToolkit/
└── UI/

client/Assets/UI/
└── Toolkit/
    ├── Minister/
    ├── Tech/
    ├── Policy/
    └── Shared/
```

---

## Module Organization

<!-- How should new features be organized? -->

Keep prefab-facing MonoBehaviours in their existing UI/Map folders. When a
MonoBehaviour grows, extract testable non-MonoBehaviour helpers beside it, for
example:

- `BuildCommandPanel` delegates list rendering to `BuildCommandListRenderer`.
- `RecipeSynthesisPanel` delegates rendered item bookkeeping to
  `RecipeSynthesisRenderedItemRegistry`.
- `MapRenderer` delegates map source and camera context work to Map helpers.

Do not move a MonoBehaviour that scenes or prefabs may reference unless the
scene/prefab assets are updated and verified in the same change.

For new UI Toolkit work, keep UXML/USS assets outside generated code paths and
route state through Core stores and ViewModels. UI Toolkit and uGUI may coexist,
but they must share the same application state model.

---

## Naming Conventions

<!-- File and folder naming rules -->

Use descriptive helper names that state the owned responsibility:

- `*Resolver` for read-only lookup/context resolution.
- `*Renderer` for Unity view/list rendering helpers.
- `*Presenter` for presentation logic that mutates views from render models.
- `*Registry` for lifecycle and event bookkeeping.
- `*Queries` for read-only Core cache/query helpers.

---

## Examples

<!-- Link to well-organized modules as examples -->

- `Presentation/UI/Domestic/BuildCommandListRenderer.cs`
- `Presentation/UI/Turn/RecipeSynthesisRenderedItemRegistry.cs`
- `Presentation/Map/MapSourceResolver.cs`
- `Core/Application/Cache/GameStateCacheReadQueries.cs`
