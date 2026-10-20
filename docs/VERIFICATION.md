# Development setup verification

## Consolidated documentation and commit checks, 2026-10-20

- Updated README, architecture notes and the HUD guide's current-state summary; added a consolidated `CHANGELOG.md` for the accumulated Linux, HUD, inventory, tooltip, notification and crafting work. Historical entries remain dated and may describe superseded trials.
- Final `./dev.sh build --configuration Release`: passed, zero warnings/errors. `./dev.sh test`: passed all existing allocation/config/death checks plus crafting search/classification, frame geometry and grouped recipe details. `bash -n dev.sh scripts/setup.sh`, Python AST parsing and `git diff --check` passed.
- No new live-game, startup smoke, multiplayer or Windows build performed during this documentation pass. Prior development deployments and user screenshots/feedback are recorded separately; final repair visibility and visual/input acceptance remain pending.

## Hide unavailable repair actions, 2026-10-20

- Repair visibility now also requires native button interactability. The entire owned control and frame extension disappear when no repair action is available, including at an otherwise repair-capable station with no eligible damaged equipment. This intentionally removes the former muted disabled state.
- The control and frame extension start hidden during construction, preventing a default-visible extension before state refresh. Native repair eligibility and click execution remain unchanged.
- `./dev.sh deploy` passed with zero warnings/errors; `git diff --check` passed. No in-game visual test performed. After F6, verify no repair label, icon or frame wing remains at the cauldron, while hand crafting, or when equipment is fully repaired. Verify the extension appears at a suitable station with eligible damaged equipment and disappears after the last repair.

## Station-specific repair extension, 2026-10-19

- Crafting repair visibility and click handling now explicitly require a current station with `m_canRepair`, in addition to the native button state. Unsupported stations and hand crafting show no repair extension; native interactability still controls whether damaged items can be repaired. Hidden extensions clear their hover state and disappear from the continuous frame contour.
- `./dev.sh deploy` passed with zero warnings/errors; `git diff --check` passed. No in-game test performed. After F6, switch between cauldron, hand crafting and a repair-capable workbench/forge, checking the extension and its frame join.

## Compact crafting requirements, 2026-10-19

- Removed the three compact recipe stat cards and their update code. Reduced requirement tiles from 86 to 60 layout units, icons from 32 to 24, and quantity text from 17 to 14. Material names, availability colors and have/need quantities remain visible.
- Reallocated the freed space to the grouped item description, increasing its viewport by 70 layout units without changing panel size or craft controls.
- `./dev.sh deploy` built with zero warnings/errors and deployed to the isolated development game. `git diff --check` passed. No in-game visual test performed; after F6, check four-material recipes, long material names, scrolling descriptions and upgrade requirements.

## Expanded crafting recipe details, 2026-10-19

- Recipe descriptions now include the native crafting tooltip grouped into physical damage, elemental damage, blocking/protection, attack/resource use, nourishment, equipment and effects. Only populated sections appear. Flavor is italic, headings muted, primary values bone and skill ranges ash; food values use HUD colors. Existing stat cards and materials remain in place, with detailed text using the existing description scroll area.
- Values come from `ItemDrop.ItemData.GetTooltip` at the selected craft/upgrade quality and world level, with the selected output amount. Grouping occurs before localization using native tokens; special-effect blocks and unfamiliar/modded lines remain present. No gameplay values are altered.
- Debug build/deploy and `./dev.sh test` passed, including new checks for native damage ranges, grouping, effect continuation, food sections and empty recipes. No in-game visual/input test performed. After F6, inspect a mixed-damage weapon, shield, food and upgraded/set item; scroll the description and verify material/craft controls remain accessible.

## Arch Linux office setup, 2026-10-19

- Native Linux Valheim from Flatpak Steam, installed build 25527674 (older than the
  Windows verification build below), copied into `~/RagnarokDev/Valheim` with isolated
  `~/RagnarokDev/saves`. The ordinary Steam game was not modified.
- Local SDK 10.0.402; SHA-256-verified BepInExPack 5.4.2351 and ScriptEngine r11.2.
- Linux setup/build/deploy/watch/play/logs/test/package/doctor/smoke commands added.
  Flatpak launches receive temporary access to the development game and saves.
