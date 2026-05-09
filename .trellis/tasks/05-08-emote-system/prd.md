# brainstorm: emote system

## Goal

Add a multiplayer emote picker and transient display flow: a player opens an emote panel from a HUD button, chooses one emote from a series/page, and all players see that emote briefly before it disappears.

## What I Already Know

* The requested UI is a two-level panel: top row switches emote series, bottom row shows emotes in that series.
* Each page should show 6 emotes.
* Clicking an emote should broadcast it to all players and display it on every player's panel for a short duration.
* Existing protocol already has `ChatEmote`, `MsgSendGameChat`, `MsgGameChatPosted`, and `MsgGameChatSync`.
* Existing server chat service validates supported emotes and broadcasts `MsgGameChatPosted` to the room.
* Existing client `GameIntentService.SendChatEmote` sends `MsgSendGameChat` through `IClientMessageSender`.
* Existing client `GameChatStore` receives chat/emote posts through `StoreMessageHydrator`.
* Existing `GameChatPanelController` is a placeholder/text transcript-style uGUI controller, and its prefab has no child UI wired yet.
* Existing `client/Assets/Art/Pipoya Popup  Emotes Pack` contains 179 split image paths plus atlas/sample paths, but all `.png` and `.gif` files in that pack are 0 bytes.

## Assumptions

* The feature will move to data-driven emote IDs instead of the current fixed enum list.
* Multiple emote series will be supported from the start.
* Emote visibility duration can be client-side presentation state because the authoritative event is the server broadcast.

## Requirements

* Add a HUD button that opens/closes the emote picker.
* Emote picker has a series tab row and a 6-slot emote page row/grid.
* Selecting an emote sends through Core command services, not directly through `NetworkManager`.
* The protocol must carry a stable emote ID that can represent multiple series and pages.
* Server broadcasts the selected emote to all players in the room.
* The game UI places all player avatars on the far left edge of the game view.
* When a player sends an emote, that emote appears beside the sender's avatar.
* Clients render incoming emotes as transient visual popups and remove them after a configured duration.
* All emote art assets are currently absent, so the implementation must tolerate empty placeholders until assets arrive.

## Acceptance Criteria

* [ ] Player can open the emote picker from the game HUD.
* [ ] Player can switch emote series.
* [ ] A page shows up to 6 selectable emotes.
* [ ] Selecting an emote closes or keeps the picker according to final UX decision.
* [ ] All connected players receive and display the selected emote.
* [ ] Displayed emote disappears after the configured lifetime.
* [ ] UI does not call `NetworkManager` directly.
* [ ] No generated protocol files are manually edited.

## Definition of Done

* Tests added/updated where practical.
* Unity prefab/scene references verified.
* Protocol generation used if protocol changes are required.
* Missing assets resolved or tracked as explicit blocker.

## Out of Scope

* Free-form text chat.
* Client-side gameplay validation.
* Paid plugin or new third-party dependency.
* Manually editing generated protocol files.
* Final art production for emotes and avatars.

## Technical Notes

* Protocol source: `protocol/panoptes/proto/v1/chat.proto`.
* Server broadcast path: `server/internal/game/chat/service.go`.
* Client send path: `client/Assets/Scripts/Runtime/Core/Application/Services/GameIntentService.cs`.
* Client receive/store path: `client/Assets/Scripts/Runtime/Core/Application/Stores/StoreMessageHydrator.cs` and `GameChatStore`.
* Existing placeholder UI: `client/Assets/Scripts/Runtime/Presentation/UI/HUD/GameChatPanelController.cs`.
* Existing prefab: `client/Assets/Resources/Prefabs/UI/GameChatPanel.prefab`.
* Asset issue: `client/Assets/Art/Pipoya Popup  Emotes Pack` has metadata but 0-byte image files, so it is not usable as-is.
* Presentation target: avatar rail on the left, emote bubble anchored to the emitting player's avatar.

## Decision (ADR-lite)

**Context**: The current fixed enum emote model is too small for multiple series and later content expansion.

**Decision**: Switch to data-driven emote IDs with series metadata, and render emotes as transient bubbles beside each player's avatar in a left-side roster.

**Consequences**: Requires protocol and client data-model expansion, but gives stable content management and cleaner future asset replacement. The first pass can ship with blank placeholders and wire the UI/layout before art arrives.

## Technical Approach

1. Introduce an emote catalog in static data with stable IDs, series IDs, display order, and asset references.
2. Expand the chat/emote protocol to carry an emote ID instead of only the fixed enum set.
3. Keep server authority on which emote was sent, but keep all visual duration and positioning on the client.
4. Build the left-side avatar rail as presentation state, then attach transient emote bubble views to each avatar slot.
5. Make the picker a two-level panel: series tabs on top, 6-slot page grid below.
6. Keep placeholder art support so layout can be verified before final assets arrive.
