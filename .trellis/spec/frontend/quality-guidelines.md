# Quality Guidelines

> Code quality standards for frontend development.

---

## Overview

<!--
Document your project's quality standards here.

Questions to answer:
- What patterns are forbidden?
- What linting rules do you enforce?
- What are your testing requirements?
- What code review standards apply?
-->

The client quality bar protects Unity prefab compatibility and the pure
presentation boundary. Most changes should be small extractions from large
MonoBehaviours into helper classes with EditMode/static coverage.

---

## Forbidden Patterns

<!-- Patterns that should never be used and why -->

- Manual edits under `client/Assets/Scripts/Protocol/`.
- `Panoptes.Protocol` references under `client/Assets/Scripts/Runtime/Presentation`.
- `NetworkManager.Instance` usage under `client/Assets/Scripts/Runtime/Presentation/UI`.
- UI scripts performing gameplay legality validation or resource affordability
  checks.
- Broad scene/prefab-facing MonoBehaviour renames without updating and
  verifying affected assets.

---

## Required Patterns

<!-- Patterns that must always be used -->

- Keep Presentation depending on Core DTO/cache APIs, not protocol messages.
- Keep UI command submission routed through Core services/intents.
- Use `EventSubscriptionBag` for repeated event/button subscriptions where
  lifecycle symmetry matters.
- Prefer `SceneObjectFinder` for fallback scene lookup when serialized
  references are unavailable.
- Keep extracted helpers beside their facade unless they are clearly shared.

---

## Testing Requirements

<!-- What level of testing is expected -->

- Add EditMode tests for extracted non-trivial helpers.
- Keep static boundary tests green:
  - no Protocol usage in Presentation;
  - no direct NetworkManager singleton usage in Presentation UI;
  - high-risk facade line counts should not grow past their captured baseline
    without updating the audit/gate docs.
- Run Unity batchmode or EditMode tests when a licensed Unity environment is
  available. If TestRunner XML is unavailable, record the limitation and at
  least verify Unity script import/compile logs contain no `error CS`.

---

## Code Review Checklist

<!-- What reviewers should check -->

- Does the change preserve serialized field and public MonoBehaviour entry
  compatibility?
- Did large MonoBehaviours shrink or stay stable?
- Did helpers avoid becoming hidden gameplay rules?
- Are event listeners unsubscribed symmetrically?
- Are generated protocol files untouched?
