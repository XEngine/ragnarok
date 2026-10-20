# Module organization

`Plugin.cs` is the composition root: it constructs settings and services, wires server-controlled options, ticks active components and disposes them before releasing the shared theme. It does not implement gameplay rules. `GlobalUsings.cs` lists shared module namespaces and explicit compatibility aliases; no dependency packages were added.

## Code ownership

- `HUD/`: character/vitals, hotbar, status effects, minimap, compass, prompts, provisions and notifications. Namespace `Ragnarok.HUD`.
- `Ui/Shared/`: Unity UI builders, fonts, procedural sprites, input blocking and shared visual primitives. Namespace `Ragnarok.UI`.
- `Ui/Windows/`: inventory, crafting, chests, item details and books. Namespace `Ragnarok.UI.Windows`.
- `Features/Gameplay/`: skill XP/loss, resource costs, Forsaken powers, harvest yields, sailing, ballistas and station modifiers.
- `Features/QualityOfLife/`: chest search/preview, weapon dial, planting information, inventory sorting/stacking controls, crafting filters and extra hotbar access.
- `Features/Equipment/` and `Features/NearbyStorage/`: equipment slot rules, tombstone recovery and owner-approved resource transactions.
- `Configuration/`: `InterfaceSettings` holds presentation options and composes `FeatureSettings`; `GameplaySettings` holds balance options. `HudPlacement` owns layout types. `ConfigSchema` defines migration and wire-name compatibility; `ConfigMigration` adapts saved BepInEx files. The settings window and controls live here as well.
- `Networking/`: server setting synchronization, authority checks and local-value restoration.
- `Development/`: optional runtime checks and isolated previews. Pure regression checks remain in `tests/Ragnarok.Tests`.

## Settings groups

**Gameplay** contains equipment rules, skill loss/XP, stamina/eitr costs, powers, harvest multipliers, station capacities/speed, sailing, ballistas and server locking.

**Quality Of Life** contains nearby chest resource access, quick stack, sort preferences, tombstone re-equipping, chest search, weapon-dial activation, plant status, crafting filters, provision shortcuts and second hotbar access.

**HUD** contains HUD style, placement, scale, inventory/crafting/book appearance, minimap, notifications, the settings window and development display/tools. Mixed former sections were split by responsibility: for example, weapon-dial appearance remains under HUD while its enable/key options live under Quality Of Life. Internal layout migration markers are not exposed as user controls.

A category does not determine authority: the explicit synchronized-entry list in Plugin remains authoritative. Server-owned convenience features stay synchronized even though their pages now live under Quality Of Life. Local visual choices and shortcuts stay local.

## Migration and compatibility

Before binding settings, migrate BepInEx's unbound entries. Legacy `QoL.*` balance pages become `Gameplay.*`; `Inventory.*` appearance pages become `HUD.*`, with behavior keys routed individually. `Interface` becomes `HUD.Window`, `Server` becomes `Gameplay.Server`, and `NearbyChests` becomes `QualityOfLife.NearbyChests`.

Migration preserves values and unknown entries. If both legacy and new definitions exist, the new definition wins. A first migration backs up the original file to `<config>.before-modules.bak`; repeating migration does not overwrite that backup. Initialization suppresses incremental saves and saves the resulting configuration after successful setup. If BepInEx's unbound-entry API is unavailable, startup fails rather than silently replacing settings with defaults.

The synchronization protocol continues to send legacy section names for existing synchronized keys. Incoming names are canonicalized. Pure regression checks cover both directions, but mixed-version multiplayer has not received an in-game test. Packets are accepted only from the connected server, and registered RPC handlers are removed on unload or network replacement.

## Bug fixes included

- Failed initialization now runs the same guarded cleanup as unload and disables the component.
- Equipment option delegates are initialized on dedicated servers, independently of UI construction.
- Configuration RPC handlers are unregistered, settings are released across network changes, and updates from non-server peers are rejected.
- Dedicated-server file-change notifications use an atomic pending flag.
- Generated font assets release their owned materials and atlas textures on reload; failed monochrome icon conversion releases its temporary texture.
- Internal layout markers no longer appear in settings or participate in page reset.

## Verification

Tombstone recovery keeps the existing synchronized `AutoEquipOnReclaim`, character
slot and extra-utility settings. `GraveRecoveryPlan` makes a read-only prediction
of available special slots followed by native MoveAll's preferred-position and
fallback stacking passes. The Use fit check retains the native carry-weight gate;
native Interact/Container.TakeAll access checks, RPC approval and callbacks still
run. Actual special-slot transfers occur only for the local player's own grave
after the local ZNetView has ownership, using native positioned inventory moves.
No ownership is claimed by recovery code and no new RPC is registered.