- An offline local-save guard disables Cloud support in the copied assembly before
  game startup; the launcher refuses to run without it. Original game assemblies
  and account-wide Steam Cloud settings are unaffected by this guard.
- Debug and Release builds passed with zero warnings/errors. Regression suite passed:
  45,562 allocation assertions, death-penalty checks and migration/wire-name checks.
- Release archive built with only our plugin and installation instructions.
- OpenGL batch-mode startup/reload check passed: plugin loads, inventory/station/XP
  runtime checks pass on both loads, and the previous instance unloads.
- Vulkan batch mode produced a native SIGSEGV in UnityPlayer during
  `QualitySettings.set_vSyncCount`; systemd's core record and the Unity log agree.
  No OOM evidence was found. Native Unity frames lack symbols; the exact engine or
  driver defect is unconfirmed. Local `gameArgs = ["-force-glcore"]` avoids that
  failure in the tested batch run. No world was loaded and no test saves were created.
- No interactive world, HUD visual, keyboard F6/F7/F10 or multiplayer test was
  performed. Windows-only `scripts/check-config.ps1` was not run here.
- During validation before the Cloud guard was added, Valheim enumerated Cloud
  saves despite `-savedir` and deleted four `_main.1` files from the older `Testt`
  revision as orphan cleanup. The current `_main.3` revision remains present.
  The post-cleanup Steam save cache was copied to `~/RagnarokDev/recovery`; this is
  a recovery snapshot, not a backup of the deleted revision. Subsequent guarded
  validation confirmed all 68 remaining Cloud save-file checksums were unchanged;
  there were no logged errors, and the smoke game process was stopped afterward.
- See [LINUX.md](LINUX.md) for setup and everyday commands.

## Windows setup, 2026-10-17

Verified locally on 2026-10-17 against Steam Valheim build 25909357.

Version 0.2.0 nearby-chest verification:

- 45,562 allocation assertions, including 5,000 randomized multi-inventory cases:
  player-first ordering, split costs, conservation, no partial withdrawal on shortage.
- Debug-only runtime checks against real Valheim Inventory instances: split spending,
  stack depletion, change/save callbacks, item quality and world-level filtering,
  single-item removal and unchanged stock on a failed withdrawal.
- All Harmony patches installed, and inventory checks passed on initial load and
  after ScriptEngine reload in the real game runtime.
- Actual recipe/build/station interactions, ward/ownership behavior and two-peer
  concurrency remain manual acceptance checks in [NEARBY-CHESTS.md](NEARBY-CHESTS.md).

- Setup script: completed successfully using pinned archive checksums.
- Debug and Release: zero build warnings and errors.
- Development deployment: DLL and symbols copied to BepInEx/scripts.
- Source watcher: a C# edit triggered a rebuild and deployment.
- Runtime smoke test: Valheim loaded the plugin through ScriptEngine; a file-change
  trigger unloaded the old instance and loaded a new instance. No logged errors in
  the successful test.
- Local sharing archive: contains only our plugin plus installation instructions.
- Game process and source watcher used for testing were stopped afterward.

Run the bounded runtime check with Steam open and Valheim closed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

This launches the isolated game hidden in batch mode, temporarily enables
ScriptEngine's file watcher, verifies initial load and reload, stops only that
process and restores the tool configurations. It does not load a world. The debug
flag `-ragnarok-inventory-checks` runs isolated inventory checks on each plugin load;
these checks are absent from Release builds.

The smoke test uses the same reload method as F6 through ScriptEngine's file
watcher. Actual F6/F7/F8 keyboard interaction and badge appearance still need a
manual check in the visible game. Multiplayer is not tested.


## Module refactor, 2026-10-18

- Source reorganized into HUD, UI shared/windows, Features, Configuration, Networking and Development; earlier HUD changes retained.
- Debug and Release builds passed with zero warnings/errors.
- Regression suite passed: 45,562 allocation assertions, death-penalty boundaries, and configuration migration/legacy-wire-name checks.
- `scripts/check-config.ps1` passed against the installed BepInEx reader using temporary files: custom values, repeat migration, new-format precedence, unknown entries and original-backup preservation.
- Fixed partial-startup cleanup, dedicated-server equipment initialization, server-only config packet acceptance/RPC cleanup, atomic file-watcher notifications and owned UI texture/material cleanup.
- No in-game or multiplayer test was performed for this refactor. The development game was running and was not interrupted. Reload with F6 after deployment; inspect the new settings groups and verify host/client locking and unload behavior.
- See [ARCHITECTURE.md](ARCHITECTURE.md) for module ownership and configuration compatibility.


