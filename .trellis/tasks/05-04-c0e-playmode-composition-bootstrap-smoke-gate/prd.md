# C0e PlayMode Composition Bootstrap Smoke Gate

## Goal

Delay manual Unity debugging one more layer by verifying the final composition roots build in real PlayMode lifecycle, not only by static text checks or EditMode asset loading.

## Scope

- Let runtime bootstrap create the project composition prefab.
- Assert exactly one `ProjectLifetimeScope` exists and has a built container.
- Load the `Game` scene through Unity scene management.
- Assert exactly one `GameLifetimeScope` exists, parents to the project scope, and has a built child container.
- Resolve critical project and game dependencies from VContainer, including C0a Store/Service/ViewModel/Binder registrations.
- Expose the gate through Makefile targets and documentation.

## Acceptance

- `make c0e-check` passes locally.
- `make c0-ui-check` includes C0e after C0b/C0c/C0d.
- The test does not connect to the backend, log in, submit commands, or run gameplay legality logic.
- Documentation explains what C0e catches and what it intentionally does not cover.
