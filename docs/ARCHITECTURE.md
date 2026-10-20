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
