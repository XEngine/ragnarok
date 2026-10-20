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

## Craft search shortcut and footer keys, 2026-10-20

- Search focus now accepts the typed `/` character as well as Unity's Slash key, supporting keyboard layouts such as Turkish Q (Shift+7). Explicitly selects the search field before activation; typing, modal, console, menu and chat guards remain in place.
- Crafting footer keys use the inventory guide's white bold styling, font size and muted action color. The close hint follows the current inventory binding.
- Windows Debug build passed with zero warnings/errors and deployed to the isolated development game's BepInEx/scripts folder. No in-game test performed. After F6, open crafting and press `/` using the active keyboard layout, then verify search focus/filtering, existing query preservation, normal slash entry while already typing, white footer keys and a rebound inventory close hint.

## Crafting material source breakdown, 2026-10-20

- Material tiles retain the combined available / required count and now show carried versus eligible nearby-chest stock underneath. Hovering a tile shows bag, chest, total and required counts; nearby crafting disabled uses bag-only availability.
- Display uses the existing read-only resource scope and matching CountItems arguments (quality -1, matchWorldLevel true, confirmed against the installed game assembly). Combined availability and red/green readiness retain the existing counting path. No withdrawal, range, permission, synchronization or ownership rules changed.
- Tiles use 74 units of the existing material-row allocation, leaving a 3-unit gap before the readiness label at the native panel height. Tooltip ownership follows the tile lifecycle.
- Windows Debug build passed with zero warnings/errors and deployed to the isolated BepInEx/scripts folder. No in-game test performed. After F6, check bag-only, chest-only, combined stock, shortage, disabled nearby crafting, range/ward restrictions, changing stack amounts, multi-craft, upgrade costs, four-material recipes, large counts, tooltip placement and reload cleanup.

## Increased shared panel opacity, 2026-10-20

- Raised inventory/crafting background opacity from 85% to 95%; chest and character side panels share the setting. Layout migration 10 updates the former 85% default once while preserving custom values. The setting description now includes crafting.
- Windows Debug build passed with zero warnings/errors and deployed to the isolated BepInEx/scripts folder. Diff whitespace check passed. No in-game test performed; after F6 compare inventory/crafting over bright scenery, check shared chest/character opacity, and verify the opacity slider and custom-value retention.

## Collapsible crafting subfilter space, 2026-10-20

- Recipe count/sort, recipe viewport, divider and selected-recipe header move up 32 UI units when no armor subfilters are shown. Selecting Armor restores the subfilter row and expanded spacing. Recipe and description viewports grow into the reclaimed space; materials, actions and repair contour stay in place.
- Resized recipe scrolling is clamped and gamepad selection visibility is reevaluated. Layout references reset on panel restoration/rebuild.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game test performed; after F6 check All/Armor/other categories, armor slot selection, station switching, search/empty results, scrolling/gamepad selection and F6 restoration.

## Inventory categories above Quick Stack, 2026-10-20

- Moved the Ragnarok inventory category tabs below the grid and directly above the Quick Stack/sort row. Reduced the top header allocation by the same 40 units added below the grid, retaining panel height, slot size, footer and character-dashboard positions. Category selection and Quick Stack scope retain their existing handlers.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game test performed; after F6 check category clicks, scoped Quick Stack, grid interactions, header spacing and character panel alignment. Chest and vanilla layouts retain their existing category positions.

## Portrait skin binding validation, 2026-10-20

- Investigated repeated Unity "Bones do not match bindpose" errors reported during weapon changes. The portrait appearance signature refreshes after equipment renderer changes. Existing code copied inactive/disabled renderers, assigned meshes before binding and did not validate bind-pose counts or missing bone mappings. Logs contain no stack trace, so runtime causality still needs confirmation.
- Portrait capture now excludes hidden renderers while retaining skeleton transforms. Skinned copies stay disabled and meshless until bone counts and every required bone/root mapping validate; then bones, mesh, bounds and blend shapes are assigned. Incompatible portrait meshes are omitted with a named warning once per mesh per portrait instance. Live renderers, skeletons and meshes are never modified.
- Hair restoration uses the same validation. Read-only inspection of the installed VisEquipment assembly confirms attach_skin equipment uses body-model bones/root. Normal attach objects retain their own copied skeleton mapping. Temporary objects and render textures retain their existing cleanup.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game test performed. After F6, enable the portrait, swap weapon/shield/torch and armor, toggle ShowHelmet, check hair/beard and neutral framing, inspect named skipped-mesh warnings if present, and verify the Unity bind-pose error no longer recurs.

## Angled character portrait, 2026-10-20

- User confirmed the portrait binding fix resolved the reported error in game.
- Offset the portrait camera to (0.32, 0.16, 1.2) relative to its existing head focus, giving a subtle side view from above so the neutral face looks toward the lower right. Head framing, orthographic size, skeleton pose and binding validation are retained.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual test performed for this camera adjustment; after F6 check face angle/centering with helmet on/off and different equipment.

## Weapon radial opening hitch, 2026-10-20

- Investigated the reported first/idle opening stall. Show previously rasterized four discs plus a new fill/edge texture pair for each unseen item count, synchronously on the game thread. At the configured resolution the initial path produced about 3.4 million Color entries (roughly 52 MiB of temporary managed pixel arrays), plus texture uploads. Every close destroyed the entire UI, including many outlined TMP labels, and every open rebuilt it. This establishes expensive work on the reported path; no in-game timing capture was taken.
- Replaced dial-specific raster art with WeaponDialGraphic polar meshes for segments, borders, hub, halo and vanilla shadow. Geometry samples the same radial/side distance alpha rules and includes transparent edge fringes. No generated textures or per-count art cache remain.
- Closing now deactivates the canvas and clears held inventory references. Reopening with the same item count/style/fonts reuses UI, updates item references/icons and repaints current key, stack, equipment and durability data; animation/aim state resets. All slots own toggleable durability bars so changing item types with the same count remains valid. Count/style/font changes rebuild the bounded current UI. Dispose destroys the cached canvas before shared theme teardown. Input, equipment actions and multiplayer rules are unchanged.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Existing offline regression suite passed; it does not exercise Unity mesh rendering or radial performance. Diff whitespace check passed.
- No in-game performance/visual test performed. After F6 check first open, reopen after idle, all item counts 1-12, adding/removing/replacing equipment at the same count, stack/durability updates, both dial styles and FollowHud changes, mouse/gamepad selection, cancel/equip while running and repeated F6 cleanup. Compare ring/gap/border/shadow appearance and confirm the reported stall is gone; profile further if it remains.

