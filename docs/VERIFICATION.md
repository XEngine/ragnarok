# Development setup verification

## Open-source alpha preparation, 2026-10-22

- Marked the current distribution 0.5.5 alpha in public copy, archive names and
  assembly informational version. Thunderstore/plugin numeric version remains
  0.5.5 for its three-number manifest/compatibility format.
- At the owner's request, original code/documentation/procedural artwork are now
  MIT-licensed. Added contribution guidance, issue forms, a PR template and public
  CI for game-independent regressions/packaging metadata. Third-party font terms
  remain independent. Legacy undocumented power PNGs were removed from the current
  tree; old commits retain them outside the MIT grant. All builds now resolve
  power artwork through the installed ObjectDB and retain the procedural stag.
- Scanned 1,052 historical text blobs for private-key/token/credential patterns;
  no matches found. No tracked DLL/PDB/save/private-key/dev.local.json paths found.
  This is a targeted scan, not a guarantee against all possible sensitive content.
  Repository history is preserved rather than rewritten. No prior Actions runs
  or GitHub releases existed at the preparation check.
- Debug/Release builds, metadata checks, all C# regression suites and 12 packaging
  validation checks passed. Alpha archives include the MIT LICENSE and five font
  licenses. No new in-game test performed; native-icon fallback appearance and
  previously recorded gameplay/UI scenarios still need live verification.

## Public beta distribution preparation, 2026-10-22

- Prepared versioned Thunderstore and manual/Nexus ZIPs for 0.5.5 beta, with
  public feature/installation copy, compact changelog, dependency manifest,
  original 256x256 procedural rune icon, font notices/license copies and SHA-256
  checksums. Detailed handoff and Nexus BBCode are under release/.
- Verified official Thunderstore manifest/icon/package rules and live dependency
  pages (BepInExPack 5.4.2351, Jötunn 2.30.2). Reviewed Nexus submission/disclosure
  requirements and included applicable AI tagging guidance in the upload handoff.
- Public Release no longer embeds legacy power PNGs without recorded provenance;
  existing displays use runtime native-icon fallbacks. Debug previews retain the
  local assets. All five font license resources remain embedded and copied into
  package notices. No external code/artwork was incorporated for the new icon.
- Shared Python-standard-library packager verifies metadata/project/plugin version
  agreement, PNG dimensions/chunk CRC, exact file allowlist, UTF-8 docs, ZIP CRC,
  safe paths and Release DLL hash. Windows package command runs Release metadata
  checks too. Cross-platform ZIP entries use forward slashes and fixed timestamps.
- Release build and Debug/Release metadata checks passed; all C# regression suites
  and 12 new packaging validation tests passed. Packager ran on Windows and WSL;
  unpacked payloads match (compressed hashes may differ by zlib implementation).
- This prepares upload artifacts only. No public mod page/upload/site-side
  validation, Vortex deployment, Linux game runtime or new in-game test performed.
  Latest HUD positioning, mount temperament and full multiplayer handoff tests
  remain pending and are disclosed in both listings. Server/client left running.

## Health and stamina regeneration numbers, 2026-10-22

- Removed the ECG trace and unused hindrance state/reflection. Stamina's measured
  per-second readout stays right of its bar; matching health text sits to the left
  of the health bar. Both use muted 10-point text and inherited vitals scaling.
- Health uses native food regen divided by its ten-second interval and the active
  SEMan health-regen modifier; full health shows zero. This is sustained food
  recovery, excluding instant heals and separate healing-over-time effects. It
  stays readable with both vanilla healing ticks and installed Steady Regeneration,
  which changes food healing frequency without changing its total amount.
- Verified native UpdateFood/Heal through read-only Cecil inspection. No game
  source/assets incorporated. Regeneration mechanics and network behavior unchanged.
- Debug deployment, Release packaging and regression suites passed, including
  health tick-unit conversion, status scaling, full-health/disabled recovery and
  invalid input checks. In-game positioning test pending; F6 loads the update.

## Stamina pulse graphic, 2026-10-22

- Replaced visible hindrance text beside the regen number with a small frameless
  ECG-style vector trace. Recovery drives pulse strength/speed; a hindrance gives
  a muted amber reaction and reduces amplitude, settling to a line when recovery
  stops. Full stamina uses a quiet muted line rather than a warning.
- Mesh refresh is capped at 30 Hz with elapsed-time animation; no per-frame
  GameObjects/textures or third-party assets. Graphic belongs to the vitals root,
  does not intercept input and retains the existing read-only stamina observer.
- Debug deployment and Release packaging passed with zero warnings/errors.
  No in-game visual test performed. F6 loads it; no server restart required.

## Stamina regeneration readout, 2026-10-22

- Ragnarok vitals now show observed stamina recovered per second immediately to
  the right of the stamina bar. Read-only instrumentation samples the local
  Player.UpdateStats(float) before/after values over 0.2 seconds of game time;
  zero/negative recovery immediately clears the rate. Full bars show +0.0/s.
  This measures recovery within native stat updates, not arbitrary frame deltas
  containing potion gains/action spending; unrelated later mod patches may still
  alter stamina outside that sampling interval.
- A small gold hindrance label pulses on action/state entry for 1.2 seconds:
  encumbered, swimming, attacking, dodging, wall-running, sprinting, blocking,
  native recovery delay, or the first active SE_Stats with a regen multiplier
  below one. Ongoing causes remain visible without repeatedly flashing; short
  causes remain readable for up to 2.5 seconds. Only one cause is shown at a time.
- Labels/outline objects belong to the vitals root; observation unsubscribes on
  style change, owner rebuild or unload. No stamina values, effects, authority,
  config synchronization or network protocol are changed. Vanilla HUD is unchanged.
- Debug deployment, Release packaging and all regression suites passed, including
  new rate/interruption/non-finite-input and hint-lifetime checks. No in-game
  visual test performed; verify right-side spacing at preferred HUD scale,
  repeated sprint/attack/dodge transitions, Wet/Cold and full stamina. F6 loads
  the client update; no server restart is required.

## Skill milestone placement, 2026-10-22

- Skill milestone cards now share recipe notifications' minimap-side placement,
  following moved/scaled minimaps with compass avoidance and viewport clamping.
  When a recipe card is active, the milestone sits below it with an eight-unit
  gap. Ordinary skill progress toasts and central system messages retain their
  existing positions. Shared placement removes duplicated recipe layout code.
- Windows Debug deployment and Release packaging passed with zero warnings/errors.
  F6 loads the UI update; no server restart needed. In-game visual check pending,
  especially simultaneous recipe/milestone cards and narrow viewports.

## Expanded map layering, 2026-10-22

- Added a sorting Canvas on the native large-map root, above Ragnarok gameplay
  HUD and windows. Its GraphicRaycaster retains native map pointer controls.
  Applies with either minimap/HUD style and restores pre-existing Canvas settings
  or removes owned components on scene replacement/F6 unload.
- Windows Debug deployment and Release packaging passed with zero warnings/errors.
  No in-game visual/input retest performed; check map pins, panning, zoom, closing
  the map and F6 reload. This UI-only change requires no server restart.

## Wrap-up audit, 2026-10-22

