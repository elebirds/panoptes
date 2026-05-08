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
`docs/2026-05-02-client-reactive-presentation-architecture-implementation-plan.md`:
Core stores feed ViewModels, and explicit Binders render either uGUI prefabs or
UI Toolkit UXML/USS. VContainer, R3, and UniTask are approved for this final
architecture with locked versions. Migrated modules should move directly to the
final Composition Root rather than adding singleton compatibility bridges.
R3's Unity package is paired with vendored NuGet core DLLs under
`client/Assets/Plugins/`.

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
    ├── Turn/
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
but they must share the same application state model. New migrated modules must
receive dependencies from VContainer scopes and must not actively call legacy
singleton `*.Instance` APIs.

Command submission lives in Core command services and crosses the network only
through `IClientMessageSender`. Presentation may call injected services or
ViewModels, but not removed static command/send compatibility wrappers.

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

---

## Audio Tooling and Assets

### 1. Scope / Trigger

Use this contract when adding or regenerating client audio placeholder assets
for music or sound effects. Audio generation is a presentation asset workflow;
it must not add gameplay authority, protocol changes, or new runtime command
validation in the Unity client.

### 2. Signatures

Run audio tooling from the repository root:

```bash
node tools/audio/generate-audio.mjs
node tools/audio/generate-audio.mjs verify
node tools/audio/generate-audio.mjs list
```

### 3. Contracts

- Tool location: `tools/audio/generate-audio.mjs`.
- Tool documentation: `tools/audio/README.md`.
- Unity audio asset root: `client/Assets/Art/Audio/`.
- Runtime-loadable output root: `client/Assets/Art/Audio/Resources/Audio/`.
- Music output: `client/Assets/Art/Audio/Resources/Audio/BGM/*.wav`.
- UI sound effects: `client/Assets/Art/Audio/Resources/Audio/SFX/UI/*.wav`.
- Attack sound effects: `client/Assets/Art/Audio/Resources/Audio/SFX/Attack/*.wav`.
- Manifest: `client/Assets/Art/Audio/manifest.json`.
- Runtime clip paths must use manifest `resourcesPath` values such as
  `Audio/SFX/UI/click_confirm`, because Unity resolves them through the nested
  `Resources` folder.
- MVP format: PCM signed 16-bit mono WAV at 44100 Hz.
- Local generator dependencies: Node.js standard library only; do not add npm
  packages for the local procedural path.

### 4. Validation & Error Matrix

- Missing generated audio file -> `verify` must fail.
- Invalid RIFF/WAVE PCM header -> `verify` must fail.
- Wrong channel count, sample rate, bit depth, or sample length -> `verify`
  must fail.
- New third-party package dependency for local generation -> reject the change
  unless the task explicitly approves a provider migration.
- Changes under generated protocol paths -> reject the change.

### 5. Good/Base/Bad Cases

- Good: regenerate all local placeholders with `node tools/audio/generate-audio.mjs`,
  commit the WAVs, manifest, and deterministic Unity `.meta` files together.
- Base: run `node tools/audio/generate-audio.mjs verify` after editing the
  catalog or synthesis code.
- Bad: hand-place ad hoc audio files outside `client/Assets/Art/Audio/` without
  updating the manifest.

### 6. Tests Required

- Run `node --check tools/audio/generate-audio.mjs`.
- Run `node tools/audio/generate-audio.mjs verify`.
- Confirm no dependency manifest changes unless the task explicitly adds a new
  provider.
- Confirm no generated protocol files changed.
- When Unity Editor is available, import/audition generated WAVs before treating
  them as production-quality assets.

### 7. Wrong vs Correct

#### Wrong

```text
client/Assets/Audio/click.wav
```

The file is outside the documented audio root and has no manifest entry.

#### Correct

```text
client/Assets/Art/Audio/Resources/Audio/SFX/UI/click_confirm.wav
client/Assets/Art/Audio/manifest.json
```

The file sits in the runtime-loadable category folder and is discoverable
through the generated manifest.
