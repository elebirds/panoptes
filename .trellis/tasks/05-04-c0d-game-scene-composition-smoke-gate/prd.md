# C0d Game Scene Composition Smoke Gate

## Goal

Delay manual Unity scene debugging by adding an automated EditMode gate for the real Game scene and composition-owned Resources prefabs.

## Scope

- Open `Assets/Scenes/Game.unity` in Unity EditMode.
- Assert the scene owns exactly one `GameLifetimeScope` named `Game Composition`.
- Assert `ProjectLifetimeScope` is not serialized into the Game scene.
- Assert critical scene components registered by `GameLifetimeScope` exist exactly once.
- Assert Resources prefab paths used by `ClientCompositionInstaller.RegisterGame` load and carry the expected component.
- Expose the gate through Makefile targets and documentation.

## Acceptance

- `make c0d-check` passes locally.
- The test does not run gameplay logic or client legality validation.
- Documentation explains what C0d catches and how it relates to C0b/C0c.