Existing owner-tagged equipment is supported. New provision-slot tags live in
item custom data alongside equipment markers and survive grave serialization.
Occupied slots are never overwritten by direct restoration; leftovers follow
normal transfers or remain in the grave. Equip retries/slot settling run after
transfers, and the existing Reset/unpatch lifecycle clears pending work on unload.

Native Linux equivalents are `./dev.sh test`, `./dev.sh build` and
`./dev.sh build --configuration Release`; see [LINUX.md](LINUX.md) for isolation,
setup and development deployment. LocalSaves is development-only and uses the
installed BepInEx Mono.Cecil assembly; it is not a plugin dependency.

Crafting keeps native selection/actions behind owned presentation. `CraftSearch`
holds pure search/food rules, `CraftFilter` adapts native lists, and `RecipeDetails`
groups untranslated native tooltip lines before localization. `CraftFrameShape`
defines the repair contour; `CraftFrameGraphic` renders it. Regression checks also
cover these pure rules and contour geometry.

Run on Windows using the installed development game assemblies:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File ./dev.ps1 test
powershell -NoProfile -ExecutionPolicy Bypass -File ./dev.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File ./dev.ps1 build -Configuration Release
powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/check-config.ps1
```

`check-config.ps1` exercises the built migration code against the installed BepInEx reader with temporary files. It neither starts Valheim nor edits the development game's actual configuration. The regression suite covers allocation, death penalties, config migration precedence, idempotence and legacy wire names.

In-game acceptance still requires F6 reload, inspecting the three settings groups, verifying server locks with a client, reconnecting, and checking vanilla/custom UI restoration. No new in-game test was performed for this refactor because the development game was already running; its session was not interrupted to run the startup smoke script.

Vanilla crafting enhancements use the same CraftFilter state and the existing
CraftAmount/native multi-craft path as the custom panel. They introduce no new
RPC, resource withdrawal path, server settings or crafting permissions. Material
source counts are read-only snapshots refreshed at 5 Hz through the existing
ResourceScope, whose access/range/world-level and server-controlled crafting
storage checks remain in force. Quantity affordability uses native
HaveRequirements and native crafting still validates/pays the final job.
VanillaCraftExtras owns its input controls and uses the native inventory tooltip
prefab for backed material hovers, with no extra material source labels; it restores
native button geometry, amount text sizing and tooltip state on disposal. The
custom panel resets shared quantity only when it actually owned a panel, so its
inactive Tick does not overwrite the vanilla quantity input every frame.

## Reference-based build menu, 2026-10-20

The reference build-menu layout is implemented as an owned view over active native
BuildUi. A separate owned CanvasGroup masks it without changing existing native
or other-mod groups; it is neutralized and removed on unload. TabHandler enabled
state is saved/restored. A plugin-owned Harmony prefix bypasses only
BuildUi.NavigationUpdate while this view owns input, and the plugin's existing
Harmony owner removes that patch on unload. Native BuildUi still manages available
pieces, discovery, lists, favorite persistence and tool changes. Native piece
selection is invoked only for a piece available in the current build tool; the
card click selects the building preview and does not spawn or pay for a piece.

The owned window is centered at 1120 by 580 canvas units, scales down to fit with
24-unit outer margins, and never scales above its designed size. Only this window
has a backdrop and frame; its fullscreen layout host has no graphic or raycast
surface. The sidebar and widened card grid scroll within the window. The right detail
panel is removed; card clicks invoke the original BuildUiPieceButton.OnClickButton. Pointer hover
and controller focus forward to BuildUi.OnHoverPiece with the matching native
button, updating the existing HUD readout. Exit/deselection/unload clear hover.
No permanent piece-name text or reserved name area remains in icon tiles. Compact density steppers sit between the category heading and piece count, outside
the clipped card viewport. The header uses matching 36-unit search/action controls.
Station upgrade markers use Piece.m_isUpgrade and the native button arrow sprite.
The experimental quickbar, pinning, saved shortcuts and number-key handling are
removed. HUD.BuildMenu density, preview toggles and keyboard bindings remain
local UI preferences, not synchronized server rules.
Nearby preview counting uses the existing read-only ResourceScope and
requires the server-controlled Enabled/Building storage flags. Its local toggle
changes displayed stock only, never actual withdrawal, ownership or access policy.
No new RPC, ownership claim, resource payment, placement rule or external dependency
is added. Counts cannot guarantee placement: native station, collision, terrain,
DLC and other final placement rules still run through the original game path.

Card objects are pooled to the visible rows plus one overscan row. Full logical
piece lists remain native; the owned top-left hammer selects native repair mode and does not
inherit native repair-button recreation flashes. Search descriptions and craftable counts come from
real Piece data and stock snapshots. Search and read-only counts refresh on a bounded
schedule. Owned roots, listeners, typing registrations, focus callbacks and active
static references are removed/restored on HUD replacement/style change/unload.
The design follows the user's supplied reference image; no pixels, outside code
or new assets were copied. Existing licensed fonts/generated theme and installed
native icons are reused.
