# Change record

## Unreleased — Rune & Iron and Linux development, 2026-10-19–20

Accumulated work after `9472bf2`. The plugin remains version 0.3.0; this is not a
published release. Later refinements supersede earlier trials in the dated
[design history](docs/RUNE-IRON-HUD.md).

### Development environment

- Native Linux setup/build/deploy/play/watch/logs/doctor/test/package/smoke commands
  alongside Windows. Support native and Flatpak Steam, verified/pinned tools and
  ignored machine paths; keep SDK/game/saves outside Git and the ordinary Steam copy.
- Offline local-save guard for the copied game, verified before launch, prevents
  development sessions using Steam Cloud. LocalSaves and game assemblies are never
  packaged. Documented prerequisites, commands and limitations in `docs/LINUX.md`.

### Shared styling and HUD

- Keep status-effect labels always visible, tighten tile and label spacing, and
  align the strip beneath the character name. Retire the label toggle shortcut/settings.

- Increase the default nearby-storage/Quick Stack radius to 100 metres and its
  configurable maximum to 500 metres. Loaded storage and existing access rules
  still apply; sorting remains scoped to the open chest.

- Add exact numeric input beside configuration sliders, with unit captions,
  invalid-input rejection and range clamping. Snap dragging to setting-specific
  increments, including five-point percentages and ten-item capacities.

- Fix integer config sliders throwing InvalidCastException and allow fractional
  values on wide float ranges. Compact config text/rows/window, add collapsible
  navigation groups and a sidebar toggle, and guard edits/default resets against
  server locks. Mark the server lock policy itself read-only on locked clients.

- Remove the regeneration-rate text inside the health bar and its unused estimate.

- Refresh Skills, Compendium, Trophies and Achievements with the same rounded
  charcoal frame, tabs, selection cards, parchment text and gold highlights as
  the character shortcuts. Add capsule scrollbars and matching Close keycap;
  replace the diagonal ember scene with a quiet rounded glyph preview.

- Refresh the character shortcut panel above crafting with the current rounded
  charcoal frame and cards, parchment typography and gold active-toggle borders.
  Preserve native shortcut actions, tooltips and inventory opacity.

- Replace the build-picker skin with a reference-inspired category sidebar,
  pooled icon tiles, craftable counts and searchable materials in a
  compact centered window. Keep native building selection and authority. Remove
  the experimental menu quickbar, pinning controls and number-key shortcuts.
  Remove the right detail panel, widen the grid and place compact density steppers
  beside the piece count. Restore native station-upgrade arrows, compact the header
  with matching action heights and select repair through the top-left hammer. Shrink the window to 1120 by 580, remove permanent piece names
  and restore native hover readouts, controller focus and piece-button clicks.
- Theme the native hammer build picker with dark rounded surfaces, gold selection
  outlines and matching fonts, retaining search, tags, favorites and native input.
- Add persistent row/column controls below the build grid, inventory-style piece
  slots, an outer menu frame, capsule scrollbars and replacement tab rules; remove
  the native material divider and its layout gap.
- Compact themed building/terrain/cultivation task card above the vitals and
  hotbar, mirroring native task, resource, snapping and station readouts. Smaller
  icons/type and wrapped requirement tiles keep the center aiming area clear.
- Licensed Cinzel, Barlow Semi Condensed and Noto Sans Runic fonts embedded with
  sources, hashes and license texts. Native item icons retained.
- Rune & Iron colors, rounded surfaces and outlined text; stronger world-label
  outlines/shadows for bright environments. Smaller compass with fading ends and
  a separate heading/biome/day footer.
- Portrait/food alignment, backed circular borders and shield artwork. Empty food
  circles have muted transparency/dashed rims and no fabricated cooldown dash.
- Smoother muted Forsaken shortcut chip, outlined labels and an introductory
  unacquired-power state without an unavailable activation prompt.
- Shared 58% glass on power, food, status, hotbar, notifications, minimap and weapon
  dial surfaces. Optional HUD backdrop plating removed; legacy keys hidden/ignored.
- Read-only average HP/s estimate inside health, using food and positive status
  healing, zero at full health. Native healing timing and values are unchanged.