## Stronger radial opacity, 2026-10-20

- Raised dial segment and center backgrounds to 90% opacity in both styles, including selected/equipped segment states. Ragnarok previously used shared 58% HUD glass; the dial now owns its opacity constant.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual test performed; after F6 check the radial against bright scenery and selected/equipped states.

## Combat key guide below hotbar, 2026-10-20

- When Valheim's combat hint group is active and a carried weapon is equipped, dock the guide below the visible Ragnarok action bar. Combat hints flow into centered lines at the bar's width/scale with white bold keys, white native input icons and muted action labels, matching inventory footer styling without keycap backgrounds.
- Native hint reading retains bindings, translations and gamepad glyphs. Building and other contexts retain their normal placement; crafting docking takes priority while the inventory is open. Disabled/custom-hidden action bars fall back to configured guide placement.
- Measure the rebuilt guide and raise the action bar only enough to fit it above the bottom edge; the saved bottom margin is unchanged and normal positioning resumes outside combat. The published action-bar reference clears on hide/dispose and guide objects retain HUD-owned cleanup.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual/input test performed; after F6 check weapon equip/unequip, bow/shield, hammer/building, inventory/crafting, mouse/gamepad bindings, long translations, hotbar scale/offsets, HUD toggles and reload restoration.

## Combat hint vertical alignment, 2026-10-20

- Compact hint keys now use TMP typographic midline alignment, matching labels rather than mixing geometric Center and MidlineLeft. Row children share the same height, and modifier joiners use the same compact 10.5-point size/alignment.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual test performed; after F6 compare key/label alignment and mouse/gamepad icons on single and wrapped combat hint rows.

## Matching crafting key-guide style, 2026-10-20

- The guide docked below the Ragnarok crafting panel now uses the shared compact combat-guide presentation: white bold keys, muted action labels, native input glyphs, matching typographic midline alignment and centered wrapping. Existing hint contents and crafting-panel docking are retained.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual test performed; after F6 inspect the guide below crafting, wrapping, modifier combinations, input glyphs, and switching between inventory/combat/building contexts.

## Tool key guide below hotbar, 2026-10-20

- Building-tool and fishing hint contexts now join weapon combat hints below the Ragnarok hotbar with the shared white key / muted label style and aligned text. Native hint groups determine tool context: read-only inspection of the installed game confirms GetCurrentWeapon excludes ItemType.Tool, so checking that accessor alone misses building tools.
- Crafting docking retains priority; native binding/glyph/translation reading and automatic room beneath the hotbar are reused. No input, equipment or gameplay rules changed.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual/input test performed; after F6 check hammer/hoe/cultivator, fishing, axes/pickaxes, weapon/tool switching, wrapped building hints, hotbar scale/offsets and inventory/crafting docking.

## Hotbar padding and smooth guide clearance, 2026-10-20

- Guide docking now measures from the visible slot bottom, reclaiming the action bar's transparent bottom padding. With default scale/margins, an 18-unit single-line guide fits in the existing 24-unit bottom margin plus 10-unit padding, without raising the bar.
- Additional clearance uses a per-frame guide-space request consumed by the action bar, with a 0.16-second SmoothDamp response on unscaled delta time. It eases up only by the missing space and eases back to the saved bottom margin when the guide is no longer docked. Requests and animation state are cleaned up on dispose.
- The guide follows the visible slot edge during motion instead of being clamped upward across the hotbar; overflowing lines slide into view as space opens. Craft-panel docking retains its on-screen clamp.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No in-game visual test performed; after F6 check single/wrapped hints, weapon/tool equip/unequip, inventory open/close, guide toggles, bottom margins/scales, smooth return and repeated reloads.

## Building, leveling and cultivation task card, 2026-10-20

- Added BuildTaskPanel, a Ragnarok-HUD-only presentation of the native selected building/terrain task. The 448-unit-wide card uses shared smoked glass, gold Cinzel title, Barlow tool/description labels, a 42-unit task icon, snapping indicator and up to three 38-unit material/station tiles per row. It renders at 85% of HUD scale and shrinks the text/icons from the native readout.
- Native Hud.UpdateBuild/SetupPieceInfo and InventoryGui.SetupRequirement were inspected read-only in the installed assembly. The card mirrors their localized text, icons, requirement visibility, amount/error colors and station results. No costs, availability calculations, resource withdrawal, authority or synchronized config changes were introduced; existing nearby-building scopes remain authoritative.
- Only individual native readouts are masked with reversible CanvasGroups. The build picker, selection, input and native update routines remain active. Switching HUD style, replacing the HUD or unloading restores original group alpha/raycast/interactable settings and removes owned groups/card objects. The card hides with inventory, modal UI and weapon radial; it retains native HUD visibility.
- Placement reads the current vitals/action-bar bounds after their update, keeps a gap above them and uses a compact description/requirement layout. If unusual HUD positions/scales would put the card in the reticle band, it moves left of center instead. At default 1080p/HUD scale, the no-material card is about 77 units high; a six-requirement card about 150, staying well below the central crosshair.
- Windows Debug and Release builds passed with zero warnings/errors. Existing offline regression suite passed; it does not exercise this Unity presentation. Debug deployed to isolated BepInEx/scripts and diff whitespace check passed.
- No in-game test performed. After F6 check hammer building, hoe leveling/raising/pathing, cultivator/planting, repair/empty selections, no-cost mode, sufficient/missing materials, nearby chests, required/missing stations, native picker hover/selection, snapping, long/modded descriptions and requirements, HUD scales/positions, hotbar animation, crosshair clearance, HUD style switching and repeated F6 restoration.

## Building card tint and native backdrop correction, 2026-10-20

- User's in-game screenshot exposed a white task-card surface and the native black build-info backdrop still visible behind the vitals. RoundedPanel is a white alpha mask: the card incorrectly supplied a white tint. Both initial creation and live opacity refresh now use explicit charcoal #0C0F11.
- The task readout masks hid only text/icons/requirements. Decorative Images in the native build HUD are now separately disabled and their original enabled states restored on vanilla switching/unload. BuildUi and legacy piece-picker subtrees are excluded, as are the source task/snapping icons and requirement subtrees whose state/color the card mirrors. Only graphic components are hidden; their objects, child controls and native updates remain active.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No new in-game test performed; after F6 confirm charcoal card, no leftover black backdrop, native build-picker visibility/input, requirement colors and vanilla/reload restoration.

