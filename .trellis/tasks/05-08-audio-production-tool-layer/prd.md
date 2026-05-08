# Build Audio Production Tool Layer

## Goal

Create a repo-local audio production tool layer that lets Codex generate and manage Panoptes placeholder audio assets for three background music tracks, UI button click sounds, and attack sound effects. The first version should work without paid third-party plugins or network APIs, while leaving a clean seam for later API-backed generation.

## What I already know

* The user needs three background music tracks.
* The user needs multiple button click sound effects.
* The user needs attack sound effects.
* Panoptes is a Unity client project, so generated usable assets should land under `client/Assets/`.
* Project rules forbid introducing undocumented third-party dependencies and forbid putting gameplay authority in the client.
* This task is tooling and presentation-asset generation only; it does not alter protocol, server logic, or game rules.

## Assumptions

* MVP should generate usable synthetic placeholder `.wav` files locally rather than depending on ElevenLabs/Stable Audio credentials.
* Audio assets should be organized under a Unity-friendly folder such as `client/Assets/Art/Audio/`.
* Tooling should be scriptable from the repo root on Windows PowerShell.
* Generated audio should be intentionally rough-but-usable placeholders, not final soundtrack mastering.

## Requirements

* Add a repo-local audio tool layer under `tools/audio/`.
* Generate exactly three background music `.wav` assets suitable as loopable placeholders.
* Generate a small library of UI button click `.wav` assets.
* Generate a small library of attack `.wav` assets.
* Produce a manifest documenting asset IDs, categories, file paths, intended usage, and prompts/style notes.
* Keep the generator dependency-free using standard library/runtime capabilities available in the repo environment.
* Avoid client gameplay logic changes.
* Leave extension seams for future external API providers.

## Acceptance Criteria

* [x] A single documented command can generate all MVP audio assets.
* [ ] Generated `.wav` files are written under `client/Assets/Art/Audio/` in category folders.
* [x] Manifest data is generated and kept beside the audio assets.
* [x] Tooling is deterministic enough for repeatable local regeneration.
* [x] No generated protocol files are modified.
* [x] No new third-party dependency is introduced.

## Definition of Done

* Tooling added and documented.
* Assets generated once into the Unity project.
* Basic verification confirms expected files exist and are valid WAV headers.
* Trellis context files include relevant frontend specs.

## Technical Approach

MVP uses a local Node.js script that writes PCM WAV files directly with procedural synthesis. This avoids Python availability issues and avoids paid/network APIs. The script exposes an asset catalog, synth helpers, and a CLI entrypoint. Future API providers can replace the synthesis backend while preserving the manifest and output structure.

## Decision (ADR-lite)

**Context**: Music-generation APIs and DAW bridges require credentials or external software, while this repo needs an immediately usable tool layer.

**Decision**: Implement a dependency-free local procedural WAV generator first, with provider seams documented for later ElevenLabs/Stable Audio/DAW integration.

**Consequences**: Output quality is placeholder-grade, but the pipeline is reliable, versionable, and usable by Codex immediately. Later replacement with API-generated assets can keep the same asset IDs and Unity paths.

## Out of Scope

* Integrating paid/cloud audio generation credentials.
* Building a runtime Unity `AudioService` in this task.
* FMOD/Wwise integration.
* Final music production/mastering quality.
* Automatically wiring every UI button or combat animation to the new assets.

## Technical Notes

* Relevant specs: `.trellis/spec/frontend/index.md`, directory structure, component guidelines, state management, quality guidelines.
* Use `tools/audio/` for repo tooling and `client/Assets/Art/Audio/` for Unity assets.
* Existing Python entrypoint is unavailable in this environment, so Trellis task files were created manually.

## Verification Notes

* 
ode --check tools/audio/generate-audio.mjs passed.
* 
ode tools/audio/generate-audio.mjs verify passed with 11 WAV files and manifest.
* 
ode tools/audio/generate-audio.mjs list lists 3 BGM, 4 UI click SFX, and 4 attack SFX.
* Scoped git diffs for protocol/generated-code paths and dependency manifests were empty.
* Unity Editor import/audition was not run in this environment.

