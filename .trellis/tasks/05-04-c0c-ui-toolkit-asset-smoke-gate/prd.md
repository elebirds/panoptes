# C0c UI Toolkit Asset Smoke Gate

## Goal

Delay manual Unity debugging by adding an automated EditMode gate for C0a UI Toolkit assets and prefabs.

## Scope

- Load authored UI Toolkit management prefabs from `Assets/Resources/Prefabs/UI`.
- Assert each prefab carries the expected binder component.
- Assert serialized `VisualTreeAsset` and `StyleSheet` references are assigned to the intended UXML/USS asset.
- Assert cloned UXML exposes every named element required by the binder contract.
- Cover `TurnSummary` as a UXML/USS asset contract until it has an authored Resources prefab.
- Expose the gate through Makefile targets.

## Acceptance

- `make c0c-check` passes locally.
- New test avoids protocol/generated file edits and does not add gameplay legality checks.
- Documentation explains when to use the gate and what it catches.