## Content-sized building task card, 2026-10-20

- Replaced the fixed card width and three-column material row with measured content sizing, bounded to 200-448 UI units. Title/tool text, a bounded preferred description width and the widest requirement row determine the card width. Each material/station tile measures its own name/amount and wraps when the card is full.
- Description height adapts to its wrapped text (up to 42 units); no-material tasks omit the material area. Hidden task/snapping icons stop reserving unnecessary horizontal room. Placement/crosshair clearance now uses the actual card dimensions.
- Text measurements/layout run only when visible contents change; resource color flashes and animated hotbar/vitals movement do not trigger repeated measurement.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Diff whitespace check passed. No new in-game test performed; after F6 compare Wood Stairs/two requirements, terrain/no materials, long task/description translations, long material/station names, changing costs and multirow requirements.

## Slot-aware tombstone reclaim, 2026-10-20

- Root cause: re-equipping happened after normal grid transfers, so armor was left in the grave once bag cells filled. Native EasyFitInInventory also counts all grave stacks against bag capacity, ignoring vacant fixed character/provision slots.
- Before an approved own-grave MoveAll, restore valid owner-marked equipment directly to empty, enabled, type-correct character/extra-utility slots with native positioned inventory transfers. Remaining items use the original MoveAll and keep its preferred grid/hotbar positions. Equip retries settle recovered gear into character slots after transfer, without equip timers; extra-utility effects synchronize even when the item already occupies its restored slot.
- Existing valid equipment marks are supported. New deaths also record provision-slot positions in custom data; retained items have their death tags cleared. Occupied slots are not overwritten by direct restoration and real overflow remains in the grave.
- Added a read-only fit planner matching the installed MoveAll's preferred-position pass, partial merges, generic quality/world-level stack rules and free-cell fallback. Native Interact can select direct TakeAll on Use when fixed slots make everything fit. Current total carry weight remains checked. Planner inputs are snapshots; it never writes a grave or performs ownership changes.
- Authority: changes use the existing synchronized AutoEquipOnReclaim, CharacterSlots and ExtraUtilitySlots settings. Native grave ownership/access checks, TakeAll permission RPC and callbacks remain active. Direct slot writes additionally require the source to be the local player's own grave with a valid locally owned ZNetView. No new RPC, ownership claim or network handler was added. The existing unload reset and instance Harmony owner remove pending work and patches.
- Native positioned AddItem still rejects x beyond inventory width even with its y-validity bypass; special positions outside that width therefore retain normal transfer/equip fallback rather than being falsely counted as direct recovery space.
- Offline checks passed for full bag/empty worn slots, conflicting and duplicate slots, real overflow, preferred-position semantics versus optimistic repacking, partial stacks, quality/world-level separation, provision stacking, unmarked items, malformed/foreign owner tags and 1,000 randomized reservations without input mutation. Existing regression suite passed. Debug/Release builds passed with zero warnings/errors; Debug deployed to isolated scripts. Diff whitespace check passed.
- No in-game test performed. After F6, first retry the existing own grave with marked armor left inside. Then test a fresh death with full bag, armor, extra utilities and provisions: E direct reclaim, manual Take All, new occupied slots, overflow, overweight versus belt bonuses, restricted equip states, world death retention settings, server-disabled recovery/slots, another player's grave, remote ownership/in-use denial, persistence/reconnect and repeated F6. Inventory simulation tests do not exercise native Unity transfer callbacks, network races or mixed-mod behavior.

## Hammer build picker theme, 2026-10-20

- Added BuildMenuSkin over the installed game's BuildUi picker, including pooled piece/tag controls, list tabs, search, scrollbars and favorites controls. Dark charcoal rounded panels use inventory opacity; muted borders turn gold for native selected/hovered pieces, selected tabs and toggled tags. Labels use the shared fonts and bone/gold palette.
- Native piece artwork, upgrade/favorite/check indicators and resource availability tint remain untouched. Transparent favorites close blockers and clipping masks retain their graphics. No selection handlers, search text, favorites data, navigation, resource checks or gameplay authority changes were introduced. No new dependencies or outside assets.
- New controls are discovered on opening and periodically while open. Original image, font, selectable and tag-color state is saved once and restored on HUD replacement, Vanilla style or unload. Owned non-raycast borders are removed; no event subscriptions or patches are added.
- Windows Debug and Release builds passed with zero warnings/errors. Existing offline regression checks passed; these do not exercise Unity picker rendering/input. Debug deployed to isolated BepInEx/scripts; diff whitespace check passed.
- No in-game test performed. After F6 open the hammer picker and check tab/tag changes, search typing/clearing, piece hover/selection, missing resources, scrollbars, favorites dropdown/checks/closing, controller navigation, hoe/cultivator pickers, localized labels, HUD style switching and repeated F6 restoration. Native layout dimensions are retained in this first styling pass.

## Build picker visual rework, 2026-10-20

- Reworked the first-pass picker after the user's screenshot: plain white bold native key labels without keycap art; gold tab underlines and slim category selection markers replace repeated boxed outlines. Body labels are smaller/regular, with strong tab labels and category text inset. Piece artwork is inset to 86% of its original scale within the existing hit cells.
- Native control surfaces now render through inset owned faces, with quieter fills and more separation between tiles. Original images remain transparent raycast targets rather than being disabled, preserving pointer hit areas. Native selectable target graphics are restored before owned faces are destroyed on unload/style changes.
- Scrollbars use a narrow 3-unit rail and 5-unit visible thumb inside the native drag geometry. Existing scroll sensitivity, navigation, handle sizing, masking, search and favorites callbacks remain native. Main surface uses configured inventory opacity; secondary surfaces use a light 14% charcoal layer to avoid heavy nested black panels. Owned graphics do not intercept raycasts.
- Original icon scales, text styles/margins/alignment, images and selectable targets are restored on unload. No gameplay, authority, synchronization, bindings or favorites data changes.
- Windows Debug and Release builds passed with zero warnings/errors; existing offline regression checks passed. Debug deployed to isolated scripts. Diff whitespace check passed. No in-game test performed; check F6, category/tab state, live key bindings/gamepad glyphs, search, favorites, piece selection and resource tint, scrollbar dragging/wheel navigation, style switching and repeated reloads.