- See [AUDIT-0.5.5.md](AUDIT-0.5.5.md) for scope, findings and remaining live checks.
- Full regression suites, Debug/Release metadata, installed BepInEx migration and
  Release packaging passed. Archive contents and DLL hash were checked; embedded
  font license resources were retained. Linux packaging syntax parsed in WSL.
- Fixed stale installation version and missing mount guide in both packaging
  workflows. No in-game test, client shutdown or server restart during this audit.

## Buff label and timer order, 2026-10-22

- Passive/ongoing status tiles now show the name directly under the icon and
  any timer below the name. Effects without icon text show no dash or timer text.
  Resting retains its comfort level below its name; debuff layout is unchanged.
- Windows Debug build/deployment passed with zero warnings/errors. F6 loads the
  client update; no server restart is needed. No in-game visual test performed.

## Mount temperament 0.5.5, 2026-10-22

- Windows Debug deployment and Release build passed with zero warnings/errors.
  Eager metadata checks passed for both configurations; all regression suites passed.
- New deterministic checks cover all four mood probabilities, Riding reduction,
  refusal/hesitation boundaries, their two-to-one distribution and grace override.
  Reviewed the summon flow: one roll before movement; hesitation resets the final
  arrival check and extends the deadline by two seconds. Refusal never enters the
  movement or completion-receipt path. Combat checks continue during the delay.
- Read installed native Tameable.IsHungry/GetStatusString with Mono.Cecil to verify
  feeding timestamp units, feeding duration and live alert semantics. No game code
  or assets were copied. Saved alert flags are excluded from temperament.
- Deployed Debug to isolated client scripts (F6 required). Gracefully saved
  RagnarokTest (_main.32.ok) and installed Release on the isolated dedicated server;
  startup confirms Ragnarok 0.5.5, loaded mount bonds and UDP ports 2456/2457.
  Backup: D:\RagnarokDev\deploy-backups\mount-restart-20261022-151058.
- No in-game temperament test performed. The user previously requested keeping
  the client open and skipping isolated-world tests. Fear is observed only for a
  server-loaded mount; distant hunger works from persistent native feeding data.

## Native verification of scene-cleanup fixes, 2026-10-21

- After the user closed the client, ran the real Unity startup/F6 smoke test.
  Inventory, station, XP, policy and new UI lifecycle checks passed on both loads;
  the destroyed notification/portrait accesses and duplicate localization errors
  did not recur. The full run failed only because BepInEx logged the earlier
  ReadDebugHeader/negative-count assembly error during startup.
- Scanned installed plugin DLLs with the installed Mono.Cecil: MyDirtyHoe.dll was
  the failing file. Preserved the all-mods smoke log outside the checkout at
  D:/RagnarokDev/diagnostics/hud-smoke-with-installed-mods.log.
- With the client closed, temporarily moved only that DLL out of plugin discovery,
  repeated scripts/smoke.ps1 and obtained a complete PASS for startup and F6 reload.
  A finally block restored MyDirtyHoe.dll and verified its original SHA-256 hash.
  ScriptEngine/BepInEx configuration was restored by the smoke script. No world or
  character was loaded/modified, and the dedicated server was not restarted.
- Cleanup fixes are deployed. Server rejoin and camera-spinning behavior remain
  unverified in gameplay; the native checks verify scene-owned UI destruction and
  plugin reload rather than mouse/controller movement in a connected world.

## Scene-change cleanup failure and interrupted reload, 2026-10-21

- Real client log identifies two destroyed-native-object accesses after returning
  through the menu/server scene transition: CharacterPortrait.Dispose writes
  RawImage.texture after its scene was destroyed; ItemNotifications.Clear writes
  destroyed CanvasGroup.alpha. The latter repeats every frame and interrupts
  OnDestroy. The next reload then fails with "Ragnarok localization is already
  registered", leaving custom rendering/native masks incompletely restored.
- Added Unity lifetime guards for fade reset, portrait texture detach and owned
  root destruction. Notification disposal is idempotent and dead child roots force
  reconstruction. Plugin unload now releases input/patch hooks first, attempts
  every component release despite failures, and still reaches native HUD restoration
  and localization cleanup. Jotunn releases its own block even if a binding fails.
- Nine production-linked fake-null/cleanup checks passed, including a simulated
  broken renderer followed by actual localization release. Input regressions now
  include a failing action disposal and preservation of another mod's input block
  (109 checks total). Full automated suites and Debug/Release builds passed.
- Added opt-in real-Unity startup checks that construct notification/portrait UI,
  destroy their synthetic parent, then clear/dispose their managed owners repeatedly.
  Windows/Linux smoke validation requires these checks on both initial load and F6.
  Native smoke was attempted but its existing guard rejected execution while the
  user's Valheim process remained running; no client/server was stopped.
- Deployed fix to development scripts. The current client needs one full restart:
  failed old-version unloads already left a localization registration and UI objects
  behind, and new code cannot repair those destroyed old plugin instances via F6.
  Rejoin/return to menu/rejoin and reload after restarting; check that errors above
  disappear and the UI restores consistently. Camera spinning is user-reported;
  logs establish the cleanup failures, not a separate camera-input cause. It still
  requires verification in a clean session; no camera-sensitivity/config reset made.

## Categorized original status icons, 2026-10-21

- Debuff refinement: enlarged only the compass-row icons to 48 pixels, with a
  larger outlined countdown inside the lower part of each icon and names below.
  Expanded row spacing; untimed debuffs omit the timer. Passive/ongoing sizing is
  retained. Debug build/deploy passed; in-game visual verification is pending.
- Visual follow-up: enlarged original icons from 24 to 32 pixels, slightly enlarged
  names/timers and expanded spacing. Removed visible plates/rims from every group;
  transparent hover targets preserve tooltips. Debug build/deploy passed without
  warnings/errors; in-game appearance remains unverified.
- Status tiles now use each StatusEffect.m_icon directly in full color; removed
  custom rested/fire/shelter/wet replacements and monochrome conversions from this
  display. Tiles grew from 34 to 42 pixels, icons from 16 to 24, with matching
  sizes in all groups. Existing Forsaken power artwork is outside this change.
- Passive effects are identified from Humanoid.m_equipmentStatusEffects (including
  extra utility effects). Other beneficial/unknown effects stay in Ongoing. Both
  groups use the existing HUD.Status placement near the character name, with
  localized headings and separate rows; portrait provisions/roster reserve their
  actual combined height. Existing tooltips, timers and comfort count are retained.
- Harmful effects use a separate centered strip below the compass at identical
  HUD scale. Known native poison/burning/smoked/wet/cold/freezing/frost/lightning/
  spirit/tared/encumbered names and poison/burning/smoke implementation types route
  here. Inspected installed SEMan native IDs and equipment-effect field. No generic
  debuff flag exists; unknown mod effects without these identities/types remain
  Ongoing. CampFire/Resting/Rested/Shelter are explicitly not harmful.
- Compass-off/preview fallback uses top center. Owned roots, labels and tooltips
  are destroyed on HUD rebuild/unload. Settings toggle restores the native list.
  Presentation only: no status application, gameplay policy, RPC or new dependency.