## Character inventory dashboard, 2026-10-19

- Debug/Release builds: zero warnings/errors; Debug deployed to isolated scripts.
- Existing allocation/death/config regression suite passed (45,562 allocation checks).
- Numeric bounds check: 738 cell-size/row-count/quiver combinations fit the unchanged side frame, with unique gear cells.
- Set count and status APIs checked against the installed game assemblies. No gameplay patches or network changes added.
- No in-game dashboard/set/scroll/input test performed. F6 reload and partial/full/mixed-set, resource, gear/provision/quiver and unload checks remain manual.

## Vanilla inventory adaptation, 2026-10-19

- Debug/Release builds passed without warnings/errors; Debug deployed to isolated scripts.
- Existing allocation/death/configuration suite passed, including 45,562 allocation checks.
- Numeric native side-panel/grid-height bounds checks passed 522 slot-size/row-count/quiver combinations.
- Installed native InventoryGui/InventoryGrid/UITooltip APIs and UI prefab anchors were inspected read-only.
- No in-game or multiplayer test performed. The running game was not interrupted. F6 and Inventory Style=Vanilla: check header, category spotlight/scoped Quick Stack, Name/Kind/Weight sorting, key colors, chest take/place/scroll, side-board alignment, provision/quiver actions, stats/set hover, screen fit and style/unload restoration.

## Guide 02 messages/tooltips/level-ups, 2026-10-19

- Debug and Release builds passed without warnings/errors; Debug deployed to isolated scripts.
- Existing suite passed: 45,562 allocation checks, death-penalty boundaries and configuration migration/legacy wire-name checks. These do not exercise the new visual UI or its local placement migration.
- Inspected installed MessageHud/Skills/Character/Player/ItemDrop/HitData source paths and native tooltip formats; no game code/assets copied into the project.
- Reviewed ownership, native discovery-mask restoration, message-log preservation, hidden-HUD fallback, popup clipping/anchoring and readonly comparison/progression paths. Diff whitespace check passed.
- No in-game/visual/audio/controller/multiplayer test performed; the running game was not interrupted. F6 then Test notifications and real pickup/unlock/status, death/discovery/day/power, ordinary/tenth skill levels, hover delay, quality stars, Shift comparison, screen edges, custom placements, HUD hiding and vanilla/style/unload checks remain manual.

## Popup console previews, 2026-10-19

- Debug/Release builds passed with zero warnings/errors; Debug deployed to isolated scripts. Diff whitespace check passed.
- Installed Terminal command registration, autocomplete and command dictionary APIs inspected read-only. UI-only registration needs no cheats/network/server authority; unload cancels queued samples and restores any displaced registration.
- No in-game test performed. After F6, check `ragnarok_popup` help/tab completion, individual samples and `all`, console-close playback/pause, `clear`, style/world guards and F6 unload/re-registration. Previews do not test real event interception, discovery audio or actual progression.
- Follow-up to reported pickup-only playback: no popup exception found in the running game's log. Added per-sample progress/next-sample reporting in the console and Unity log, explicit cadence/pause instructions, failure reporting, and preservation of the queue while the HUD is hidden or unavailable. Leaving/changing player sessions still cancels playback. Debug/Release builds passed; Debug deployed. No in-game reproduction or confirmation of the reported stop yet.
- User subsequently confirmed `all` works; the sequence had not stopped.

## Notification glass opacity, 2026-10-19

- Small notification cards now use 58% background opacity instead of 96%; wet uses a darker, muted blue tint. Card outlines are 45%, icon wells 40% and icon rims 65%; text/icon opacity is retained.
- Debug/Release builds passed; Debug deployed to isolated scripts. Diff whitespace check passed. No in-game visual test performed; after F6 compare `ragnarok_popup pickup` and `ragnarok_popup wet` over bright/dark scenery.

## Failure preview text, 2026-10-19