## Build grid density and final native decoration removal, 2026-10-20

- Added local persistent HUD.BuildMenu.Columns (4-16, default 12) and Rows (3-10, default 6), exposed through four minus/plus buttons below the native piece viewport and through the existing settings UI. Bounds disable the appropriate buttons. Viewport clearance reserves 34 units for the footer; fixed-column cells and six-unit spacing fit the requested rows/columns. Native layout and explicit navigation are rebuilt after a density or viewport size change, including tab/tag width changes.
- This is presentation only: no resource costs, available piece lists, favorites data, input bindings, server authority, synchronized settings or building operations are changed. Icons retain native availability tint and now preserve aspect ratio when cells change shape. Native content remains scrollable for pieces beyond the visible grid.
- Piece wells and borders now use the same HudActionSlot/HudActionEdge sprites and charcoal/hover colors as inventory slots. Main menu gets a PackEdge frame; the tab row gets a quiet straight rule. Native thin line/border/divider artwork is removed, and the exact BuildUi.m_materialSeparator is hidden with its layout footprint ignored, preserving native reordering/activation. This addresses the user's material-category separator crop directly.
- Scroll rail/thumb use the HUD bar capsule sprite with smaller sliced borders for rounded ends at narrow widths. Native scrollbar handle geometry and raycast targets remain intact.
- Owned footer controls are excluded from the native-control skinner and their listeners are removed on disposal. Grid size/spacing/constraint/axis, viewport bottom inset, material separator layout state, icon aspect/scales, graphics and navigation are restored on unload or Vanilla style. Existing layout elements are preserved; only the added separator layout element is destroyed.
- Windows Debug/Release builds passed with zero warnings/errors. Existing offline regression checks passed; they do not exercise the Unity picker or native prefab layout. Debug deployed to isolated scripts. Diff whitespace check passed.
- No in-game test performed. After F6 check visible default 12 columns and 6 rows, both footer controls at minimum/maximum values, persistence across F6/restart, grid/category scrolling, tab changes and hidden category list widths, search/favorites, missing-resource icon colors, pointer/gamepad selection after resizing, material separator removal, outer/tab borders, rounded scroll ends, HUD scales/resolutions, Vanilla switching and repeated reload cleanup.

## Build grid footer overlap correction, 2026-10-20

- The user's screenshot showed the density footer drawn over the last visible piece row. The original change reserved space on ScrollRect.viewport, whose offsets can be driven by native scrollbar layout, and assumed that viewport itself supplied clipping.
- Reserve the footer on the outer ScrollRect frame instead, and create the footer as its sibling using the same horizontal anchors/offsets. Native viewport/scrollbar layout now runs inside the smaller list area. Footer bounds follow category-list width changes.
- Add an owned RectMask2D when the actual viewport has no local mask, so list items cannot render into the footer region. The footer has its own charcoal surface and top rule. Row sizing uses the resulting clipped viewport after native canvas layout.
- Unload removes the added mask/footer/listeners and restores the original outer-frame inset and grid layout. No native mask is altered or removed.
- Windows Debug build passed with zero warnings/errors; Debug deployed to isolated scripts. Diff whitespace check passed. No in-game test performed; after F6 verify the last complete row ends above the controls, scrolling cannot render pieces behind the footer, row/column changes, category visibility/tab changes, scrollbar appearance and repeated reload/Vanilla restoration.

## Inventory/crafting TAB hitch optimization, 2026-10-20

- Code inspection found CraftPanel.RebuildRows destroying/recreating all recipe UI objects whenever the logical recipe/availability signature changed. Replaced this with a visible row pool bounded by ceil(viewport height / 48) + 2. Full native recipe data/content height remain intact; scrolling rebinds pool slots to logical recipe indices. Click closures read the current bound row rather than capturing a stale recipe. Inactive pooled views drop item references; quality changes update upgrade labels.
- Gamepad selection scrolls to the selected logical recipe before the visible pool is bound, so selection can move outside the currently rendered window. Availability-only changes keep scroll position; recipe/filter/order changes reset it. The pool is retained across ordinary closes and owned by the existing panel root for unload/rebuild cleanup.
- The crafting panel's initial build used to immediately request another native UpdateCraftingPanel after inventory opening had already built/filtered/sorted the recipe list. The recipe-list postfix now records its owner and whether Ragnarök sorting was applied; panel construction requests a refresh only if that state is missing. Reset clears the tracked native reference. Native filtering, availability, recipe indices, costs and crafting/repair controls remain authoritative.
- MonochromeIcons previously allocated/blitted an entire native texture atlas for every uncached small glyph. It now crops the sprite region directly into a render target bounded to 128 by 128 before readback/conversion. Existing per-source sprite caching and disposal remain active, with native-color fallback on conversion errors. No shaders, dependencies, external assets or gameplay/network changes.
- Windows Debug and Release builds passed with zero warnings/errors. Existing offline regression checks passed, covering crafting search/detail formatting and the rest of the regression suite; these checks do not execute Unity pooling, GPU readback or measure actual frame times. Debug deployed to isolated scripts and diff whitespace check passed.
- No in-game performance or interaction test performed, so the full one-second hitch cannot yet be attributed or its reduction quantified. After F6 compare first TAB and repeated close/open, hand crafting versus stations with large lists, long scrolling and scrollbar jumps, filter/search/sort/empty results, material availability changes/nearby chests, quantity changes, upgrades, controller navigation offscreen and repeated reload/style cleanup. Check info/status icon appearance after cropped readback as well.

## Vanilla crafting functionality parity, 2026-10-20

