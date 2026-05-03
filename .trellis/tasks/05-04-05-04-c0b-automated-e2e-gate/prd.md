# C0b Automated End-to-End Gate

## Goal

Delay manual Unity debugging by adding a deterministic automated gate that exercises the current backend protocol frames against the client Store/ViewModel path without opening the Unity editor.

## Scope

- Generate backend-owned `ServerFrame` protojson fixtures from a real debug scenario and a representative static catalog snapshot.
- Replay those frames in client EditMode tests through `MessageDispatcher` and `StoreMessageHydrator`.
- Assert the replay reaches C0a management ViewModels: catalog data, game/planning stores, planned research, next-turn planning events, and pending build projection.
- Provide a single command for the gate so future C0a/C0b UI work has a repeatable check before manual prefab inspection.

## Non-Goals

- Do not replace Unity batchmode import/PlayMode testing.
- Do not test visual layout pixels or manually authored prefab aesthetics.
- Do not add gameplay legality checks to the client.
- Do not introduce a new protocol format or modify generated protocol files.

## Acceptance Criteria

- [x] Backend fixture generation is deterministic and lives in a normal repo command path.
- [x] Client offline replay parses server protojson `ServerFrame` lines with generated C# protocol classes.
- [x] Replay hydrates Core stores through existing dispatcher/hydrator infrastructure.
- [x] Replay verifies at least one management ViewModel from each key C0a surface family: tech, build catalog, and national overview.
- [x] `make c0b-check` documents the intended automated gate.
- [x] The gate includes a filtered Unity EditMode run for the replay test, because `dotnet test` only protects assembly-level compilation in this Unity project shape.

## Verification

- `cd server && go run ./cmd/c0bgatefixture -out ../client/Assets/Scripts/Tests/EditMode/Fixtures/C0b/server_frames.jsonl`
- `cd server && go test -count=1 ./cmd/c0bgatefixture ./internal/debug ./internal/transport/codec`
- `dotnet build client/Panoptes.Tests.EditMode.csproj`
- `dotnet test client/Panoptes.Tests.EditMode.csproj --no-build`
- `make c0b-check-unity`
- `git diff --check`