- Replaced the unresolved `$msg_nostamina` preview token with explicit sample text, “Not enough stamina”, and an explicit failure color flag. Preview no longer depends on a nonexistent localization entry to select its red styling; real message routing is preserved.
- Debug/Release builds passed; Debug deployed. Diff whitespace check passed. No in-game test performed; after F6 check `ragnarok_popup failure` displays readable red text.

## Skill pill reference revision, 2026-10-19

- Ordinary skill gains now use a 52-high capsule, gold circular up-arrow badge, muted 14-point caption and 18-point Cinzel skill/level line. Width follows measured text up to 560 units; long labels auto-size and the whole pill scales down to fit narrower screens. Smoked glass retains 58% opacity. Procedural sprites belong to Theme and unload with it; native skill icons no longer replace the arrow.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game visual test performed; F6 then `ragnarok_popup skill`, real short/long skill names, localization, placement and small-screen fit remain manual.
- User screenshot showed excess space after short skill names. Removed the 300-unit minimum width, increased the caption/name gap from 12 to 20 units and right-aligned the name/level with 20-unit end padding. Debug/Release builds passed; Debug deployed. In-game visual confirmation remains pending.

## Death banner diamond, 2026-10-19

- Removed the death-only diamond suppression. Death now shows a crimson (#D64A3E) diamond; other banners reset it to gold.
- Debug/Release builds passed; Debug deployed. Diff whitespace check passed. No in-game visual test performed; F6 then `ragnarok_popup death` verifies presentation without killing the player.

## Shared HUD glass, 2026-10-19

- Applied the pickup notification's 58% smoked glass to Forsaken power circle/key, eaten food backgrounds, status tiles, provision cells and hotbar wells/badge/key. Wet status tiles share the muted notification blue; freezing is dark red glass. Softer neutral hotbar/status outlines; hotbar cooldown overlay reduced to 35%.
- Removed opacity stacking: food silhouette backing now uses a ring instead of a solid disc; equipped/breaking hotbar wells replace the base fill. Text, icons, timers and state/duration/durability rings retain their contrast. Empty food/slot rules remain intact.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game visual/input test performed. After F6 check bright/dark scenery, active/inactive power, eaten/empty food, wet/freezing/rested, equipped/breaking/cooldown hotbar slots, mouse/gamepad selection and provision cells.

## Health regeneration readout, 2026-10-19

- Added a compact outlined HP/second readout inside the health track's left edge. Uses averaged food healing (native 10-second ticks), current SEMan health-regeneration modifiers, positive SE_Stats periodic healing and remaining healing-over-time ticks. One-shot healing and damage are excluded. Full health displays muted `0/s`; otherwise positive rates use `+0.##/s`. Follows HUD numeric visibility and scale, with owned text destroyed alongside the HUD root.
- Inspected installed Player.UpdateFood, Character.Heal, SEMan.ModifyHealthRegen and SE_Stats tick/healing implementations read-only. No patches, gameplay writes or network changes added.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game test performed: verify no-food, full/damaged health, different foods, rested/resting/wet modifiers, healing mead expiry, bar readability/scale and numeric visibility after F6. Arbitrary healing behavior from other mods outside these native APIs is not estimated.

## Minimap glass and HUD backdrop removal, 2026-10-19

- Small Ragnarök map image color alpha set to 58% and restored on style switch/unload. Procedural rim/medallion fills use shared glass; map key uses glass with a muted border. Added world outlines/shadows to biome, cardinal, map key, day/clock and weather labels; next-weather text stays at 85% opacity. Pins/markers and large map are not retinted.
- Removed HudBackdrop and all owned backdrop creation/update paths from vitals, compass, key guide, notifications/skill pill and minimap information strips. Legacy keys remain bound but ignored/hidden; inventory panel opacity remains exposed. Added configuration regression coverage for removed controls versus placement/inventory controls.
- Fixed minimap info replacement cleanup and reset displayed-text caches on rebuild; added biome-mask cleanup for same-frame reload. Owned label silhouettes disappear with their HUD roots; original map color and native visibility restore.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Existing suite passed (45,562 allocation checks, death boundaries, configuration migration/wire names and backdrop visibility checks). Diff whitespace check passed.
- No in-game visual/controller test performed. F6: verify map transparency with the installed map shader, label contrast, pins/markers, map open/close and zoom, minimap info toggling, vanilla/style/unload restoration, absence of old HUD Shade/Plate layers and settings controls, and retained inventory panel opacity.

## Weapon radial reference styling, 2026-10-19

- Rune & Iron wheel segments and center use shared 58% glass with muted neutral edges and gold selected state. Removed its broad shadow wash, reduced selection aura to 8%, and outlined center/slot labels. Valheim item icons and existing input/equip logic remain in use.
- Added a compact type/quality/in-hand header, Cinzel item title, quality-scaled native base damage summary (including elemental/tool damage), shield base block armor, and an explicitly labeled durability percentage. Per-item durability bars turn moss/yellow/red, with hotbar key and stack count beneath each icon. Text auto-sizes/wraps in the existing center geometry; details refresh every 0.2s while open.
- Read installed ItemData damage/block/durability and Skills APIs. Values are base item statistics, not predicted skill/buff-adjusted hit damage. No new gameplay patches, network settings or dependencies. Label silhouettes belong to the dial canvas and generated sprites to its existing owned-art collection.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game visual/input test performed: after F6 hold the configured dial key (default V); verify varied item counts, selected/equipped/broken items, stacks/keys, long names, physical/elemental/tool damage and shields, vanilla style, mouse/right-stick cancel/equip, and unload.

## Skill popup vertical placement, 2026-10-19

- Replaced the default equipped-hotbar-card docking target with the active Ragnarök vitals panel. The pill's bottom sits 12 scaled HUD units above that panel's top; screen clamping and narrow-screen scaling apply. The unchanged default placement tuple (BottomCenter, 0, 228) opts into docking, preserving custom placements and a fixed fallback when the Ragnarök HUD is absent. The live panel reference clears on owned-root disposal.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game visual test performed: after F6 use `ragnarok_popup skill`, check no overlap with weapon/health labels, move/scale vitals and verify the pill follows, test custom placement and HUD style/unload restoration.

## Inventory glass, 2026-10-19

- Rune & Iron inventory, chest and character side-panel opacity defaults to shared 58% glass. Layout migration 6 updates the old 95% default once while preserving customized opacity. The existing inventory opacity control remains available.
- Reduced nested slot, stat-card, set-band and footer-button fills rather than stacking near-opaque surfaces over the translucent panels. Empty slots remain muted, and occupied/hover/equipped/breaking distinctions, item icons, numbers and durability stay visible. Native wood-style panels are unchanged.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Existing allocation/death/config-schema suite passed; it does not exercise the runtime layout-version opacity migration. Diff whitespace check passed.
- No in-game visual/input test performed. After F6 check inventory/chest/side-panel blending over bright/dark scenery, set bands, empty/occupied/selected/breaking slots, opacity slider, F7 config reload, input/drag handling and vanilla switching. Confirm old-default migration and customized-opacity retention in-game.

## Lighter inventory and character HUD overlap, 2026-10-19

- Reduced the inventory/chest/side-panel default opacity from 58% to 42%. Local layout migration 7 carries the previous default forward while leaving other custom values intact.
- Hide the character HUD (portrait, stats, food, provisions and party) and status strip whenever the native inventory visibility guard is true. Both return on close; preview mode remains visible. Hidden character portrait rendering/refresh is skipped, and key-guide avoidance no longer reserves space for the hidden status strip.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game test performed; F6 then check open/close for inventory and chest, portrait/food/status restoration, opacity migration/slider, previews and HUD style switching.

## Stronger inventory opacity, 2026-10-19

- Raised inventory/chest/character side-panel default opacity to 72% after the 42% pass proved too translucent. Layout migration 8 updates the previous default; custom values remain. Character HUD hiding while inventory is open is retained.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Diff whitespace check passed. No in-game visual test performed; F6 then compare panel contrast over bright/dark scenery.

## Crafting interface implementation, 2026-10-19

- Implemented the approved smoked-glass study without Pin, retaining native station/item icons, crafting/upgrading/repair/style/quantity/progress controls and existing nearby-storage authority/consumption. Native recipe-pair selection and gamepad indices remain aligned after filtering/sorting.
- Added station-specific labeled categories, shields/meads separation, armor-slot subfilters, independent craftable-only, stable name/craftable-first sorting, 150ms-debounced multi-word name/ingredient search, clear/empty-result reset, station/world context reset and slash focus. Vanilla keeps native sorting/art and gains ingredient search and new category glyphs.
- Selected recipe shows flavor, native base-stat cards, full native details on icon hover, station level, alternative-ingredient labeling, green/red have/need tiles and accurate readiness/progress states. More than four ingredients retain paging. Inventory food/gear categorization includes mead bases/shields after the crafting category split.
- Installed InventoryGui selection/list/update/crafting and ItemData stats APIs inspected read-only. UI prefab inspection confirms a 570×650 native crafting panel. Numeric bounds checks passed for description/stat/material separation, footer and style/quantity/craft widths. Game assets were not copied into the repository.
- Debug/Release builds passed without warnings/errors; Debug deployed to isolated scripts. Offline tests passed: 45,562 allocation checks, death/config checks, and new multi-word name/ingredient search, missing-term/partial/Unicode matching, food dominance/ties and mead classification checks. These tests do not exercise native recipe interception, station switching or UI input. Diff whitespace check passed.
- No in-game visual/input/multiplayer test performed. After F6 check Workbench/Forge/Cauldron/by-hand/upgrader contexts, armor slots, health/stamina/eitr/meads/bases, query clearing and station changes, sorting, empty results, gamepad navigation/scrolling, craft/upgrade/repair/style/quantity, alternative materials, nearby/ward/ownership counts, long labels, tooltip bounds, progress, vanilla switching and F6 unload/reload restoration.

## Crafting repair accessibility, 2026-10-19

- Replaced the 32×32 icon-only repair control with a 104×40 icon-and-label button. Shifted the Craft/Upgrade capsule left, retaining station name auto-sizing and clear header bounds. Gold glass/edge and hover feedback indicate repair availability; muted styling and tooltip explain when nothing is repairable.
- Uses the original guarded repair click; no repair-all behavior, new keybinding or gameplay changes. Debug/Release builds passed; Debug deployed. Native 570-unit header bounds and diff whitespace checks passed. No in-game visual/input test performed; F6 then check damaged gear, no repairable gear and long station names.

## Detached left repair button, 2026-10-19

- Moved the styled 104×40 Repair button outside the crafting panel's left edge, with a 10-unit gap and center 158 units below the panel top (matching the inspected native repair-button height). Craft/Upgrade returns to the header's right side; station name space expands.
- Inventory fitting now includes the detached button's bounds while visible, avoiding overlap with the inventory/character panel. The static bounds reference clears on restoration/unload. Existing repair availability/hover/tooltip/click behavior is retained.
- Debug/Release builds passed; Debug deployed. Native repair prefab placement inspected read-only; bounds and diff whitespace checks passed. No in-game visual/input test performed: F6 then check left-edge placement, inventory fitting, repair ready/disabled states, chest hiding and vanilla/unload restoration.

## Integrated repair contour and stronger shared glass, 2026-10-19

- Repair now occupies a flush 104×40 extension of the crafting panel, replacing the detached button plate. A single generated mesh silhouette and its matching contour cover the body and extension, eliminating the joining border and duplicated background opacity. Rounded outer corners, subtle edge smoothing and label/icon hover feedback remain; no separate gold button fill.
- Native repair visibility toggles the extension; blank space above/below it passes pointer hits through. Inventory fitting tracks its real bounds. Owned mesh graphics disappear with the panel root; no textures, materials, subscriptions or gameplay changes were added.
- Raised shared inventory/chest/character/crafting opacity to 85%. Local layout migration 9 carries the prior 72% default forward; unrelated custom opacity is preserved.
- Debug/Release builds passed with zero warnings/errors; Debug deployed. Existing allocation/death/config/search tests and new continuous-silhouette, inset-containment, join-coverage and hidden-extension bounds tests passed. Diff whitespace check passed.
- No in-game visual/input test performed. After F6 verify a seamless repair join on bright scenery, matching crafting/inventory/chest/side-panel opacity, ready/disabled/hidden repair states, adjacent inventory clicking, chest hiding, resizing and vanilla/unload restoration.
- Crafting alongside open chests (2026-10-19): removed `ChestFocus` and its lifecycle hooks, so opening a chest no longer hides or disables crafting. The key guide continues docking under the visible crafting panel. `./dev.sh deploy` passed with zero warnings/errors and deployed to the isolated development game. `git diff --check` passed. No in-game test performed; after F6, check crafting visibility/input while opening and closing a chest.