- Translucent minimap and outlined labels; weapon dial shows type/quality/name,
  damage or block information, durability and slot condition indicators.

### Inventory, chest and equipment

- Bring search shortcuts/debounce, collapsible armor filters, sorting, bounded
  craft quantity and bag/nearby material counts to vanilla crafting using native
  fonts and button art, retaining its recipe list, descriptions and actions.
- Reduce inventory/crafting opening work with pooled visible recipe rows, cropped
  icon conversion instead of full-atlas copies, and no redundant native recipe
  rebuild when the opening list already has Ragnarök filtering/sorting applied.
- Slot-aware tombstone recovery restores previously worn gear into empty
  character slots before filling the bag, and restores saved provision positions
  for new deaths. Use reclaims directly when the combined inventory fits within
  carry weight, retaining native access/ownership approval and overflow handling.
- Matching rounded inventory/chest slots, muted empty outlines, category tabs and
  unified name/armor/weight headers. Supplied shield outline and rounded weight meter.
  Removed extra inventory hotbar strip/category legend; white keys, muted actions.
- Category tabs scope Quick Stack and its button label; assigned/equipped/quest
  items remain protected. Sorting labels are Name, Kind and Weight.
- Stacked Worn, Provisions and optional arrows in the side panel, with stats and
  muted blue set summary below. Full set names, separated piece counts, locked/active
  native effects and detailed hover cards without enlarging the panel.
- Equivalent headers, categories, Quick Stack and side dashboard in vanilla style,
  using native artwork/fonts/slots. Native inventory interactions remain intact.
- Inventory/chest/side-panel/crafting opacity defaults to 85%, with lighter inner
  fills. Exact former defaults migrate; unrelated custom values remain intact.
- Character/food/status HUD hides with inventory. Crafting remains usable with an
  open chest; the old ChestFocus hiding rule is removed.

### Tooltips and messages

- Consistent delayed, edge-clamped item/status/set hover cards with quality stars,
  native properties and supported Shift comparison deltas.
- Translucent semantic popup cards, outlined system lines, death/discovery/day/power
  banners and skill milestones. Crimson death diamond restored; failure preview's
  missing localization token fixed.
- Compact skill pill sizes to content with spaced label/right-aligned name and level.
  Default placement docks above the complete vitals panel.
- UI-only `ragnarok_popup` samples, `all`/`clear`, F5-paused queue and command cleanup.
  Previews do not change player resources/progression or native logs. The user
  confirmed sequential playback works.

### Crafting

- Smoked interface within native panel dimensions: Craft/Upgrade tabs, native
  artwork, recipe list/details, station requirements and readiness feedback.
- Debounced, case-insensitive multi-word recipe/ingredient search with slash focus,
  clear/reset, and station/world filter reset.
- Station-relevant categories, separate shields/meads, dominant health/stamina/eitr
  food filters, armor slot filters and independent Craftable only. Name/Craftable-first
  sorting preserves native selection indices; vanilla retains its native sorting.
- Native craft/upgrade/style/quantity/progress and nearby-storage paths retained.
  No recipe pinning or new gameplay/network protocol.
- Native tooltip text grouped into physical/elemental damage, blocking/protection,
  attack/resource use, nourishment, equipment and effects. Preserve quality/world
  level, skill ranges and unknown effects, with italic flavor and muted headings.
- Removed stat cards; requirement tiles reduced from 86 to 60 layout units, giving
  descriptions 70 more vertical units. Names/icons/have-needed counts retained;
  larger ingredient lists retain paging.
- Flush repair extension shares a continuous background/border. It starts hidden
  and appears only at a repair-capable station with an available native action;
  fully repaired gear leaves no disabled extension. Fitting reserves visible space.

### Lifecycle and verification

- Owned graphics/fonts/hover cards/input registrations/preview commands and native
  overrides follow rebuild, style-switch and unload cleanup.
- Added offline checks for crafting search/classification, grouped recipe details,
  repair geometry and legacy backdrop visibility alongside existing regression tests.
- See [verification notes](docs/VERIFICATION.md) for results. User screenshots guided
  iterations; final agent-run visual, gameplay and multiplayer acceptance remains
  pending. Builds and offline tests do not establish in-game correctness.
