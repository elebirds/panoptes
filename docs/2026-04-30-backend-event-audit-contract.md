# Backend Event Audit Contract

Status: M1.2 contract, April 30, 2026.

## Purpose

Backend domain events are the audit trail for real rule decisions. Engine and
game systems may read authoritative state and produce `event.Event` values, but
formal state writes remain behind `Event.Apply(world, state)`. This preserves
the current authority model and keeps resolution reports, debug traces, and
future replay tooling aligned.

## Event Categories

Authoritative state events have an `Apply` method that writes durable or
runtime game state. Examples include planning lock-in events, unit movement and
damage, city founding/capture, building lifecycle changes, point budgets,
research progress, and recipe progress/completion. These events must be emitted
through a resolution or lifecycle collector path when they are part of turn
resolution.

Report-only events record a decision or failed attempt but intentionally do not
write state. Their `Apply` method may be empty, but `Kind()` and `String()` must
still be stable because projection and debug tooling depend on them. Current
examples include `building_skipped`, `recipe_skipped`, `settle_city_failed`,
`conflict`, `resource_flowed`, `minister_acted`, and `player_reconnected`.

Internal state-only events write server state but are not client timeline
events. They can remain in server-side collectors for audit/debugging, but
`game/projection` filters them from `DomainEventEnvelope` output. Current
example: `recipe_selected`, which resets building operation state and is
observed by clients through the post-resolution node snapshot instead.

## Naming And Payload Rules

Every event type must provide:

- A stable snake_case `Kind()` string.
- A non-empty `String()` for debug output.
- A projection payload in `game/projection.EventPayloadFromEvent` when the event
  is client-visible.
- A typed `DomainEventEnvelope` oneof only when the proto already defines one.

The current typed projection oneof field set is deliberately small:
`research_target_changed`, `policy_changed`, `technology_completed`,
`technology_activated`, `unit_moved`, `city_founded`, and `building_built`.
The `policy_changed` oneof still carries the stable event kind
`national_policy_changed` in the envelope `kind` field. Other visible events use
the generic `kind + data` envelope. Adding another typed projection is a
protocol change and must go through the proto source plus `make gen`; M1.2 does
not add any typed event family.

## Authority Rules

`Event.Apply` stays authoritative. New rule systems should not mutate
settlement state directly and should not call another event's `Apply` from
inside an `Apply` method. If two auditable facts must be visible, producers
should emit two events explicitly. If multiple events share mutation mechanics,
extract a private helper instead of nesting event application.

Planning draft writes and planning-start runtime orchestration remain separate
from settlement systems, but any externally meaningful state transition they
surface must still have a stable event kind and projection category.
