# M7 Minister Default Execution Layer

## Goal

Make ministers the server-owned default executor for the existing national machine. When a player does not manually fill planning drafts, rule-based ministers should propose and/or apply auditable planning intents that share the same structures as player orders.

## Scope

- Add a deterministic rule-based default planning pass for ministers.
- Reuse existing planning intent/order structures for research, policy, building, recipe, logistics-adjacent construction, and military/unit orders where already supported.
- Preserve player override precedence: manual player drafts win over minister defaults for the same surface.
- Keep LLM out of authority; LLM may polish draft text only.
- Add tests proving no-client/default-minister turn planning can create executable drafts.

## Non-Goals

- No personality simulation beyond existing minister metadata.
- No new proto unless an existing planning structure cannot represent the intent.
- No client-side validation.
- No M8 information distortion yet.

## Acceptance

- At planning start or server preparation, ministers can populate useful default planning drafts for a player.
- Player-approved/overridden commands replace corresponding default intent.
- Minister-generated plans are visible in planning snapshots or traceable in state.
- `cd server && go test -count=1 ./...` and `make lint` pass.
