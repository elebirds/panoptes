# Move Playback HP Bar And Landing Anchor Fix

## Goal

Fix turn settlement movement playback so the unit stack HP bar follows the moving unit model, and the unit lands on the normal visual center of the destination tile.

## Requirements

- During settlement movement playback, the unit stack HP bar should track the moving `UnitView` transform instead of staying at the destination or static node anchor.
- Unit movement destinations should use a single presentation helper that resolves the visible tile center for unit placement.
- Final `SetUnitNode` placement should snap the unit to the same resolved normal tile unit position used by movement playback.
- Keep the fix presentation-only. Do not change server state, command payloads, or generated protocol files.

## Verification

- C# changes remain under Presentation.
- No generated protocol files are touched.
- `git diff --check` passes.