- Expanded the existing VanillaFilters add-on while retaining native wood/button sprites, fonts/colors, recipe list, selection, descriptions, repair/variant buttons and progress. Native recipe list data still passes through the shared filter/sort adapter; both styles now offer Name A-Z and Craftable-first sorting.
- Search matches the same localized recipe/current-ingredient terms as Ragnarok, with 0.18-second debounce and slash focus (typed slash supports Turkish Q). Station/world context changes discard pending input and show the reset shared query. Added clear/reset controls and a native-font shortcut footer that reads the actual inventory close binding.
- Removed the old forced armor-slot reset in vanilla. Armor subfilter buttons use PresentSlots and shared ArmorSlot state; the native list reserves an additional row only while Armor is selected. Category/craftable icons repack together rather than leaving the craftable toggle at a fixed far-right position. Existing wheel-step components are reused and only an owned component is removed on unload.
- Added native-art minus/typed-quantity/plus controls for new-item crafting. Quantity is bounded to 1-99 and native HaveRequirements affordability using a bounded binary search; upgrade selection resets to one and crafting-in-progress hides the stepper. Native craft button narrows to share its original row, then its exact geometry is restored for upgrades/progress/unload. This reuses existing CraftAmount/native multi-craft handling, with no new crafting or payment implementation.
- Native requirement amounts show total available / required, with a bag/nearby source line and native UITooltip breakdown. Match visible native icon/name and station/upgrader/quality rules; requirements/page selection remain native. Count snapshots refresh at 5 Hz, immediately for recipe/storage-mode changes, using read-only existing ResourceScope and current server-controlled nearby-crafting settings. Native availability/cost/withdrawal remains authoritative between display refreshes.
- Added cleanup of input listeners, typing registrations, quantity/source objects, native amount text sizing/tooltips and button/list geometry. The inactive custom panel no longer resets shared quantity every frame; switching feature off refreshes the unfiltered native list. No new dependency, external asset, RPC or synchronized gameplay rule.
- Windows Debug and Release builds passed with zero warnings/errors. Existing offline regression checks passed, including shared crafting name/ingredient filters and tooltip formatting; they do not execute the new Unity controls/layout, GPU rendering or native crafting callbacks. Debug deployed to isolated scripts and diff whitespace check passed.
- No in-game test performed. After F6 set HUD.Crafting.Style to Vanilla (or Vanilla HUD with FollowHud) and check existing wood/fonts, all-category/clear/craftable/sort/search, slash with Turkish Q and rebound close key, armor subfilter expansion/collapse, station/world reset, native list scrolling/controller selection, typed/minus/plus quantities with bag-only and nearby stock, material loss, upgrades, Shift crafting, variants, active crafting/cancel and style switching, counts/tooltips across requirement pages, nearby disabled/access denied, opening a chest, disabled VanillaFilters, resolution/HUD scales and repeated F6 cleanup. Native recipe rendering is retained; Ragnarok-only visible-row pooling is not applied to vanilla's native UI objects.

## Vanilla crafting layout readability correction, 2026-10-20

- User screenshot exposed category buttons compressed to narrow slivers and a dedicated shortcut row with excessive blank space. Category buttons now remain 28-unit squares with 20-unit glyphs and wrap by available width. Filter/list clearance measures the actual category row count; the armor row still only reserves space when selected.
- Clear/sort/shortcuts share one utility row. The compact slash/current inventory-key guide has bounded auto-sizing for long bindings, with hover explanation. It no longer reserves a separate 18-unit hint row. Native button art/fonts remain in use.
- The screenshot also showed material source text outside the requirement tile, obscured by the native craft button. Each material tile now grows upwards by 24 units while its bottom edge stays fixed. Its native name/icon/amount are arranged within the tile and bag/chest counts occupy two readable internal lines. Source text no longer extends below the tile into the craft row. Original tile and child rect transforms are restored on unload/style switching alongside existing font/tooltip state.
- Windows Debug build passed with zero warnings/errors and deployed to isolated scripts. Diff whitespace check passed. No in-game visual test performed; after F6 check all-category wrapping, few-category hand crafting, armor row expansion, long key bindings, selected material counts with/without nearby chests, upgrades/Shift quantities, long descriptions, resolution/scaling and repeated style/reload restoration. This layout-only refinement adds no gameplay/network behavior.

## Vanilla crafting tooltip-only material sources, 2026-10-20

- Removed the extra bag/chest labels beneath vanilla crafting requirements. Reverted the prior material-tile expansion and icon/name/amount repositioning, so requirements retain their native geometry and only their total available/required text is enhanced.
- Detailed bag/chest/total/required counts remain in the material hover. Requirement tooltips now use the installed native inventory-slot tooltip prefab, including its background/frame/fonts, instead of the flat requirement tooltip. Filter and shortcut hovers use that backed native prefab too.
- Use native UITooltip.Set with the original anchor/offset, a material title and a separate count body, so an already-open tooltip refreshes when its contents change. On disposal, hide only a currently hovered material tooltip managed by this feature, then restore the original prefab/topic/text and amount text sizing. No native prefab is cloned, modified or distributed; no new objects/subscriptions or gameplay changes were introduced for tooltips.
- Windows Debug build passed with zero warnings/errors and deployed to isolated scripts. Diff whitespace check passed. No in-game test performed; after F6 confirm no inline bag/chest rows, native requirement geometry, backed material/filter/shortcut hovers, live count changes, tooltip screen-edge clamping and cleanup on style switching/reload.

## Build repair icon flash on tab/category changes, 2026-10-20

- Read-only inspection of installed BuildUi.UpdatePieceButtons confirms that the special repair/remove button is destroyed and instantiated again for every piece-list rebuild, while ordinary piece buttons are pooled. BuildMenuSkin previously discovered the replacement on a 150 ms polling interval, so a new special icon could render at native size with native chrome before the theme applied its inset scale and surface.
- Track the current native m_specialPieceButton reference every LateUpdate. A replacement immediately triggers skin discovery/painting in that frame; periodic scans remain for other pooled/dynamic controls. No changes to native button creation, callbacks, repair actions, navigation or availability colors.
- Prune destroyed images, labels, controls, tags, icon state and owned-object references during scans so repeated repair-button recreation does not accumulate stale presentation records. The special reference resets on disposal. No new patch, event subscription, dependency or gameplay/network rule was added.
- Windows Debug build passed with zero warnings/errors and deployed to isolated scripts. Diff whitespace check passed. No in-game visual test performed; after F6 rapidly switch category/tab/material/recent/favorite views, including transitions where repair is hidden, then reopen the picker, change tools, verify repair clicks/hover, Vanilla switching and repeated reload cleanup.

## Reference-layout build menu replacement, 2026-10-20