- Added routing regressions for equipment, ongoing buffs, native harmful names,
  mod damage types, clone names and precedence. Debug/Release builds and all
  automated suites passed; development build deployed. No in-game test performed.
- After F6 verify Megingjord/Wishbone/Wisplight, rested/shelter/fire, actual poison/
  burning/smoke, timer expiry, mixed categories, long/localized names, tooltip
  hovers, moved/scaled compass, status toggle and repeated reload cleanup.

## Active food beside Forsaken power, 2026-10-21

- Moved the three active-food cells from the portrait to an independently owned
  HUD root beside the Forsaken power display, retaining original food sprites,
  rings and countdowns. Follows HUD.Power placement and HUD scale; switches to the
  left when space on the right is insufficient and clamps to the screen.
- Food remains available when the power display is disabled. Existing food toggle
  and inventory visibility behavior are retained. Provision shortcuts remain below
  the portrait; their layout and the party roster no longer reserve the old food gap.
  New root is destroyed during HUD rebuild/unload. Presentation only.
- Debug build/deploy passed without warnings/errors. No in-game test performed;
  after F6 check food placement, moved/scaled power, power toggle, inventory and
  repeated reload cleanup.

## Original food item icons, 2026-10-21

- Food cells below the portrait now display HudFood.Icon (the eaten item's original
  GetIcon sprite) in full color using the existing item-icon renderer. Removed
  the generic health/stamina/eitr food symbols and tints; duration rings/countdowns
  remain. Cells without a sprite display no substitute icon.
- Debug build/deploy passed without warnings/errors. No in-game visual test
  performed; after F6 check three different foods, refresh and expiration.

## Crosshair ranged ammunition, 2026-10-21

- Icon/count refinement: removed the ammo-name label, increased the icon to
  20 pixels at 90% opacity, and retained the muted count/soft outline. Empty ammo
  now displays a red ×0. Debug build/deploy passed; in-game appearance unverified.
- Further muted the ammo line (60% text, 50% icon opacity) and replaced its heavy
  world silhouette with a narrow, low-opacity outline. TextOutline defaults remain
  unchanged for existing callers. Debug build/deploy passed; visual test pending.
- Visual follow-up: reduced ammo text from 14 to 12 pixels and icon from 20 to
  16 pixels, tightened spacing, and muted text/icon opacity. Debug build passed
  without warnings/errors and was deployed. In-game appearance remains unverified.
- Position follow-up: shifted the readout 32 HUD pixels right while retaining
  its gap below the crosshair, scaled with the HUD. Debug build/deploy passed;
  in-game positioning remains unverified.
- Added HUD.CrosshairAmmo (default true): a frameless outlined selected-ammo name
  and count plus icon below the native crosshair for Bows/Crossbows weapons with
  an ammo type. Shows localized No arrows/No bolts with a red zero when empty.
- Inspected installed Attack.FindAmmo: it reads the equipped ammo, validates its
  inventory membership/type and falls back to Inventory.GetAmmoItem. Called via
  a cached delegate without equipping or consuming ammo. Counts same-name/type
  carried stacks only, including Better Archery inventory quiver rows. Other ammo
  varieties and nearby chest resources do not inflate the number. Reads at 10 Hz.
- Local presentation on either HUD style; hides for unequipped/non-ranged weapons,
  death, hidden crosshair/HUD, inventory, menus, chat, console, large map and modal
  UI. Owned objects/outlines are destroyed on HUD replacement and plugin unload.
  No new gameplay patches, server settings, RPCs or dependencies.
- Eight production-linked tests cover ranged detection, mixed ammo names/types,
  multi-stack counting, empty inventories and overflow. Full automated suites and
  Debug/Release builds passed; deployed to development scripts.
- No in-game test performed. After F6, equip bows/crossbows, switch ammo, fire,
  split/move stacks, empty/replenish ammo, use Better Archery quiver keys, holster,
  open menus and repeat F6. Verify real modded ammo selection and crosshair spacing;
  count denotes carried ammunition, not a separate crossbow chamber counter.

## Tamed animal minimap dots, 2026-10-21

- Added HUD.Minimap.TamedAnimalDots, enabled by default, for loaded living tamed
  creatures with Tameable components in explored terrain. Vanilla Tame() does not
  persist a taming player's ownership; marks represent shared tames, not a claim
  about which player owns an animal. Reads Character.GetAllCharacters at 4 Hz.
- Native unsaved pins use an original runtime-generated yellow dot sprite with a
  dark rim. Native map zoom/clipping supports both minimap styles and the large
  map. Jotunn's existing map overlays remain intact; moving dots use native pins
  because its map manager exposes texture layers rather than a moving pin API.
- Inspected installed Minimap.Start/UpdatePins/GetClosestPin: None is a visible pin
  type, and unsaved pins are excluded from normal pin editing. IsExplored is read
  via a cached private delegate. Splatform is referenced from the installed game
  for AddPin's optional signature, Private=false; no game binaries redistributed.
- No RPCs, ownership changes or persistent animal/map writes. Removes owned pins
  on death/unload, fog exclusion, disable, minimap/player change and plugin unload;
  destroys owned sprite/texture. Dedicated server never creates this HUD component.
- Added 18 production-linked marker lifecycle checks with doubles for filtering,
  movement, death, unload, foreign pin preservation and repeated cleanup. Full
  suites and Debug/Release builds passed; deployed to development scripts.
- No in-game test performed. After F6, verify tamed versus wild animals, yellow-dot
  size/visibility, moving livestock, explored terrain, death/unload, both map styles,
  zoom/large map and setting toggles. Another player's network ownership must not
  hide a shared tame. No tracking of distant unloaded animals is provided.

## Split pickup and unlock notifications, 2026-10-21

- Native Character.ShowPickupMessage was inspected in the installed game assembly:
  pickups use TopLeft, `$msg_added ` plus item name, and the supplied amount/icon.
  Route this identifiable message into a dedicated pickup queue; unrelated warnings
  and general messages retain their existing feed. Native logging remains in place.
- Pickups use a single outlined text line without images/frames above ShownVitals
  and the equipped-name area, with a gold name, bold +quantity, quick rise, damped
  pop and fade. Same-item active/pending arrivals merge counts; distinct items are
  FIFO. Skill toasts dock above active pickups. A centered fallback supports absent
  custom vitals.
- Unlock events use a separate FIFO milestone channel beside the minimap. Actual
  minimap/compass bounds determine placement; narrow layouts move below the compass
  and both channels clamp to the canvas. No new gameplay settings, RPCs or assets.
  Owned objects and queues are cleared on style changes, preview clear and unload.
- Added pickup queue regressions for active/pending merges, FIFO, invalid amounts,
  overflow, cleanup and a 1,000-event randomized burst. All suites and Debug/Release
  builds passed. Development build deployed; no in-game visual test performed.
- After F6, use `ragnarok_popup pickup` and `ragnarok_popup unlock` (close F5), then
  real pickups/unlocks. Check +5 merged quantity, independent simultaneous channels,
  moved/scaled HUD, narrow resolutions, skill toast spacing, native log, Vanilla
  fallback and repeated F6 cleanup. Preview commands change no gameplay state.

## Removed second hotbar, 2026-10-21

