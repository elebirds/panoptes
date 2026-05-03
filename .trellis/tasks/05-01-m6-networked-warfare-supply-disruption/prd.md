# M6 Networked Warfare And Supply Disruption

## Goal

Extend warfare from killing units and city cores into damaging the national
machine: roads, facilities, storage, and frontline supply.

## Scope

- Add backend map actions for road destruction and city-storage raids.
- Add authoritative storage raid events.
- Make combat upkeep account for road-network supply reachability.
- Add static raider/siege unit definitions as backend-owned unit spectra.
- Keep conquest victory as the only active victory condition.

## Non-Goals

- No active defense-zone automation.
- No client-side legality checks.
- No minister military planning yet.
- No new victory condition.

## Acceptance

- Cutting roads can make frontline units unsupplied/starving.
- Sabotage can destroy roads and affect M3/M4 logistics.
- Raids can reduce city storage through auditable events.
- City core destruction remains conquest/game-over path.
- `cd server && go test -count=1 ./...` and `make lint` pass.
