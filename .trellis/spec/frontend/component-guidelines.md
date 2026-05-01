# Component Guidelines

> How components are built in this project.

---

## Overview

<!--
Document your project's component conventions here.

Questions to answer:
- What component patterns do you use?
- How are props defined?
- How do you handle composition?
- What accessibility standards apply?
-->

Unity UI and map scripts should be built as prefab-facing facades plus small
testable helpers. A facade MonoBehaviour owns serialized fields and Unity
lifecycle methods; helpers own rendering, lookup, event bookkeeping, or
read-only view-model construction.

Long-term target: information-heavy panels may use UI Toolkit with explicit
Binders, while map/HUD/world-space UI remains on uGUI. Both styles must consume
the same Core DTO/store/ViewModel layer.

---

## Component Structure

<!-- Standard structure of a component file -->

Recommended shape:

```csharp
public sealed class SomePanel : MonoBehaviour
{
    [SerializeField] private SomeItemView itemPrefab;

    private readonly SomePanelRenderer _renderer = new();
    private readonly EventSubscriptionBag _subscriptions = new();

    private void OnEnable()
    {
        _subscriptions.Add(
            () => cache.OnChanged += Refresh,
            () => cache.OnChanged -= Refresh);
        Refresh();
    }

    private void OnDisable()
    {
        _subscriptions.Clear();
        _renderer.Clear();
    }
}
```

Keep serialized field names stable unless the related prefab/scene assets are
updated and verified in the same commit.

For UI Toolkit binders, prefer explicit `root.Q<T>("name")` lookup and a single
`Render(state)` method during the pilot phase. Do not start with reflection,
string-path binding engines, or hidden view locators.

---

## Props Conventions

<!-- How props should be defined and typed -->

Unity serialized fields are the primary "props" for prefab-facing components.
New runtime collaborators should be plain C# helpers created by the facade, not
new required scene singletons.

---

## Styling Patterns

<!-- How styles are applied (CSS modules, styled-components, Tailwind, etc.) -->

Use existing uGUI/TextMeshPro styling and prefab styling. Do not generate final
production UI prefabs from code for C0/C0p; code may provide binders and
presenters for manually-authored prefabs.

For C0a+, UI Toolkit UXML/USS is allowed as a target plan for dense management
panels once dependency policy is amended. Keep uGUI for scene-bound map
presentation.

---

## Accessibility

<!-- A11y requirements and patterns -->

Prefer predictable focus/click behavior and avoid hidden client-side
validation. Disabled or locked states must reflect server/Core cache state or
static catalog metadata, not client-authored gameplay rules.

---

## Common Mistakes

<!-- Component-related mistakes your team has made -->

- Adding backend/protocol types directly to Presentation.
- Adding gameplay legality checks to UI click handlers.
- Binding button listeners in `Awake` without symmetric unsubscribe.
- Using fallback scene lookup when a serialized reference or
  `SceneObjectFinder` is available.
- Letting UI Toolkit and uGUI panels duplicate business state instead of sharing
  Core stores/ViewModels.