- Deleted HotbarRows and its UseHotbarItem patch, plugin lifecycle hooks, enable
  setting and switch binding. Removed the action-bar switch indicator, dynamic row
  selection, second-row inventory key labels and associated localization entries.
- The action bar displays row zero; KeepHotbar sorting protects only the top row.
  Provision hotkeys no longer defer to a row-switch modifier; their existing patch
  still suppresses the matching native hotbar activation. No new network policy.
- Migration discards SecondRow/SwitchRowKey from HUD.Hotbar and QualityOfLife.Hotbar
  while preserving normal hotbar presentation settings and inventory contents.
- Debug/Release builds and automated regression checks passed, including saved
  config cleanup using the installed BepInEx. Development build deployed for F6.
  No in-game test performed. Check normal 1-8 keys/action-bar clicks, provision
  shortcuts, sorting below the first row, absent switch UI and repeated F6 reload.

## Removed legacy Quick Stack keep options, 2026-10-21

- Removed KeepWeapons, KeepConsumables and KeepTools and their unused fallback
  path. Both inventory styles supply the selected category to Quick Stack;
  equipped/quest items, held items and assigned gear/provision/quiver slots retain
  their protections. No new authority or synchronization settings were added.
- Migration removes all three saved keys from current and legacy chest sections.
  Migration regressions and installed BepInEx configuration checks passed, as did
  the build and existing automated suites. Deployed to development scripts.
  No in-game test performed; press F6 to load the changed settings page.

## Unified Nearby Chests, 2026-10-21

- Combined crafting/upgrading, building, station supply, Quick Stack and search
  under one Enabled entry and one effective radius (PersonalRange capped by the
  server Range). Removed separate capability switches and search radii. Server
  Enabled/Range remain mandatory even with the general gameplay lock off.
- Search now uses ContainerRegistry discovery, access checks and read-only refresh;
  no separate object scan or ownership request. Private/ward-protected, out-of-range,
  in-use, world-loot, tombstone, cart and ship inventories are excluded consistently.
  Quick Stack into the currently open owned chest retains the shared range/access
  checks. Disabling the feature closes search and clears its guide/marker.
- Moved shortcut, stacking preferences and marker display onto the Nearby Chests
  page. Preserved existing Nearby Chests enable/range values and moved personal
  preferences during migration; retired switches/ranges cannot override the policy.
  Version is 0.4.0 because older independent permissions cannot represent this model.
  Update clients and dedicated servers together.
- Debug/Release builds passed without warnings/errors. All automated suites passed,
  including new migration assertions. Installed BepInEx checks verified migration,
  backup preservation, actual StorageSettings binding and shared cap calculations.
  Eager loader/metadata checks passed for both configurations. Client Debug build
  deployed; no active client or dedicated-server process was restarted.
- No in-game test performed. Verify the shared range boundary across all actions,
  disabling/re-enabling with an active guide, private/ward/in-use exclusions, open
  chest Quick Stack, F6 cleanup and two-peer authority using NEARBY-CHESTS.md.

## ScriptEngine Jotunn metadata resolution fix, 2026-10-21

- Reproduced the reported Jotunn assembly-resolution failure with the installed
  Mono.Cecil eager metadata reader and core/game search paths, without adding the
  nested Jotunn plugin directory. The compatibility attribute's enum arguments
  triggered the same ReadCustomAttributeEnum failure.
- Omitted that attribute from Debug builds; release VersionCheckOnly/Minor metadata
  and the required Jotunn dependency/runtime check remain intact.
- Added `scripts/check-plugin-metadata.ps1`. After building each configuration, run
  it with `-Configuration Debug` and `-Configuration Release`. Both checks passed;
  Debug reads successfully without a Jotunn resolver search path, and Release checks
  the actual enum values with the installed dependency available.
- Debug/Release builds passed with zero warnings/errors, and all regression suites
  passed. The Debug fix was deployed to the isolated development scripts directory.
  Actual in-game F6 reload remains to be verified; no active client/server was stopped.

## Jotunn phases 1-4 for existing features, 2026-10-21

- Implemented the [phased roadmap](JOTUNN-ROADMAP.md). New content is explicitly
  deferred until requested; no items, pieces, effects, skills or new save data were added.
- Localization: 181 English/Turkish tokens for settings navigation/page summaries,
  common controls/overview captions, controller options and chest search. Game
  language selects the catalog. Unconverted individual descriptions and unsupported
  languages use English. Registration/native words are cleaned on unload, previous
  words restored and foreign replacements preserved. Settings rebuild on language
  changes; Turkish glyphs already exist in the shipped font character set.
- Controllers: optional personal settings-window and weapon-dial bindings, both None
  by default. Keyboard/mouse behavior is retained. The opening device controls dial
  release; right-stick aim uses the existing dial. Rebinding/unload remove native Joy
  actions, Jotunn registrations, layout presentations and config listeners. Large enum
  controls cycle readable choices. No gamepad button is consumed by default.
- Versions: release metadata declares VersionCheckOnly/Minor. Jotunn's BepInEx startup
  registry cannot guarantee the same handshake for ScriptEngine development instances.
  Existing authenticated server-settings packets already contain the version; exact
  differences remain logged and now display in the F10 Build card. Display state is
  assigned after complete validation and cleared on disconnect. Wire format/authority
  remain unchanged; extra native runtime assertions were added but not run in-game.
- Maps: HUD.Minimap.ChestSearchMarker is off by default. The selected authorized
  chest result receives a temporary fog-respecting marker during its existing guide.
  Range/access/life-state/open-chest checks are reused each frame. No background chest
  scan, persistent pin or new RPC. Texture uploads occur on position changes, not per
  frame. Owned layer, texture, toggle and listeners are removed on unload, and the
  shared composite redraw removes stale pixels. Another mod's layer/toggle remains.
- Validation: existing regression suites passed, plus 577 localization assertions,
  34 map drawing/clipping/cleanup assertions, 12 controller lifecycle assertions and
  106 input/UI assertions using linked production code, installed Harmony and doubles.
  Debug/Release builds passed with zero warnings/errors. Installed BepInEx migration
  checks passed. Release metadata was checked for VersionCheckOnly/Minor and both
  embedded catalogs. Diff whitespace checks passed; client deployment hash matched.
- No new in-game test performed and no active game/server was interrupted. Press F6
  to load the client build. Test English/Turkish navigation, crafting search typing,
  Mouse3 dial, an assigned controller button's hold/release and settings toggle,
  marker selection/expiry/access loss and another F6 reload. Test the release version
  handshake with real peers separately; do not treat simulated lifecycle tests as
  proof of native rendering, controller hardware or multiplayer behavior.

## Required Jotunn dependency and typed APIs, 2026-10-21

- User approved depending on Jotunn after confirming the mouse-dial fix. Ragnarok
  now declares a hard BepInEx dependency on Jotunn 2.30.2+, with an explicit runtime
  check for ScriptEngine on clients and servers. This supersedes the earlier
  optional-integration notes below. InputManager/ButtonConfig/GUIManager calls are
  typed; private access remains only for unload cleanup. Mouse bindings retain
  direct input polling, and shared settings/server authority are unchanged.