- Replaced BuildMenuSkin/BuildMenuGrid with BuildMenuPanel, BuildMenuData and pure BuildMenuRules. Follows the user's supplied example with a charcoal outer frame/header, tool/durability, piece/material search, native-availability and nearby-count toggles, favorites/recent/categories/materials sidebar, named piece-card grid and right detail panel. Native Piece icons are retained in place of illustrative runes; comfort, flags and station/material facts come from the installed game, with no invented fire/support metadata.
- Cards show native availability tint, native favorite state and a resource-limited preview count. They are pooled to visible rows plus an overscan row, with a separately clipped density footer. The right pane has real description, costs, bag/chest contributions, station range, comfort, Pin and Place. Click inspects; double-click/Place invokes native OnSelectPiece after checking current tool availability. No world piece is spawned by the menu itself.
- Favorites use native ToggleFavorite/RemoveFromFavorites and persist through the game's own lists. Recent/categories/material lists remain native. Local nine-slot quickbar saves prefab names, can pin/remove an inspected piece or replace a slot via Shift-click, and resolves only pieces currently available to the equipped tool. Number keys apply only inside the open menu; no Player hotbar method is patched. Unavailable saved pieces remain stored and are dimmed.
- Added local OnlyBuildable/CountNearbyChests preferences and configurable Q/E, F and slash bindings. Slash character handling supports Turkish Q; search is debounced at 0.15 seconds. Close reads the native BuildMenu binding, with Esc/B supported. Controller card navigation remaps pooled selection to the same logical entry after scroll/rebind, uses trigger category changes, X favorite, left-stick search and Y repair. Native navigation is bypassed only while the custom view owns input; original groups/TabHandler restore on disposal. UI input is suspended for console/settings/search/unified popups.
- Default named-card density is 5 columns by 4 rows. Layout migration 11 changes only the previous untouched 12/6 pair, retaining other user choices. Existing 4-16 column and 3-10 row controls remain beneath the grid. Responsive virtual layout fits the HUD canvas; all scroll views use clipped content and capsule scrollbar handles. Opacity still follows inventory opacity. Real building-storage access/range/world-level/server settings control preview eligibility; the local count toggle never alters actual resource authority or withdrawal.
- Native repair button recreation is now hidden behind a stable owned sidebar action. Replaced skin's native image/font/scale mutation machinery is removed. Unload removes listeners/typing registrations/owned root/static input owner and restores native CanvasGroup and tab component state. Plugin Harmony cleanup handles the new navigation prefix; no new subscription, dependency, RPC or shipped game asset.
- Windows Debug and Release builds passed with zero warnings/errors. Existing regressions passed. Added checks for empty/small/large/partial-last-row/overscroll card windows, 3,000 randomized viewport coverage and pool bounds, and empty/sparse/oversized quickbar persistence. Offline tests do not execute Unity UI or native input/favorite/placement handlers. Debug deployed to isolated scripts; diff whitespace check passed.
- No in-game visual, input, network or frame-time test performed. After F6 open the hammer picker: inspect screen fit/header at current resolution/scale, category/material/favorite/recent changes, repair stability, search by name/material and Turkish slash, only-buildable filtering, permitted/disabled/denied nearby counts, station range, resource changes and no-cost mode; inspect/select/double-click/Place; pin/replace/remove quickbar entries, 1-9 inside menu, ordinary hotbar outside, tool availability and restart persistence; controller selection across scroll boundaries, density extremes, long localized labels/descriptions/cost lists, modal popups, Vanilla switching and repeated F6 restoration. Placement collision/terrain validity remains native and is not promised by the resource count. Advanced native favorite-tag editing is not redesigned into the new sidebar.
- Final reload review: the view now always adds its own CanvasGroup instead of borrowing any existing native/other-mod group. Disposal neutralizes that owned group before deferred destruction; a new F6 instance adds a fresh group and cannot accidentally reuse the outgoing queued component. Existing native group state remains untouched. Final Release rebuild passed with zero warnings/errors; Debug redeployed after this correction.

## Compact build window and quickbar removal, 2026-10-20

- Removed the experimental hammer quickbar, pin/remove controls, slot persistence binding, prefab lookup, and 1-9 input handling. Removed the obsolete quickbar parser checks; pooled-card coverage checks remain.
- Replaced the full-screen backdrop/frame with an owned centered 1280 x 680 canvas-unit window, capped at design size and scaled down to fit smaller canvases with outer margins. Preserved sidebar, search/filter controls, scrollable cards/details and density controls; Place now fills the detail action row. Guides fit the compact footer.
- Windows Debug and Release builds passed with zero warnings/errors. All offline regressions passed, including 3,000 randomized build-card viewport checks. Debug deployed to the isolated BepInEx/scripts directory; whitespace diff check passed.
- No in-game visual or input test performed. After F6 verify the centered window and exposed world at current UI scale/resolution, density footer, scrollable category/card/detail areas, controller navigation to Place, and native hotbar behavior after closing. Repeated F6/style changes should restore the original picker correctly.

## Build grid expansion and aligned density footer, 2026-10-20

- Removed the right detail panel and its Place button, scroll view, resource rows and unused detail-only stock lookup. The compact 1280 x 680 window remains; the piece grid now spans the space to the right of the sidebar (994 canvas units instead of 634). Clicking a piece calls native selection directly, including controller submit. Existing build-task UI supplies requirements after selection.
- Moved grid density into a separate 38-unit footer below the card viewport with a 12-unit gap. Columns and Rows each have aligned label, minus button, centered numeric value and plus button; the guide fits on its own line below. Card navigation reaches the density controls instead of the removed Place button. Columns/rows and local persistence retain their existing bounds.
- Windows Debug deployment and Release builds passed with zero warnings/errors. All offline regressions passed, including randomized card pooling/viewport coverage. Debug deployed to isolated BepInEx/scripts. Whitespace diff check passed.
- No in-game visual/input test performed. F6 checks: grid takes reclaimed space, footer never overlaps cards, stepper alignment at current UI scale, clicks/controller submit return to the selected building preview, favorite/category/search controls and native task requirements work, and repeated reload restores native state.
- Suggested follow-up only: replace independent row/column controls with one Item size slider that automatically derives both counts from the viewport. This slider is not implemented in this change.

## Icon build picker and native hover/click restoration, 2026-10-20

