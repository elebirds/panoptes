# Integrate Audio Playback and Revise BGM Catalog

## Goal

Revise Panoptes audio assets to match the requested game music roles and integrate UI click plus attack sound effect playback into the Unity client without adding gameplay authority or new third-party dependencies.

## Requirements

* Revise the three BGM catalog entries and generated audio files:
  * Main menu BGM: medieval rustic countryside music, relaxed, melodic, lazy plains village life, with a slightly modern/trendy flavor.
  * First fifteen turns BGM: orchestral calm before battle, preparation-phase tension, restrained but sword-drawn atmosphere.
  * Later fifteen turns BGM: orchestral, energetic two-armies-clashing combat feeling.
* Keep the local audio generator dependency-free and runnable with `node tools/audio/generate-audio.mjs`.
* Connect UI button click audio to client UI interactions through a presentation-safe service, not through gameplay logic.
* Connect attack audio to damage presentation feedback: unit HP loss, death-with-damage cue, and building damage should play attack/impact SFX only when resolved damage is greater than zero.
* Do not modify generated protocol files.
* Do not introduce forbidden dependencies.

## Acceptance Criteria

* [x] `node tools/audio/generate-audio.mjs` regenerates BGM assets with the new roles and style notes.
* [x] `client/Assets/Art/Audio/manifest.json` reflects the new BGM ids/usages/prompts.
* [x] UI click SFX can be played from button interactions via a reusable client audio helper/service.
* [x] Attack SFX can be played from existing damage presentation code when resolved damage is greater than zero.
* [x] No UI script directly calls `NetworkManager` as part of this work.
* [x] No client-side gameplay legality/rule computation is added.
* [x] Script checks and lightweight verification pass.

## Definition of Done

* Tooling/assets updated.
* Runtime client code added or updated with minimal presentation-only integration.
* Relevant EditMode/unit checks added where practical.
* Specs/task notes updated if a new convention is established.

## Technical Approach

Inspect existing composition roots, UI button binding patterns, and combat feedback presenters. Prefer adding a small Presentation audio service that loads `AudioClip` assets from `client/Assets/Art/Audio` via Unity Resources-compatible references if feasible, or serialized/scene-safe fallbacks if not. Keep runtime behavior optional and non-authoritative: missing clips should warn or no-op, never block commands.

## Out of Scope

* Paid/cloud BGM generation.
* FMOD/Wwise integration.
* Full dynamic adaptive music system.
* Reworking every UI prefab manually if a centralized button-click hook is available.
* Final mix/master quality.

## Technical Notes

* Related previous task: `.trellis/tasks/05-08-audio-production-tool-layer/`.
* Existing generated audio root: `client/Assets/Art/Audio/`.
* Relevant specs: frontend directory structure, component guidelines, state management, quality guidelines.
* 2026-05-08 verification:
  * `node --check tools/audio/generate-audio.mjs`
  * `node tools/audio/generate-audio.mjs verify`
  * `node tools/audio/generate-audio.mjs list`
  * `git diff --check` on touched text files
  * generated protocol diff check returned no files
  * dependency manifest diff check returned no files
* Unity Editor import/audition was not run because Unity was not available in PATH or the checked `6000.4.1f1` Hub path.