- Builds discover one installed Jotunn DLL recursively or use JotunnPath. Copy-local
  is disabled. Windows/Linux setup download a SHA-256-pinned 2.30.2 archive only
  when no Jotunn installation exists. Existing copies are preserved. Sources and
  license are recorded in DEPENDENCIES.md; releases require separate installation.
- Debug/Release builds passed with zero warnings/errors; all existing regression
  suites and 106 input/UI assertions passed. Missing/older dependency checks,
  three simulated unload/reload rounds, shared blocks, rebinding and mouse keys
  are covered. Verified release metadata includes the required minimum version
  and typed assembly reference, and Jotunn is absent from plugin output. Invalid
  JotunnPath was rejected with a clear build error. Windows doctor found 2.30.2.0.
- PowerShell scripts and Linux helper syntax passed; downloaded archive version,
  checksum and plugin layout were checked. Full setup was not run against the
  active game, and Linux setup/runtime received no execution test. The Debug DLL
  was deployed to the isolated client and verified by matching SHA-256 hashes.
- No new in-game test performed. F6 reload and verify F10, Mouse3 weapon-dial
  hold/release, crafting-search typing and another reload with no stuck input.

## Weapon-dial mouse binding after Jotunn integration, 2026-10-21

- User reported the dial no longer opening after reload. The saved key is Mouse3.
  The integration had changed mouse shortcuts from legacy Unity Input polling to
  Jotunn/ZInput actions. Inspection of the installed ZInput.KeyCodeToPath shows
  Mouse3 and Mouse4 converted to the Input System Forward and Back enum entries;
  those are a different input path from the key picker's direct legacy input.
- Mouse0 through Mouse6 now retain the original direct polling for down/held,
  without creating a converted Jotunn action. Keyboard bindings continue through
  Jotunn. Switching between mouse/keyboard removes old registrations, and dial
  modifiers, context gates and release-to-select behavior remain in the caller.
- All regression suites passed, including 105 input/UI checks covering all seven
  mouse bindings, held/released states and keyboard-to-mouse-to-keyboard rebinding.
  Windows Debug build/deploy passed with zero warnings/errors. No in-game test
  performed; F6 and hold the existing side mouse button to verify the fix.

## Optional Jotunn input/UI integration, 2026-10-21

- Settings toggle and weapon-dial keys register through the installed Jotunn
  InputManager when available. Configured keys, modifiers and dial hold/release
  behavior are preserved; no new gameplay setting or RPC was introduced.
- Modal windows and registered focused fields share one Jotunn GUI input-block
  request. The settings toggle remains available to close its own window. GUI
  recreation closes settings; unload releases owned input actions, button entries,
  config listeners and the GUI event listener. The adapter is client-only and
  optional, with native fallback and diagnostics for unavailable APIs.
- Checked the installed Jotunn 2.30.2 metadata and native ButtonAction getter.
  The new `tests/Ragnarok.Jotunn.Tests` links the actual adapter and UI guards,
  uses the installed Harmony with Unity/Jotunn doubles, and passes 82 assertions:
  three unload/reload rounds, key rebinding/None, partial registration failure,
  absent Jotunn, name collisions, input initialization, scene replacement, E key
  suppression, settings toggle access and preserving another mod's input block.
  Windows `dev.ps1 test` runs this suite along with the existing regression suites.
- Windows Debug and Release builds passed with zero warnings/errors; existing
  regression suites and the input/UI suite passed. Debug deployment to the isolated
  development client's BepInEx/scripts was verified by matching SHA-256 hashes.
- No in-game test performed: the development client is running, and the startup
  smoke script requires it closed. The automated reload rounds use test doubles,
  not an actual F6 reload. After F6, check F10 open/close (including while editing),
  key rebinding, `leather` in crafting search, weapon-dial hold/release with modifiers,
  movement during the dial, another Jotunn window, and returning to the main menu.
  Then reload again and confirm no duplicate-button warning or stuck input.

## Craft search input with Jotunn, 2026-10-21

- Typing E in the crafting search could close the inventory. Inspected the installed
  game and Jotunn assemblies: InventoryGui closes on the Use button; Jotunn's
  GetButton/GetButtonDown/GetButtonUp postfixes replace the result using reverse
  patches, bypassing Ragnarok's prefix suppression.
- Added a last-priority postfix to reapply the existing typing/modal input guard.
  It covers both crafting styles and other registered Ragnarok text fields, without
  adding a dependency. The patch uses the plugin's existing Harmony unload cleanup.
- Windows Debug build passed with zero warnings/errors. A temporary .NET Framework
  harness compiled the actual BlockGameButtonsPatch against the installed Harmony:
  the old prefix-only version failed on Use with a simulated Jotunn postfix; the
  fixed version passed 60 checks across button down/up/held, normal input, typing,
  modal/closing-frame suppression, focus release and patch unload.
- No in-game test performed. After deployment, press F6 and type `leather` into
  crafting search in each style; E must stay in the field. Click outside it and
  verify E/Tab close normally. Check chat, console and crafting amount entry too.

## Delete by drop and confirmation window, 2026-10-21

- User reported Delete not working and asked for drag-to-delete with a confirmation
  window. Root cause, found with temporary in-game click logging: the vanilla-style
  panel's `Dispose` runs every frame while the Ragnarök inventory is shown, and it
  called `ItemTrash.Reset()`, which cleared the two-press arm (and later closed the
  popup) on the next frame. Teardown no longer resets; only the Delete button's own
  `OnDisable` (inventory closed, panel rebuilt) and plugin unload do. Separately,
  Valheim moves items by click with the mouse (drag-release is touch only), so a
  release over Delete never clicked the button.
- The Delete button (both inventory styles) now also catches a release over it
  while an item is held. With `Confirm` on, Valheim's `UnifiedPopup` Yes/No window
  asks (cover background on); the two-press arm remains only where no popup
  instance exists. The answer re-checks that the item is still in its inventory
  and deletes at most the asked amount; closing the inventory or rebuilding the
  panel pops the window.
- Tested in game (development install, Ragnarök inventory, joined to the local
  dedicated server): click Delete with an item held → popup above the inventory
  (raycasts hit `UnifiedPopup/.../FullscreenBlocker`); Yes deleted stone and iron
  and cleared the cursor; No kept the item held; press on an item and release
  over Delete opened the popup and Yes deleted it. Not tested: Esc while the popup
  is open, split amounts, chest items, the vanilla-style panel. Diagnostics removed
  afterwards; build passed.
- Follow-up: the user found the full-screen `UnifiedPopup` too heavy. Replaced by a
  small card built from the Delete button itself (sprite, outline, label font) so
  it matches either inventory style, opening above the button in the lower half of
  the panel and below it in the upper half. A click outside it, Escape, closing the
  inventory or a rebuild cancels it. Build passed; deployed. Not yet seen in game.

## Movement while typing in searches, 2026-10-20

- User reported the character moving while typing in the crafting or chest search.
  Static review did not find why the existing `ZInput.GetButton*` block missed it
  (installed Steam and development assemblies are identical; the methods are not
  inlining candidates; no other installed mod patches the input path).