- Removed permanent piece-name text and its reserved area. Native piece icons now center within the tile, using available height; small craftable counts and favorite stars remain as overlays.
- Reduced the centered window from 1280 x 680 to 1120 x 580 canvas units, preserving downscaling on small screens. Shortened sidebar/grid, refitted header controls and kept density controls/guide below the clipped viewport.
- Inspected the installed game's BuildUi/BuildUiPieceButton handlers through Mono.Cecil. Own icons now forward mouse hover/controller focus to native BuildUi.OnHoverPiece using the matching source button. Native Hud.OnHoverPiece updates the existing build-task readout. Pointer exit (respecting native touch behavior), deselection, close, category reset and unload clear hover. Hover is refreshed for pooled tiles as their bound pieces change.
- Clicks invoke the original BuildUiPieceButton.OnClickButton, preserving native desktop selection/sound/close and touch double-tap gating. No new placement, resource or network behavior; existing native selection authority remains. Owned pointer/focus callbacks are cleared on object destruction.
- Windows Debug deployment and Release builds passed with zero warnings/errors. All offline regressions passed, including 3,000 randomized build viewport checks. Debug deployed to isolated BepInEx/scripts; whitespace diff check passed.
- No in-game visual/input test performed. After F6 verify compact window fit, icon-only tiles at saved density, pointer hover requirements/title/description, exit clears preview, controller focus/submit, touch behavior if available, category/filter/scroll rebinding and repeated reload/style restoration.

## Hammer readout below build picker, 2026-10-20

- BuildTaskPanel exposes only its currently visible owned RectTransform and clears that reference on hide/dispose. Build-menu layout converts its actual top edge into the menu canvas coordinates, reserves a 12-unit gap, and shifts the picker upward; it scales down to fit between that edge and the 24-unit top margin when necessary.
- Placement updates even when row/column density is unchanged, so hover descriptions, wrapped requirements and HUD scaling are included in the reserved area. Existing hammer readout placement above vitals/hotbar remains authoritative. No gameplay/input/resource changes.
- Windows Debug and Release builds passed with zero warnings/errors. Offline regression checks passed. Debug deployed to isolated BepInEx/scripts; whitespace diff check passed.
- No in-game visual test performed. After F6 verify the entire hammer name/description/material card remains below the menu across hovered pieces, long descriptions/multiple requirement rows, saved densities and UI scales. Closing the menu should retain the normal task card position.

## Stable hammer readout reservation, 2026-10-20

- Replaced live hovered-card-height menu reservation with a fixed 200-unit task slot scaled by HUD scale and available width. The task panel publishes the slot's top in world space; menu layout reserves that edge with the existing gap, independent of hovered text/costs.
- While the native build picker is open the hammer readout stays centered below the menu. Tall cards scale down to fit the reserved slot, retaining all description/requirement content. Outside the picker the existing dynamic size/position behavior remains. Visible-slot state clears on hide/dispose.
- Windows Debug/Release builds passed with zero warnings/errors. Offline regressions passed. Debug deployed to isolated BepInEx/scripts; whitespace diff check passed.
- No in-game visual test performed. After F6 move repeatedly between short/long descriptions and pieces with different requirement counts, including while scrolling; menu position and scale should stay fixed. Confirm full hammer readout remains visible, tall-card readability, native hover/click behavior, HUD scaling and normal placement after closing the menu.

## Compact build sidebar text and rows, 2026-10-20

- Reduced category text from 16 to 13 (autosize floor 10) and row pitch from 44 to 30 units, with 27-unit buttons. Reduced Favourites/Recent text from 18 to 15 and their buttons from 40 to 30 units; category/material tabs and repair label use 12-unit text.
- Reclaimed vertical space above and below the category viewport: its height is now 246 instead of 206 units. At the current window layout, eight complete categories fit instead of four, with the same sidebar width. Updated scroll-content height to match the smaller row pitch. Native category callbacks and selection behavior remain unchanged.
- Windows Debug build passed with zero warnings/errors and deployed to isolated BepInEx/scripts. Whitespace diff check passed. No in-game visual test performed; verify text readability and category scrolling after F6, including long localized labels and the last category above Repair.

## Build picker markers and compact controls, 2026-10-20

- Inspected installed game BuildUiPieceButton.Setup: the vanilla upgrade arrow is
  shown from Piece.m_isUpgrade. Owned cards now mirror this flag and use the
  native m_upgradeArrow sprite; pooled non-upgrade cards disable the marker.
- Moved compact row/column steppers alongside the category heading and piece count.
  Reclaimed the separate footer for the card viewport. Search, filter toggles and
  Close now share a 36-unit height with smaller text and icons.
- Replaced the sidebar repair button with a top-left hammer button that calls the
  existing native repair selection. The category viewport uses the freed space.
- Windows Debug build and dev deployment passed with zero warnings/errors.
  No in-game test was performed; F6 reload and visual/input checks remain.

## Character shortcut panel theme refresh, 2026-10-20

- Replaced InfoPanel's legacy ornamented Window/Button sprites with PackSurface,
  PackEdge and PackPill surfaces matching current inventory/build UI. Left-aligned
  parchment name, thin divider, rounded shortcut cards, balanced monochrome icons
  and labels; gold borders/icons identify enabled toggles such as PvP.
- Kept original panel bounds, inventory opacity, native tooltips and forwarding to
  native buttons/toggles, including other mods' shortcuts. Native source masks
  suppress pointer hits without disabling Selectable.IsInteractable checks.
- Owned listeners and roots are cleared on rebuild/unload/HUD replacement; owned
  masks are neutralized before deferred destruction and do not reuse other masks.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game test performed. F6 reload, visual alignment, shortcut clicks and PvP
  toggle state still need an in-game check.

## Character books theme refresh, 2026-10-20

- Skills, Compendium, Trophies and Achievements share the current PackSurface/Edge
  frame, rounded tabs and cards, parchment headings/body copy, gold selection
  outlines and compact Esc/Close control. Preview glyphs remain data driven in a
  rounded card; removed the legacy diagonal scene and per-frame ember animation.
- Updated skill rows, journal chapters/entries, trophy/achievement tiles and
  progress bars. Added capsule rails/thumbs outside existing scroll viewports
  without reducing the five-column tile area. Inventory opacity updates live;
  the existing 1560x880 frame scales down when the canvas is narrower.
- Kept all native data sources, secret/locked achievement behavior, collection
  counts, skill progress, tab actions and dialog close paths. Owned native masks
  are neutralized and destroyed; listeners/root are cleared on close/unload.