- Defense in depth: `PlayerController.TakeInput` and `Player.TakeInput` now return
  false while a Ragnarök field, or any focused field selected in the EventSystem,
  has focus. Movement/camera controls are zeroed and character actions stop
  regardless of how keys are read. Game buttons keep the narrower rule so Valheim's
  chat/console are unaffected.
- Build and `dev.ps1 test` passed; deployed to the Gale profile's `BepInEx/scripts`.
  Not yet tested in game.

## Inventory-first choices, Delete, vanilla provisions and Better Archery, 2026-10-20

- Where Valheim picks one of several items (Smelter/CookingStation/Fermenter
  `FindCookableItem`, `Player.GetFirstRequiredItem` for "any one ingredient"
  recipes), the choice now runs over the carried inventory first with vanilla's
  order and falls back to nearby chests only when nothing carried qualifies.
  Payments already spent carried stock first (`WithdrawalPlan`); unchanged.
- Delete button in both inventory styles destroys the item held on the cursor
  (split amount), with optional two-press confirmation. Local only; no network
  authority is involved beyond Valheim's own container ownership.
- Vanilla HUD provision row moved beside the Forsaken power icon (sized to it,
  clamped to 60-100% of the hotbar) with key labels at 60% size; the old
  placement remains as `HUD.Provisions.VanillaPlacement = AfterHotbar`.
- Better Archery's own vanilla-grid arrow slots are skipped while a Ragnarök
  character panel shows them, removing the duplicate.
- Windows Debug/Release builds, `dev.ps1 test`, `check-config.ps1` and the config
  harness passed. A static Mono.Cecil check confirmed every new patch target and
  Harmony parameter name in the installed Valheim and Better Archery 2.0.5. Sharing
  archive rebuilt. No in-game test: the new layout, deletion and station choices
  have not been seen or exercised in a world.

## Server-locked QoL policies and audit fixes, 2026-10-20

- Chest search, nearby chests (enabled, crafting, building, stations, range) and
  death-reclaim auto equip are always defined and locked by a modded server,
  independent of `LockSettings`. Both radii are server maximums with a separate
  personal range that can only lower them; the personal preference stays on disk.
  The second hotbar is now off by default.
- Audit fixes: saved slot items load regardless of admission rules; the Better
  Archery quiver moved from the invalid (8, 64) to (0, 66) with live/saved migration;
  slotted upgrades with a full bag recreate into the freed slot; reload while
  server-locked keeps the personal value; craft-amount reset/unload during a batch
  keeps the original defaults; server hot reload resyncs peers and disconnects
  release their RPC registrations; recipe details, requirements and chest counts
  are throttled/cached for the UI while resource spending still checks live stock.
- Windows Debug/Release builds, `dev.ps1 test` and `check-config.ps1` passed. Local
  harnesses against the installed BepInEx/Valheim assemblies passed: config
  policy/reload/disconnect, craft-amount reset/unload, a static (Mono.Cecil) check
  that the installed `Inventory.AddItem(string, ...)` IL matches the upgrade
  transpiler, and native position-gate acceptance of the relocated quiver slot.
- Not run: `smoke.ps1` (development Valheim was in use), so the new
  `PolicyRuntimeChecks` and the transpiler have not run inside the engine. No
  character/world, multiplayer, Better Archery relog or full-bag upgrade test was
  performed.

## Craft progress before shared-chest authority, 2026-10-20

- User clarified that the progress bar finished without putting an item in their
  inventory; whether ingredients were spent was not confirmed. The earlier change
  addressed a notification only. Inspection of installed vanilla code showed that
  OnCraftPressed starts progress without checking chest authority; DoCrafting then
  returns without output when the final owned-resource check fails.
- Added a progress-phase authority gate: check owned stock without sending requests,
  request remote ingredients only on a shortfall, and hold progress until the
  checks pass. The native craft still checks and consumes materials normally.
  After eight seconds without a handoff, cancel with an explicit timeout message.
  Cancel/unload clears wait state. No resources are spent by the gate.
- The running game was located in Gale's Cem profile; prior development deployments
  did not update that profile. Its log has no matching Ragnarok withdrawal exception.
  Other mod errors are present and are not established causes of this report.
- Windows Debug/Release builds and existing regression suites passed.
  Sharing archive rebuilt. No real-peer/in-game validation of
  this fix performed; acceptance steps are in NEARBY-CHESTS.md.

## Shared-chest message after successful crafting, 2026-10-20

- User reported successful crafts displaying the pending ownership message while
  the other player was idle. Resource enumeration requested remote ingredient
  chests even when owned stock was sufficient, and scope disposal treated those
  requests as blocked crafting.
- Record the recipe requirement result on the enclosing crafting scope, including
  nested checks. A passed check suppresses the pending notification for that craft;
  post-payment UI checks cannot reset it. Failed checks retain the handoff warning.
  Chest authority, permission checks and withdrawal behavior are unchanged.
- Windows Debug/Release builds and regression suite passed; sharing archive rebuilt.
  No in-game test performed. Real-peer regression steps are in NEARBY-CHESTS.md.

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

## Remove portrait provision row, 2026-10-21

- Removed the carried provision shortcut row under the Ragnarok portrait and its
  unused renderer/root. Active food remains beside Forsaken power. Party layout
  no longer reserves space for the removed row. Inventory slots and provision
  shortcuts retain their existing behavior; the optional vanilla HUD row remains.
- Updated the affected setting descriptions. Windows Debug build/deploy passed
  with zero warnings/errors; no in-game visual test. Client F6 reload required.

## Combat status indicator, 2026-10-21

- Add a presentation-only ongoing status tile: a borrowed native sword icon,
  localized In combat label, Active while engaged and seconds remaining during
  the mount quiet period. Reuse the same player combat predicate as summoning.
  The tile indicates player combat only; mount combat and other restrictions still
  apply independently.
- Ragnarok HUD places it with ongoing effects near the character name. Vanilla
  HUD gets the same frameless tile near the top center/below the compass. No actual
  status effect, stat modifier, item, save data or network rule is added. All owned
  UI and static status state are cleared on unload/scene replacement.
- Windows Debug build/deploy and regression/localization checks passed. No in-game
  visual test was run; F6 client reload is required. Server restart is unnecessary.

## Persistent mount identity correction (0.5.3), 2026-10-21

- The user's screenshot showed the generic alive/tamed/saddle rejection. New
  diagnostics established that the old ledger's `1:8777` resolved to `wood_roof_45`
  after restart. A raw ZDO ID is not a durable mount identity in this game version.
- Version-2 bonds use an owner-stamped unique string on the original animal's ZDO;
  cached IDs never resolve an unmarked bond. Server world-load indexing handles
  ID remapping and refuses duplicate markers. Legacy rows require rebinding or
  explicit guarded exact-name recovery. Marker saves use the native server-world
  save method; the public profile-save helper was found to require a local player.
- With the user's confirmation of the name Moose, guarded recovery found exactly
  one tamed, saddled `Moose` prefab (its species display name; no custom pet name).
  It remained at (-57.68, 29.35, 7.66), with 1000/1000 health. No movement, healing,
  replacement or character action was performed during recovery.
- A normal server save/restart preserved marker
  `1b75d39904aa4117a5a883c996fe9023`. Restarted 0.5.3 resolved the same Moose from
  the marker at ID `1:5144`, with unchanged position, health and saddle. This proves
  native marker persistence; synthetic regression checks separately cover ID
  reassignment and rejection of a different object at the former ID.
- The owner now requests a missing distant ZDO before executing a command, with
  a bounded wait and no replayed movement. A protocol handshake explicitly rejects
  older clients instead of leaving them to time out on unsupported RPCs.
- Debug/Release builds, metadata and all regression suites passed. Client Debug
  deployed for F6; Release installed on the live isolated RagnarokTest server.
  Full Ashlands summon/arrival still needs the user's in-game retry. No additional
  test client was launched because the user requested keeping their client running.

## Dedicated mount handshake correction (0.5.1), 2026-10-21

- The live 0.5.0 server loaded Ragnarok successfully, but the mount handshake
  required resolving the peer's Player prefab via ZNetScene.FindInstance. Native
  ZNetScene only streams around its own reference position; a connected player
  can have an authenticated ZDO without a loaded Player on a dedicated server.
- Advertise the mount service independently of the streamed Player. Bind and
  summon authority uses the connection's character ZDO, owner and profile ID.
  The requesting client supplies fresh, nonce-matched physics/action checks
  before and after windup; server constraints, wards, combat, bond identity and
  animal-owner execution remain authoritative. Missing player records now return
  a loading explanation instead of silently dropping requests.
- Windows Debug/Release builds, both eager metadata checks and all regression
  suites passed, including rejection of non-finite, diagonal-out-of-range and
  excessive-height arrival proposals. Client Debug DLL deployed for F6 reload.
  Multiplayer summon/placement still needs a player test; no test character or
  movement was injected into the user's session.
- Sent normal Ctrl+C to the dev server's console, verified its orderly shutdown
  and new `_main.20.ok` save at 19:29:31, then installed the verified Release DLL.
  Restarted RagnarokTest from that save; native logs confirm Ragnarok 0.5.1,
  `Mount server ready; world-scoped bond store loaded`, `Game server connected`
  and UDP 2456/2457. Client F6/reconnect and an actual bind/summon remain to verify.

## Riding HUD placement, 2026-10-21

- Move the native mount name/health/stamina panel above Ragnarok vitals, leaving
  clearance for the equipped-weapon title. Use measured HUD bounds and scale.
- Fall back to right-center when the build task/equipped card overlaps that slot,
  pickups or skill messages use it, or the slot would crowd the reticle/screen edge.
  Layout runs after the vitals, build card and notification updates.
- Preserve native values and saddle support. Restore the original panel position
  and scale on dismount, HUD-style change, scene replacement and plugin unload.
- Windows Debug build/deploy passed with zero warnings/errors. No in-game test:
  the user previously requested leaving the running client alone. F6 reload is
  needed; check riding, equipping the hammer, dismounting and HUD scale changes.

## Mount companion service and controls, 2026-10-21

- Implemented server-owned bonds, authenticated requests, repeated safety checks,
  cooperative owner movement/acknowledgment, combat stamps/cancellation, cooldowns,
  Jotunn keys, hover hints, settings status/release, wave and original whistle audio.
  Persistent source-ward checks use nearby ZDO sectors even when the ward is unloaded.
  Arrival updates the patrol point so animals do not walk back to an old location.
- Final Windows Debug/Release builds passed with zero warnings/errors. All
  regression suites and both eager plugin-metadata checks passed. Debug DLL/PDB
  deployed to the isolated client's BepInEx/scripts; F6 is required to load them.
- The extended runtime runner now covers service binding, unloaded summon,
  cooldown, combat cancellation/rejection and release. It was NOT run: the client
  had reopened, and the user explicitly requested leaving it running and skipping
  the isolated test. The full new service/input/audio/ward/multiplayer flow is
  untested in-game. Earlier transport-only results below do not cover these changes.
- The live server DLL was Windows-mapped and refused replacement. Its original
  hash was verified against the backup; no server process was stopped. Release
  files and a guarded apply.ps1 are staged externally under
  `D:\RagnarokDev\server-updates\Ragnarok-0.5.0`. Save/stop the dev server, apply the
  update, then start it before trying these controls on the dedicated server.

## Mount transport prototype, 2026-10-21

- Windows Debug build/deploy passed with zero warnings or errors. Regression
  checks passed, including exclusive bonds, replacement/release, atomic disk
  persistence and rejection of invalid or cross-world records.
- Isolated native runtime check passed at
  `D:\RagnarokDev\mount-checks\20261021-182501`. A tamed saddled lox moved while
  loaded, streamed out in a distant sector and returned with its original ZDO ID,
  saddle, name and health. Exactly one matching animal existed after return.
- The test used its own temporary character/world and closed its client on
  completion. The running dedicated server was left alone.
- This proves local authoritative loaded/unloaded transport only. Remote owner
  transfer, multiplayer authority and normal bind/whistle controls remain
  outstanding; the full summon feature has not received an in-game test.

## Hover keys match Forsaken Power, 2026-10-21

- Changed hover keycaps from the general button sprite to the same HudKeySurface
  glass face and HudKeyEdge at 45% bone opacity used by PowerEmblem. Key text uses
  the same 90% bone color; compact size and normal action labels are retained.
- Debug build/deploy passed with zero warnings/errors. No in-game visual retest
  was performed. Load with F6; no server restart is needed.

## Forsaken tooltip duration and hover wrapping, 2026-10-21

- Confirmed the installed ItemStand.GetHoverText reads its power template's
  SE_Stats.GetTooltipString. This supplies the flavor and stamina modifier lines,
  plus the raw template TTL (300), while the configured active duration is 600
  seconds. Server/client DurationMinutes are both 10.
- Power template tooltip previews now replace only the native TTL line with the
  configured next-activation duration formatted by StatusEffect.GetTimeString.
  Active effects retain their instance TTL text; shared templates are not mutated.
- Hover prose/info lines wrap within 420 UI units and expand vertically; object
  names and key/action rows stay compact. Chest peek follows the resulting bounds.
- Debug build/deploy passed with zero warnings/errors; eager metadata validation
  passed. No in-game tooltip/wrapping retest was performed. F6 loads the client
  presentation change; no server restart is needed.

## Themed hover action labels, 2026-10-21

- Split native hover text into an object heading, normal 12-point body/action
  labels and themed dark keycaps. Hold is a separate muted hint; modifier names
  are shortened and placed before their main key. Pet, Rename and mount actions
  no longer inherit the heading's larger bold font or incoming bold/size tags.
- Preserve native bindings, sprite glyphs, warning colors, condition lines and
  mod descriptions. Reuse row/key objects when only label/timer text changes;
  source atlas changes update copied glyph assets. Prompt bounds continue to
  position the chest preview. All owned UI is destroyed and native alpha restored
  on style/scene changes and unload. Interaction/input behavior is unchanged.
- Debug build/deployment and eager metadata validation passed with zero build
  warnings/errors. All regression suites passed, including new parser checks for
  native pet/rename text, hold chords, modifiers, gamepad glyphs containing plus,
  alternative keys, CRLF/blank lines, mod colors/text and reusable timer rows.