- Windows Debug build/deployment: zero warnings/errors. Diff check passed.
  No in-game test performed; check all four tabs, selection, scroll dragging,
  Esc/Close, opacity and lower-resolution layout after F6 reload.

## Character books opening fix, 2026-10-20

- User reported the native panels remained visible after the theme refresh.
  Development LogOutput.log confirmed Unity rejected a duplicate CanvasGroup
  on Achievements; BookWindow.Open then threw NullReferenceException before
  constructing the themed canvas. The style config correctly followed Ragnarok.
- BookWindow now reuses an existing CanvasGroup and snapshots/restores its alpha
  and raycast settings. Only groups created by this view are destroyed. Applied
  the same correction to InfoPanel's child masks; native interactability remains
  unchanged. This supersedes the separate-group assumption in the entries above.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  Root cause is confirmed from the running game's log, but the deployed fix has
  not yet been tested in game. Reload with F6 and reopen the character books.

## Pre-commit regression checks, 2026-10-20

- Windows dev.ps1 test passed all suites: 45,562 allocation checks including
  5,000 randomized transactions; death penalties; configuration migration;
  crafting search/frame/details; 1,000 randomized grave reservations; and
  3,000 randomized build-grid viewport checks.
- Latest Windows Debug build/deployment passed with zero warnings/errors.
  The latest character-book opening correction still needs an in-game retest.

## Health-bar regeneration text removal, 2026-10-20

- Removed the HP-per-second text object and its HUD update/snapshot values.
  Deleted the now-unused read-only HealthRegeneration estimator; native healing
  behavior and the current/maximum health display are unchanged.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game test performed. Reload with F6 to verify the health-bar display.

## Configuration sliders, compact layout and navigation, 2026-10-20

- Running development log confirmed ConfigControls.Slider -> ConfigEntry<T>.BoxedValue
  InvalidCastException. The numeric conditional promoted integer values to float;
  explicit boxing now preserves Int32/Single types. Wide float ranges no longer
  force integer stepping. Unit labels distinguish minutes, metres and percentages.
- Added regression checks for boxed types, signed penalties, fractional station
  speeds, minimum durations, opacity precision, range clamping and capacity zero.
  dev.ps1 test passed all existing suites and the new slider suite.
- Audited Plugin's sync list: nearby-storage toggles/range, equipment/recovery,
  chest search, powers, harvest, skills, resource costs and all station/sailing/
  ballista settings are registered. Retained server LockSettings policy and local
  HUD preferences. Added UI write guards and locked-default-reset skipping; the
  server's lock policy is read-only on clients while enforcement is active.
- Config frame reduced to 1040x680 with smaller text/rows, screen-fit scale,
  independently folding section groups and hide/show sidebar with page reflow.
  Existing config subscriptions and owned controls are cleaned up on unload.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game or multiplayer test performed for this change. Check slider dragging,
  group/sidebar toggles, host edits reaching a client, locked controls/default reset,
  and restoration of personal settings after disconnect.

## Configuration numeric input and drag steps, 2026-10-20

- Replaced slider readouts with numeric text boxes plus unit captions. Enter or
  focus loss commits exact values within the allowed range; invalid/non-finite
  input restores the existing value. Supports invariant/current-culture numbers,
  opacity percentages and existing server write guards.
- Drag steps: percentages 5, capacities/margins 10, ranges 5, minutes 1,
  scale/opacity 0.05, other floats 0.1 and other integers 1. Slider endpoints stay
  reachable. Exact text input bypasses steps.
- Numeric fields join typing registration and remove listeners/registrations on
  page changes and unload. Build and all regression suites passed, including
  snapping, negative values, exact entry, opacity conversion, invalid input and
  clamping. Debug deploy passed with zero warnings/errors; diff check passed.
- No in-game test performed; F6 reload and slider/text-field interaction checks
  remain, including server-controlled clients.

## Expanded nearby-storage radius, 2026-10-20

- User selected increased radius with open-chest-only sorting, rather than loaded
  world or unloaded-world storage. Shared nearby-storage/Quick Stack range now
  defaults to 100m, configurable 1-500m. Set the isolated development config's
  existing 50m value to 100m. Chest search has its independent existing setting.
- ContainerRegistry continues to enforce the same radius on requester and owner;
  its Range entry remains in server synchronization. No changes to privacy, wards,
  occupancy, handoff, payment or sort scope. Unloaded bases are not included.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game/multiplayer test performed; reload F6 (F7 reloads config), then check
  resource pulling/Quick Stack at 50-100m and rejection beyond the configured radius.

## Always-visible compact status labels, 2026-10-20

- Status labels always render; removed the ShowLabels/LabelsKey bindings and
  shortcut handler so an old saved key cannot hide them. Tooltip/timers unchanged.
- Tightened cells from 46x60 to 40x52, row gaps from 4 to 2, and label top from
  38 to 35. Removed the 6-unit icon inset. Updated food clearance calculations.
- Default status left margin is zero; layout migration 12 changes the prior
  TopLeft/150 margin to zero and preserves custom placements.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game test performed; F6 reload to check alignment and label readability.

## Status alignment correction, 2026-10-20

- User clarified the status strip should start under the character name, not at
  the screen edge. Restored the 150-unit default origin (40 character margin +
  110 name offset). Layout migration 13 corrects the prior zero-margin setting.
  The removed internal 6-unit icon inset stays removed, preserving tighter spacing
  and always-visible labels. This supersedes the previous left-margin change.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game test performed; F6 reload to verify the corrected name alignment.

## Center status labels on their icons, 2026-10-20

- Centered the 40-unit label bounds on the 34-unit icon/plate (x=-3), matching
  the timer's center, and set explicit centered text alignment. Strip origin
  and compact cell spacing are unchanged.
- Windows Debug build/deploy passed with zero warnings/errors; diff check passed.
  No in-game test performed; verify label centering after F6 reload.

## Session wrap-up checks, 2026-10-20

- Final Windows Release build passed with zero warnings/errors. All regression
  suites passed, including configuration numeric input/drag-step tests. The latest
  Debug DLL is already deployed to the isolated development scripts directory.
- User confirmed the centered status labels. Multiplayer configuration locking
  and expanded nearby-storage access still require real-peer validation.