- No in-game visual/gamepad/rebind retest was performed. F6 loads this client-only
  change; no server restart required.

## Riding progression 0.5.4, 2026-10-21

- Added server-controlled linear 10%-to-zero maximum-stamina cost and
  1.2-to-0.6-second whistle windup across Riding 0–100. The owner reports bounded
  finite native skill/stamina snapshots during both arrival checks; the server
  recomputes cost, requires sufficient stamina and authorizes completion only
  after confirmed movement. Failed/cancelled/unconfirmed summons have no receipt.
- Default XP factors are 0.5 for the first bond per player/animal and 0.25 for a
  successful summon at most every 300 real seconds. Atomic world-scoped server
  history preserves eligibility across release, F6, reconnect and restart.
- Ordered persisted completion receipts retry until an authenticated matching
  acknowledgment. Owner-side application records a per-world sequence in character
  custom data before native stamina/XP calls. This handles duplicate packets/F6,
  but is not an atomic transaction across abrupt character/server save crashes.
- Companion settings/hover/calling text expose Riding, condition, cost and windup;
  tiers 25/50/75/100 add bounded native particle layers with extra audio muted.
  Unity's installed ParticleSystemModule is referenced, never copied into releases.
- Debug/Release builds passed with zero warnings/errors. Both eager metadata
  checks and all regression suites passed. New pure checks cover formula endpoints
  and monotonicity, malformed snapshots, first-bond history, restart/clock/cooldown
  boundaries, ordered receipts, duplicate/out-of-order acknowledgments, bounded
  queues, malformed/cross-world files and failed-write rollback.
- Deployed Debug client scripts and installed Release on the dev server through
  the existing graceful save/backup/restart helper. Protocol 3 rejects outdated
  requesters and connected mount owners; clients must F6/reconnect.
- No live progression test was performed. Still check exact cost, insufficient
  stamina, cancellation, XP/cooldown/rebinding, packet retries, reconnect/F6,
  remote owners, skill 0/25/50/75/100, tooltip layout and visual/audio feel in-game.
  The user requested leaving Valheim open and skipping the isolated test.

## Fixed mount panel position, 2026-10-21

- Keep the compact mount panel above the player vitals whenever those vitals are
  available. Hammer/build cards and notifications no longer move it to the right.
  Screen-edge clamping remains; right-center is used only if vitals are absent.
- Debug build/deploy passed with zero warnings/errors. No in-game hammer retest
  was performed; reload the client with F6. No server restart required.

## Compact Ragnarok mount panel, 2026-10-21

- Replaced the relocated vanilla readout with an owned 280-by-52 frameless HUD:
  compact native name/status line, red health and yellow stamina tracks, current
  and maximum values, matching fonts and outlined text. Follows HUD scale.
- Reads the ridden Sadle/Character directly; no health/stamina/network writes or
  new synchronization settings. Native update/control hooks keep running while
  a reversible CanvasGroup masks the vanilla presentation. Dismount, HUD switch,
  scene change and unload destroy owned UI and restore original native alpha.
- Retains above-vitals positioning, weapon-title clearance and occupied-space
  right-center fallback. Render-time placement avoids native layout interference.
- Debug build/deploy, eager metadata checks and all regression suites passed.
  No in-game visual, HUD-switch, hammer or modded-saddle retest was performed.
  Load with F6; this client-only presentation needs no server restart.

## Mount readout visible-bounds placement, 2026-10-21

- User screenshot showed the riding readout below vitals. Placement now measures
  active visible native graphics instead of assuming the container rect encloses
  the name and bars. Screen fitting/clamping uses those same visible bounds.
- Reapply placement through Canvas.willRenderCanvases after animation/layout;
  keep the existing above-vitals and occupied-space right-center fallback.
  Dispose unsubscribes the render callback and restores native position/scale;
  dismount/owner changes restore state without removing the callback.
- Debug build/deploy passed with zero warnings/errors; eager metadata validation
  passed. No live mounted, hammer-equipped or HUD-scale retest was performed.
  Client-only change; no server restart required.

## First-entry unarmed key guide, 2026-10-21

- Native fist hints can activate from targeting during entry. The custom guide
  now requires an equipped inventory weapon for combat rows, sharing the hotbar
  docking predicate and clearing unarmed rows without the normal scan delay.
- Native guide suppression now runs after KeyHints.Awake/UpdateHints as well as
  Hud.LateUpdate. It tracks the KeyHints owner, restoring the previous CanvasGroup
  before attaching to a replacement UI instance. Existing groups retain their
  original alpha, and owned groups are removed on restore/unload.
- Debug build/deploy and eager metadata validation passed with zero build
  warnings/errors. No in-game first-entry, reconnect or F6 retest was performed.
  This client HUD change requires no server update.

## Distant mount stale alert fix, 2026-10-21

- Removed the persistent alert-bit rejection from mount readiness. The bit can
  represent fear or a stale unloaded state; it does not identify an active fight.
  Loaded AI must be alerted with a living hostile target to block movement.
  Recent combat timestamps, rider, health, tame, saddle and owner checks remain.
- Debug deployment, Release build, both metadata checks and all regression
  suites passed. No live calm/hostile/unloaded summon retest was performed.
- Installed the Release DLL on the dev server using the existing graceful
  shutdown/save and backup helper, then restarted RagnarokTest.

## Combat false-positive checks, 2026-10-21

- Removed passive Player.IsTargeted and unrelated nearby alerted hostiles from
  continuous engagement. Hostiles must be alerted and actually target this player.
- Removed the native time-since-attack fallback: it starts at zero on spawn;
  confirmed attack hooks already stamp the shared combat timestamp.
- Debug build/deploy passed with zero warnings/errors; all automated regression
  suites passed. Native method inspection confirms passive targeting and timer
  initialization behavior. No live creature/indicator test was performed.
- Client scripts updated; running dedicated-server binary was not replaced.

## Picked plant hover name, 2026-10-21

- Fixed the regrowth hover replacement displaying a raw item localization token:
  resolve Pickable.GetHoverName through native Localization before adding the
  timer. Respawn timing and world state are unchanged.
- Windows Debug build/deployment passed with zero warnings/errors. The user's
  screenshot identifies the affected branch; no in-game retest was performed.

## Mount arrival animation, 2026-10-21

- Windows Debug build and deployment passed with zero warnings/errors.
- Arrival presentation uses native spawn/skill-up assets and animates only the
  original animal's visual child after confirmed movement. Pending reveals and
  owned effect objects are disposed on unload/scene change; renderer state and
  scale are restored on completion or mounting.
- Deployed to the isolated client's ScriptEngine scripts directory; F6 loads it.
  The existing server protocol is unchanged and no server restart was needed.
- No in-game visual test was performed; the user requested keeping Valheim open
  and skipping the isolated test. Burst appearance, timing and compatibility with
  modded mount visuals require a live check.

## Session wrap-up checks, 2026-10-20

- Final Windows Release build passed with zero warnings/errors. All regression
  suites passed, including configuration numeric input/drag-step tests. The latest
  Debug DLL is already deployed to the isolated development scripts directory.
- User confirmed the centered status labels. Multiplayer configuration locking
  and expanded nearby-storage access still require real-peer validation.
