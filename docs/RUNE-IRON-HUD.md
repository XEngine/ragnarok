# Rune & Iron HUD

## Current implementation, 2026-10-20

See [the consolidated change record](../CHANGELOG.md) and [README](../README.md)
for the current feature overview. The dated entries below preserve the design
iterations; later refinements supersede earlier values and layouts.

- Cinzel names/headings, Barlow text/numbers and Noto Runic badges; native item icons.
- Smaller compass with fading ends and separate bearing/biome/day footer.
- Character/food alignment, backed circular outlines, muted dashed empty food slots
  and no fabricated cooldown dash. Character/food/status HUD hides with inventory.
- Shared 58% glass on HUD fills with outlined world labels; optional backdrop
  plating removed. Inventory, chest, side dashboard and crafting use 95% glass by
  default, independently adjustable through inventory opacity.
- Inventory/chest header, shield and rounded weight meter; category-scoped Quick
  Stack; Name/Kind/Weight sorting; white keys with muted actions. Native artwork
  retained, no separate inventory hotbar strip or category legend.
- Stacked Worn/Provisions/optional arrows in the side panel, stats and set summary
  below. Named sets show a separated worn count and locked/active native effect.
  Vanilla has equivalent functionality in a native-looking side panel.
- Guide-styled item/status/set tooltips, semantic popup cards, center banners,
  compact skill pills above vitals, UI-only console previews, and crimson death diamond.
- Read-only HP/s estimate inside health; translucent minimap and labeled weapon dial.
- Crafting stays visible while a chest is open. Station-aware search/filtering,
  Craft/Upgrade, native quantity/style/progress and compact requirement tiles.
  Recipe details are grouped text, with no stat cards; the repair contour appears
  only when a repair-capable station has a native repair action available.

Builds/offline tests are recorded in [VERIFICATION.md](VERIFICATION.md).
User screenshots and preview feedback informed the iterations; final agent-run
in-game visual, interaction and multiplayer acceptance is still pending.

## HUD system guide: states and tokens

The user supplied the “HUD Kit · Rune & Iron — States & Tokens” image on
2026-10-19 as the shared design reference for the incremental HUD work. The
following describes the intended system, not a claim that every token is already
implemented. Later explicit user refinements take precedence over the reference.

Color tokens from the guide:

- Smoked glass: `#0C0F11`, 58% opacity.
- Bone: `#ECE5D6`.
- Forged gold: `#D4A955`.
- Frost: `#8FC1E8`.
- Moss: `#9BC48A`.
- Health: `#D64A3E`; stamina: `#E8C04A`.
- Eitr: `#9A7BEA`; ember: `#F09A5A`; ash: `#A99F8D`.

Typography in the guide is Cinzel 600 for names, places and powers; Barlow Semi
Condensed 700 for numbers; 600-weight uppercase labels tracked at 0.2em; and
Noto Sans Runic for portrait runes and item sigils. The implementation now embeds
Cinzel 600, Barlow Semi Condensed 400/600/700 and italic, and Noto Sans Runic.
Names/titles use Cinzel, labels use Barlow 600, primary numeric displays use 700,
and character/companion rune badges use Noto Sans Runic. Font sources, static Cinzel
conversion, file hashes and OFL licenses are recorded in
`src/Ragnarok/Assets/Fonts/SOURCES.md`.

Vitals: full bars fade to 40% after four seconds idle; recent-loss segments wait
0.6 seconds before draining; health below 25% has a pulsing outline and screen-edge
vignette; exhausted stamina has hatching until regeneration resumes. The existing
user refinement keeps labels and numbers at full opacity while graphics fade.

Hotbar: distinct default, hover, equipped, cooldown, empty and breaking states.
Equipped uses a gold rim and muted gold/brown fill. Cooldowns use a remaining-time
sweep and number only when a real timed restriction exists. Empty slots have a
transparent fill and dashed rim; breaking slots have muted red feedback.

Status semantics: gold for buffs (Rested), bone for neutral states (Shelter),
frost blue for debuffs (Wet), red for drain/danger (Freezing). Keys are compact
outlined chips. Map/compass semantics: gold diamond for bosses, blue diamond for
portals, bone diamond for home, red dot for hostiles and green dot for allies.
These symbols describe actual targets/states; they do not imply new detection,
party membership or disclosure of hidden player positions.

Use restrained translucent surfaces, fine outlines, rounded slots, clear numeric
hierarchy and sparse rune ornament throughout. No source code or assets were
imported from the supplied guide.

## Implementation history

Visual reference supplied by the user: [Valheim HUD — Rune & Iron](https://claude.ai/artifact/SdHGe26m3b2QPoFd32hrNv#artboard-15dd8cd198dd), accessed 2026-10-18. This is an original Unity UI implementation inspired by the reference; no external source code, images or fonts were imported. The reference does not declare a redistribution license. Existing project assets retain their recorded licenses.

The shared palette uses charcoal, parchment, brass gold, blue eitr/status accents and green ready/companion accents. New default placements put identity, food and companions at top left, buffs under the name, vitals above the bottom-center hotbar, messages above the bottom-left Forsaken seal, and skill toasts on the right. The minimap retains the live vanilla map with a circular mask, a fine brass rim, cardinal letters and wind/heading controls underneath.

`HUD.Character`, `HUD.Power` and `HUD.SkillToast` expose independent placements. Existing vitals, status and notification positions migrate once only if they exactly match the former defaults. Customized positions are retained. Buff labels remain available through the existing B shortcut. The character badge supports a rendered player portrait or the rune glyph through HUD.Character.ShowPortrait.

The companion roster shows up to three other players who share their map position. It is not a party membership system. Loaded characters supply replicated health; distant characters show distance without fabricated health. All settings are local presentation choices. There are no new RPCs, synchronized settings or gameplay authority changes. Skill notifications intercept the existing localized message path; XP and level-up effects remain vanilla.

All new UI objects are owned by the HUD and destroyed when it unloads or switches style. Minimap changes use the existing restoration path. Notification roots are also disposed when the HUD instance changes.

## Verification

- Windows Debug build passed with zero warnings and errors using `powershell -NoProfile -ExecutionPolicy Bypass -File ./dev.ps1 build`.
- Checked the installed game's message flow against the local decompiled reference, including the first skill level's center message.
- No in-game visual test was performed for this change. Valheim was already running; the startup/reload smoke script requires it to be closed.

After development deployment, press F6 and check:

1. Default layout at 1080p, other aspect ratios and HUD scales; check buffs with labels on/off and more than four effects.
2. Food timers, health/stamina/eitr and the character name; inspect companion health nearby and distance-only rows remotely, and verify hidden-position players stay absent.
3. Use **Test notifications** in settings, then trigger a real pickup, unlock and skill gain (including level 1). Confirm the toast appears once and ordinary center messages remain vanilla.
4. Forsaken ready, active and cooldown states and rebound activation key; map pins, wind and biome labels in the circular map.
5. Toggle Vanilla/Ragnarok, hide the HUD, die/respawn, leave/rejoin a world and F6 reload. Check for duplicate objects, stale messages and restored vanilla map visuals.

## Screenshot correction, 2026-10-18

Compared the user's reference crop with their in-game capture. The game log confirmed that the portrait rune U+16B1 was absent from Alegreya and replaced with a space. The rune is now procedural line art. The name has a larger, auto-sized text area with overflow allowed and a profile-name fallback when the networked name is unavailable; its missing appearance has not yet been verified in the corrected game build.

Food now uses circular remaining-duration rings, semantic health/stamina/eitr glyphs, and parchment timers below. Empty food slots remain empty rings instead of rectangles. Buffs use rounded tiles, semantic accents, and timer-only defaults; layout migration version 2 turns labels off once, after which B still toggles them. Companion rows now use rune badges, red health bars and a real visible-player count. A solo player still has no fabricated roster.

Validation: Windows build and development deployment succeeded with zero warnings/errors; git diff whitespace check passed. The supplied screenshot tests the previous version, not this correction. No new in-game visual verification was performed.

## Vitals correction, 2026-10-18

The supplied vitals reference replaces the stacked health/stamina layout with two rounded bars on one baseline: health left, stamina right, numeric labels above and a central gold diamond. Eitr is a thin purple strip below, with the equipped weapon's localized name above. The shared width is 513 HUD units, matching the default action bar's eight slots. Existing custom vitals positions are retained; the old bottom-center margin of 144 migrates to 118 once (layout version 3).

The extra vanilla bar in the screenshot is adrenaline. Its value and maximum are read from Player, drawn as an additional thin row only while positive, and its vanilla root is hidden/restored through the existing HUD cleanup mechanism. Recent-damage lag and low-health feedback are retained. No gameplay values or authority are modified.

Validation: Windows Debug build passed with zero warnings/errors. This correction has not received an in-game visual test; F6 reload is required after development deployment. Check health loss/healing, stamina drain, eitr presence/absence, adrenaline, ShowNumbers, weapon swaps, and switching back to vanilla.

## Forsaken power correction, 2026-10-18

The user's reference/in-game comparison exposed an unmasked square active-state wash, ornamental double rings, an oversized filled glyph and a key chip overlapping the disc. The power badge now uses an opaque circular disc, one gold/parchment radial ring, a smaller procedural Eikthyr antler glyph, a rounded key beside the caption, an uppercase title and a parchment state line with a gold timer. Other powers retain their existing glyph/fallback. The ring displays remaining active duration while active, cooldown recovery while cooling, and a full ring when ready. Eikthyr's line says "Run & jump" rather than claiming a movement-speed boost.

No outside assets were copied. The generated glyph is owned by Theme and cleaned up with its other sprites. The power root now measures the full 310 by 96 HUD-unit layout. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Notification feed correction, 2026-10-18

Unlocks and ordinary top-left messages now share plain parchment text rows above the Forsaken badge, without cards, borders, diamonds or item icons. Item localization tokens and unlock names receive gold emphasis; quantities follow the message inline. Repeated non-quantity messages refresh their lifetime without gaining a fabricated count. Skill improvements retain their separate toast and placement. Existing localized message wording and message-log behavior are preserved.

Validation: Windows Debug build passed with zero warnings/errors. No in-game visual test was performed. After F6 reload, check real pickups (including repeated pickups), repeated walk toggles, unlocks, skill improvements and long localized messages.

## HUD state feedback correction, 2026-10-18

Health below 25% now pulses a red bar outline and soft glow, with a faint screen-edge vignette. Recent losses hold their ghost segment for 0.6 seconds before draining. Full resources fade to 40% after four unchanged seconds and return when their value changes. Empty stamina and the game's StaminaBarEmptyFlash signal activate a hatched track until stamina increases again. These signals only affect presentation.

Equipped action slots rise six HUD units with a solid gold fill. Rounded slots have a lighter hover state, dashed empty borders, and a red breaking state below 10% durability. Consumables blocked by a timed status effect of the same name/category show its remaining timer and radial sweep; no cooldown is invented for ordinary weapons. Freezing uses a red status tile and shelter a parchment outline.

Generated state textures belong to Theme. The vignette belongs to the HUD and is removed on style changes/unload; the stamina observer callback is cleared on disposal and its Harmony patch follows plugin cleanup.

Validation: Windows Debug build passed with zero warnings/errors. No new in-game visual test was performed. After F6, verify health crossing 25% in both directions, repeated damage and healing, four seconds at full resources, an unaffordable stamina action followed by regeneration, equip/unequip and row changes, hover and empty slots, low/broken durability, same-category mead cooldowns, freezing/shelter, death/respawn, and switching back to vanilla.

## Food tracks and hotbar margins, 2026-10-18

Food duration tracks use the Forsaken ring's dark bronze (#514C3D), with the gold remaining segment retained. Empty food slots, including their disc and placeholder timer, render at 30% opacity and return to full opacity when filled.

HUD > Hotbar now exposes SlotSpacing (default 7), HorizontalPadding (8), and VerticalPadding (10), in unscaled HUD pixels. The hotbar has a shared rounded charcoal panel with a subtle rim. Margin changes rebuild its owned layout live, repositioning the badges and armor/weight line along with the slots. BottomMargin still measures the panel's distance above the screen edge, while OffsetX and Scale remain available.

Validation: Windows Debug build and existing test suite passed. No in-game visual test was performed; F6 reload is required. Check empty/filled food transitions, spacing/padding extremes, the raised equipped slot, and margin changes with gamepad selection and a second hotbar row enabled.

## Hotbar background and durability correction, 2026-10-19

Removed the shared hotbar panel and its rim. Existing spacing and padding controls remain available for layout. Durability lines now have ten HUD pixels of horizontal inset and eight below the fill, with a dark backing track and green healthy fill so they remain readable on gold equipped slots. Low and broken durability retain orange/red feedback. Track and fill are hidden together for items without durability.

Validation: Windows Debug build passed with zero warnings/errors. No in-game visual test was performed; check after F6 reload with equipped, unequipped, damaged, broken and non-durable items.

## Profile armor and carry weight, 2026-10-19

Moved armor and carry weight from above the hotbar to two compact rows directly beneath the top-left rune badge. Brass shield/pack glyphs and parchment figures match the profile; excess carry weight turns red. The food and companion sections move down to leave room, and the stats follow the character placement, scale and visibility. Values refresh from the local player every half second, independently of whether the custom hotbar is enabled.

Validation: Windows Debug build passed with zero warnings/errors. No in-game visual test was performed. After F6 reload, check equipment armor changes, pickup/drop weight, overburdened color, carry-capacity effects, food/buff spacing, and hiding/switching the HUD.

## Character portrait and helmet toggle, 2026-10-19

HUD > Character now exposes ShowPortrait and ShowHelmet, both enabled by default. The front-facing portrait fits inside the existing circle and keeps its runic rim. Disabling ShowHelmet removes the helmet only from the portrait and restores the normal hair/beard from the player's appearance. Disabling ShowPortrait restores the rune glyph.

The portrait uses render-only copies of the character's transforms, meshes, skinning and material property blocks. No player, network, physics, animator or gameplay-script components are instantiated. An isolated camera renders a 256-pixel texture when the appearance changes; appearance is checked once per second. Temporary render objects and lights are disabled immediately and destroyed after capture. The owned render texture and mask are released when the character HUD unloads. Rendering errors fall back to the rune with one logged warning. Preview without a player keeps the rune.

No outside assets or source code were incorporated. The installed game's VisEquipment implementation was inspected locally to match helmet/hair attachment and coloring behavior; game assemblies and decompiled references remain outside this repository.

Validation: Windows Debug build and existing test suite passed. No in-game portrait/helmet visual test was performed because the user's Valheim session was already running. After F6, check the front-facing crop and lighting, helmet on/off with hidden or shortened hair/beards, helmet swaps, appearance changes, ShowPortrait off/on, death/respawn, leaving/rejoining, style switching and reload cleanup. Custom character models/shaders may require additional compatibility work.

## Fixed portrait pose, 2026-10-19

The portrait copy now restores the character prefab's neutral skeleton before rendering. Camera framing follows the copied neutral head attachment instead of the live player's head point. Movement, crouching, attacking, look direction and world heading therefore do not determine the portrait pose when appearance changes trigger a new capture. Character model, skin, hair, beard and equipment are retained, including the portrait-only helmet toggle. The live player's skeleton is never changed.

Validation: Windows Debug build passed with zero warnings/errors. No new in-game visual test was performed. After F6, toggle the portrait helmet or change equipment while running, crouching, attacking and looking up/down; the portrait should keep the same neutral pose and framing.

## Vitals text contrast, 2026-10-19

Vitals captions, numeric values and the equipped-weapon name now have a dark charcoal SDF outline. Text sizes increase by roughly 10%: captions 9 to 10, main values 13 to 14.5, thin-resource values 10 to 11, and weapon name 15 to 16.5. Bar geometry and placement are retained. Outline materials are owned by the vitals HUD and destroyed during rebuild/unload; shared font materials are not modified.

Validation: Windows Debug build passed with zero warnings/errors. No new in-game visual test was performed. After F6, check the text against snow, bright water and dark interiors, including critical-health numbers, full-bar fading, large resource values, and different HUD scales.

## Bright-environment vitals correction, 2026-10-19

The follow-up screenshot showed the material outline was not producing usable text contrast, and full-resource idle fading also reduced caption/number opacity. Replaced that outline with eight dark offset text silhouettes, sharing the original font material and matching changing text/visibility. These require no outline-shader support or owned font materials. The silhouettes are children of the vitals HUD and are destroyed with it.

Idle fading now applies only to resource graphics. Captions and values retain full contrast. Each track has a permanent dark border extending 1.5 HUD pixels beyond its edges, including the thin eitr/adrenaline strips; the opaque backing also separates unfilled track regions from bright scenery. The existing text-size increase and critical-health feedback are retained.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, check snow/bright sky and dark backgrounds, full-resource idle fading, changing and hidden numbers, critical health, and HUD scale changes.

## Softer vitals contrast, 2026-10-19

Reduced the text silhouette radius from 1.1 to 0.65 HUD pixels and changed its color to muted charcoal at 32% alpha per silhouette. The eight overlapping silhouettes still provide contrast without the previous heavy black stroke. Bar borders now extend 0.75 instead of 1.5 HUD pixels and use muted charcoal at 65% opacity. Text size and full-opacity label behavior are retained.

Validation: Windows Debug build passed with zero warnings/errors. No new in-game visual test was performed; compare against bright sky and dark scenery after F6 reload.

## Rounded exhaustion and larger eitr, 2026-10-19

The stamina hatch now sits inside a rounded stencil mask using the same sliced capsule sprite as the track, preventing rectangular hatch corners from flattening the ends. The mask is owned by the vitals hierarchy and is destroyed with it.

Eitr thickness increases from four to six HUD pixels. Its current value increases from 11 to 13 points, and the maximum uses 85% rather than 75% of that size. The numeric area is widened with a matching reduction of the track's right endpoint to preserve separation. Other resource dimensions and the softened contrast styling are retained.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. Check both ends of exhausted stamina, stamina regeneration, eitr values and spacing, and different HUD scales after F6 reload.

## Exhaustion end-cap correction, 2026-10-19

The follow-up screenshot still showed a flattened end on the masked hatch. Replaced stencil clipping with a hatch texture whose own alpha forms a capsule. Its 464 by 18 dimensions match the 232 by 9 stamina track at double resolution, keeping the ends semicircular under HUD scaling. Stripe spacing and tint are retained, and the extra stencil mask is removed. The generated sprite follows Theme's existing ownership and cleanup.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed; check exhausted stamina's left and right ends after F6.

## Closer vitals labels, 2026-10-19

Health and stamina numbers increase from 14.5 to 16 points. Their labels now sit six HUD pixels from the measured numeric text rather than beside a fixed-width numeric box. Spacing updates when displayed values change, and outline silhouettes follow the resized/repositioned text. Eitr retains its separately tuned size.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed; check single-, double- and triple-digit values after F6 reload.

## Vitals reference size refinement, 2026-10-19

Increased health/stamina numeric text from 16 to 18 points and tightened the measured label gap from six to four HUD pixels, following the supplied HEALTH 142 / 165 crop. Eitr keeps its independent size. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Vitals bottom alignment, 2026-10-19

Health and stamina captions and values now use bottom alignment in matching text rectangles, ending three HUD pixels above their bars. Dynamic label spacing uses the same rectangle height and bottom edge. Eitr/adrenaline retain their inline alignment. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Vitals shared glyph baseline, 2026-10-19

Replaced text-box bottom alignment with explicit baseline alignment for health/stamina captions and values. All use a zero-height baseline rectangle four HUD pixels above the bar, so differently sized text shares the same typographic baseline rather than different scaled font descenders. The outline copies follow this layout. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Rounded eitr track, 2026-10-19

Eitr now uses the same sliced capsule sprite as health and stamina for its border, well, recent-loss segment and fill. Its six-pixel thickness and larger numeric text are retained. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Eitr caption centering, 2026-10-19

Centered the Eitr caption's text rectangle on the six-pixel bar, matching the vertical position of its numeric value. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Hotbar header alignment, 2026-10-19

Slot keys and item levels now share a baseline 14 HUD pixels below each slot's top. Both use the same ten-pixel content inset as the durability fill: keys align to its left edge and levels to its right. A shared content-inset constant keeps the header and durability margins consistent, including when the equipped slot rises. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Padded hotbar icons and outlined labels, 2026-10-19

Item artwork shrinks from 46 to 36 HUD pixels, with eleven-pixel side insets and two pixels between its box and the durability fill. Keys, levels, stack counts, cooldown timers, queue indicators and the external key chips use the same softened charcoal text outline as vitals. The outline implementation now lives in UI.Shared.TextOutline; its objects remain owned by each HUD hierarchy and require no extra font materials.

Displayed item levels have a small generated five-point star beside the digits. The star follows measured level width, disappears with an absent level, and uses dark bronze on an equipped gold slot for contrast. Its sprite belongs to Theme and follows existing cleanup. No external assets were imported.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, check icon spacing, equipped/breaking contrast, level stars, stack/cooldown text, and switching hotbar rows or input devices.

## Reference hotbar framing, 2026-10-19

Action slots now use dedicated generated sprites with approximately eight-HUD-pixel corners and one-pixel rims. Backgrounds use opaque dark charcoal (#101515), with a subtle gray-green default edge. Equipped slots use a muted brown (#2B271B) fill and warm gold (#D4A955) rim, retaining the raised equipped position and the padded game item artwork. Healthy durability stays green over a gray remainder track. The generated slot sprites belong to Theme and follow its cleanup; other HUD tile geometry is unchanged.

Default SlotSpacing decreases from seven to four HUD pixels. Layout migration version 4 updates the previous seven-pixel default once and preserves other custom spacing values. No shared hotbar background is added.

Validation: Windows Debug build and existing test suite passed with zero build warnings/errors; git diff whitespace checks passed. No new in-game visual test was performed. After F6, compare default/equipped/hover/breaking/empty slots and inspect custom spacing, gamepad selection, row switches, and cooldown overlays.

Removed the hotbar's inventory/TAB key hint on 2026-10-19. Windows Debug build passed with zero warnings/errors; no in-game visual test was performed.

## Inventory hint restored, 2026-10-19

Restored the inventory hotkey hint beside the hotbar; it reads the actual binding and falls back to TAB. Replaced the weight glyph with a dedicated outlined backpack showing a front pocket, flap and straps. The new sprite is generated and owned by Theme. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Readable critical health, 2026-10-19

Reduced critical-health glow alpha from 45-85% to 8-18% and reduced its spread to eight HUD pixels. The pulsing rim is now one pixel wide at 18-35% alpha. While critical, the track has an opaque charcoal well that covers the full-width glow inside the bar, so only the actual fill and recent-damage segment represent remaining health. Normal-state track styling is retained.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed; check critical health with and without recent damage and after healing above 25% following F6 reload.

## Critical glow visibility refinement, 2026-10-19

Raised the outer critical glow to 20-40% alpha with a slightly wider spread, and the thin rim to 28-50%. The opaque dark well still blocks the glow inside the empty track, preserving the readable remaining-health segment. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Darker recent-loss segments, 2026-10-19

Reduced the parchment blend in resource lag segments from 55% to 25%, keeping their resource hue while making recent loss less pale. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Dark lag fill, 2026-10-19

Changed recent-loss segments to blend each resource color 45% toward charcoal rather than toward parchment. Lag is now darker than the current fill instead of a pale highlight. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Darker depleted tracks, 2026-10-19

Depleted resource tracks now use opaque deep charcoal (#101413), separating them from the dark resource-colored lag segments in both normal and critical states. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Muted translucent tracks, 2026-10-19

Replaced black depleted tracks with desaturated resource colors blended toward muted sage charcoal at 60% alpha (80% during critical health to limit glow bleed). Recent-loss segments blend toward muted ash at 75% alpha, remaining lighter and more resource-colored than the depleted portion. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Adrenaline matches eitr, 2026-10-19

Adrenaline now shares eitr's six-pixel rounded capsule track, rounded border/fill, 13-point numeric value with an 85%-sized maximum, centered inline label/value, and widened numeric area. Both thin resources use the same layout path while allowing the longer ADRENALINE caption its existing space. Adrenaline retains its orange accent and appears only while positive. Muted depleted/lag colors and soft text outlines apply equally.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, check adrenaline with and without eitr, drain/refill, numeric spacing and HUD scaling.

## Preserved resource accent colors, 2026-10-19

The shared health accent is #D64A3E and stamina is #E8C04A, as specified by the user. These are the base fill colors; depleted/lag states continue to derive muted variants. Preserve these accents in subsequent visual refinements. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Status text outlines and contained labels, 2026-10-19

Status timers and names now share the softened charcoal TextOutline used by vitals and hotbar text. Labels use parchment for legibility. With names enabled, the four-column strip uses 64-pixel cells with six-pixel gutters, centered icons, and bounded two-line names. Long names use ellipsis within their own cell instead of touching neighboring labels. Timer-only mode retains its compact dimensions. Food placement uses the same row-height calculation so additional labeled buff rows receive enough room.

The shared outline helper now follows its source's active state, wrapping and overflow mode; hiding names also hides their outline copies, and truncated text remains bounded. All outline objects remain owned by their status tile and are removed when the effect expires or the HUD unloads.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, toggle B, inspect long names and multiple buff rows, and verify timers/names over bright backgrounds and after effects expire.

## Reference empty and breaking slots, 2026-10-19

Empty slots now have transparent backgrounds, no key number, and a muted dashed outline. Their button remains enabled with its raycast surface intact; gamepad selection uses the dashed outline too. Breaking slots use a dark red-brown background and muted red rim, taking visual precedence over equipped gold. The bright pulsing red fill is removed. Low durability below 10% uses the health-red segment over the existing gray remainder; zero durability no longer displays a misleading full-length red bar.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, check empty/filled row switches, selected empty slots, equipped breaking items, zero durability and repaired items.

## Compact status labels, 2026-10-19

Reduced labeled status cells from 64 to 46 HUD pixels wide and removed the six-pixel gutters. Status names decrease from ten to nine points, use at most two top-aligned lines, and sit within clipped label bounds that also contain their outline copies. Long labels retain ellipsis rather than overflowing into neighboring cells. Timer-only mode stays compact; the shared row-height calculation updates food clearance. TextOutline now mirrors the source's maximum visible lines.

Validation: Windows Debug build passed with zero warnings/errors and git diff whitespace checks passed. No new in-game visual test was performed. After F6, inspect long localized names, multiple rows and toggling labels with B.

## Single-line status names, 2026-10-19

Disabled wrapping for status names to prevent splitting words such as Megingjord onto a second line. Names now shrink between nine and eight points within one line and use ellipsis when they still cannot fit. Outline copies share the same auto-size bounds. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Inside-tile status countdown trial, 2026-10-19

For the user's layout test, countdowns now sit inside the bottom of each buff tile. Icons move up to leave a separate timer band, and timers use eight-to-nine-point auto-sizing with the existing soft outline. Effect names remain single-line below the tile, with row heights reduced to remove the former external timer space. Windows Debug build passed with zero warnings/errors; no new in-game visual test was performed.

## Reference compass rail, 2026-10-19

Restyled the compass from the user's supplied image using the existing Alegreya/Scada fonts,
parchment and antique gold palette, procedural diamond sprite and soft text outlines. The
720-unit heading rail has five-degree ticks, taller 15/45-degree marks, serif direction names
and gold north. The old center notch is removed. Named, colored diamond markers sit beneath
the rail, with a separate baseline-aligned footer for bearing/direction, the player's localized
biome name and current world day. The footer stays visible when a marker passes the center.
Labels truncate at a fixed width and hide when they collide or cross the rail's edge;
their diamonds remain visible. The layout shrinks to fit narrow HUD canvases.

Existing home, portal, ship and publicly visible player target rules/ranges remain in use.
No altar target, map pins, gameplay changes, new network messages or synchronized settings
were added. Optional compass backdrops have since been removed. All compass objects,
including text outlines, are destroyed on HUD replacement, feature disable and unload.
TextOutline now mirrors character spacing and font size so spaced/scaled footer labels keep matching silhouettes.
No external code or assets were imported from the reference image.

Validation: native Arch Linux Debug/Release builds and the existing regression suite passed.
The updated Debug plugin was deployed to the isolated development game's ScriptEngine folder.
The startup/reload smoke check was not run because the user's Valheim session was active;
it was left running. No in-game visual test was performed. After F6 reload, check north/359°
wraparound, centered targets, crowded markers, biome/day changes, HUD scale and narrow screens,
hidden/shared player positions, and disable/re-enable/reload cleanup.

## Guide typography and compass footer correction, 2026-10-19

Replaced the shared theme's title/label fonts with the guide's Cinzel SemiBold
(600) and Barlow Semi Condensed SemiBold (600). Added Barlow Bold (700) for HUD
resource values, food/status/cooldown timers, counts, item levels, armor, carry
weight, companion distances and the minimap clock. Body/italic text uses the
matching Barlow family. Character and companion rune badges now use Noto Sans
Runic; the player portrait retains its existing capture and fallback behavior.
Guide fonts include Turkish glyphs directly. Cinzel is shipped as a static
600-weight instance for the runtime loader. Font sources, conversion notes,
hashes and OFL licenses live in `Assets/Fonts/SOURCES.md`; licenses are embedded
alongside the font files in the DLL. No extra runtime dependency was added.

The supplied in-game screenshot exposed offset dark copies in the compass footer.
TextOutline now mirrors anchor/pivot/alignment and weight as well as position/size;
it also strips color tags from rich text so silhouettes remain charcoal. Bearing,
biome and day now share a single centered TMP line, with a larger gold 700-weight
bearing and 600-weight context on one baseline. This removes independently measured
text boxes and their positioning mismatch. Numeric/cardinal changes both refresh
the footer, including direction changes at half-sector boundaries.

Validation: Arch Linux Debug and Release builds passed with zero warnings/errors;
the existing regression suite passed. Offline font checks confirmed static faces,
400/600/700 weight metadata, numeric/Turkish glyphs and runic glyph coverage.
The Debug plugin was deployed for F6 reload. No new in-game visual or startup/reload
test was performed because the user's game session was running. Check the footer
while turning, glyphs in player names, mixed-size resource values, timers, rune
fallback, and repeated reload/style switching after F6.

## Smaller compass with fading ends, 2026-10-19

Reduced the entire compass to 85% of its previous size, multiplied by the existing
HUD scale. Directions, rail, markers and footer scale together. Moving ticks,
direction labels and marker groups now fade smoothly over the outer 20% at either
end, retaining full contrast in the central 60%. A generated alpha-gradient sprite
applies the same curve to both the parchment rail and its dark underlay, replacing
the previous hard line endpoints. The centered footer retains its full opacity.
The compass owns and destroys the generated texture/sprite on disable, HUD change
and unload; no shaders, external assets, gameplay or network changes were added.

Validation: Arch Linux Debug and Release builds passed with zero warnings/errors;
the Debug plugin was deployed to ScriptEngine. No new in-game visual test was
performed. After F6, check the smaller footprint and smooth disappearance of the
rail, ticks, directions and markers at both ends while turning.

## Pack inventory guide, 2026-10-19

Applied the user's Pack inventory reference with the user's explicit preference
to retain Valheim item icons. The custom inventory now has a Cinzel Pack heading,
All/Gear/Materials/Food/Trophies pill tabs, a rounded smoked-glass grid board,
rounded dark slots, dashed transparent empty slots, gold/brown hover/equipped
states, category dots and a matching five-color legend. Numeric counts and item
levels use Barlow 700 with the shared soft outlines. Broken/low-durability items
retain red feedback; food, durability and teleport restrictions remain visible.

The tabs spotlight matching categories by dimming other items in their original
slots. Hovered/controller-selected items remain readable, and all slots return to
full visibility during a drag. No item positions, saved inventories, slot counts,
equipment rules or network authority are changed by category selection. The Gear
category includes armor, weapons, tools and ammunition; armor has its own blue dot.
Materials/miscellaneous items are ash, food/mead peach, and trophies green.

A compact HOTBAR strip beneath the legend mirrors the actual first inventory row,
with clicks/dragging forwarded to those same native slots. It adds no storage or
new hotkey behavior. Quick Stack, Keep preferences, sorting, nearby-chest feedback,
armor/carry stats, character gear, provisions and optional quiver slots remain
available. The gear/provision board uses the same rounded presentation. Existing
chest slot styling is retained. The custom inventory can shrink to fit limited
screen space even at the default configured scale.

Rounded board/pill sprites are procedural and owned by Theme; inventory controls,
slot outlines and hotbar mirrors are destroyed on style change/unload. Native
grid masks now remain installed across layout rebuilds, avoiding reuse of
CanvasGroups already scheduled for destruction; unloading restores the masks.
No external assets or code were imported from the supplied image.

Validation: Arch Linux Debug/Release builds passed with zero warnings/errors,
and the existing regression suite passed. The updated Debug DLL was deployed to
ScriptEngine. No new in-game visual, interaction or multiplayer test was performed
because the user's Valheim session was running. After F6, check Pack/character/chest
placement, all category tabs, hover/card focus, empty-slot drops, Shift-splitting,
Ctrl-move, right-click use, hotbar mirror input, gamepad selection, sorting, Quick
Stack, resizing and switching inventory styles/reloading repeatedly.

## Matching chest interface, 2026-10-19

Chests now use the Pack guide's rounded smoked-glass board, filled/dashed slot
states, category dots, legend, Cinzel title and Barlow numeric text. The header
shows the localized chest name and All/Gear/Materials/Food/Trophies tabs. As in the
Pack, tabs dim unrelated items in place; hovered/controller-selected items and
drag targets remain readable. Chest weight and the existing sort, Take all and
Place stacks actions sit below the grid in matching pill controls. Smaller chest
grids are centered in the board; their native dimensions and item coordinates are
unchanged. No hotbar or extra storage is added to a chest.

Header/tabs, legend and action-button rendering now share `InventoryGuide` with
the Pack inventory. Existing native click/drag/split/move handlers, access rules,
container synchronization and mask restoration remain in use. All new controls
belong to the chest root and are removed on rebuild, style changes and unload.
No dependencies, external assets or gameplay/network settings were added.

Validation: Arch Linux Debug and Release builds passed with zero warnings/errors;
the existing regression suite passed and the Debug DLL was deployed. No new
in-game visual, interaction or multiplayer test was performed while the user's
Valheim session was active. After F6, inspect small/large chests, long names,
category tabs, moving/splitting/dragging, Take all/Place stacks/sorting, tooltip
placement, Pack/chest docking and style switching/reload restoration.

## Unified inventory header, reference tooltip and category Quick Stack, 2026-10-19

The newer inventory reference supersedes the earlier Pack layout. One rounded
panel now wraps the name/header, grid, controls and input hints. The player's name
and INVENTORY caption sit left; armor with a frost shield glyph, Weight current/max
and a small capacity bar sit right. Over-capacity values/bar turn health red.
The old ornamental armor/weight StatLine widget is removed. Chests use the same
header language with their localized name, STORAGE caption and actual weight;
no weight limit or capacity meter is invented for a chest.

Per the user's clarification, category tabs remain below the header. The custom
Quick Stack button uses that same selected category and names it (for example,
Quick Stack: Materials); the three Keep icon toggles are removed from this panel.
All transfers every eligible category, and the other tabs transfer only their
matching category. Existing protection for equipped, quest, dragged, assigned
gear/provision and quiver items remains in force. This selection only narrows the
existing owner-approved transaction; access/range, server-enabled state and
multiplayer transport remain unchanged. Vanilla inventory Quick Stack still uses
the saved Keep preferences, with settings descriptions updated to explain this.
Chest tabs spotlight chest contents; native Take all/Place stacks actions retain
their existing scope. With a chest open, the custom Quick Stack still targets
only that open chest, using the player's selected category.

Removed category legends and the duplicated inventory HOTBAR section. The real
first grid row has its key numbers, and the live gameplay hotbar remains intact.
No decorative runic equipment legend is added; the functional gear/provision
panel remains available. Valheim item icons remain as explicitly requested.
Durable items use the reference's green/yellow/red durability line, including a
full line on healthy items and zero remaining fill on broken ones.

ItemCard, shared by player and chest inventories, now uses a rounded gold outline,
gold Cinzel title, tracked type line, italic description, simple left-label /
right-value rows and a muted Weight / Quality footer. Removed the level diamond,
stat glyphs, moss detail bands and ornamental durability ruler. Actual vanilla
stats, ranges, effects, crafting notes, value and teleport restrictions remain
available as plain rows/text. Tooltips flip left near the screen's right edge and
fit/clamp to the root canvas. UI nodes remain owned by each panel and unload
through the existing restoration path; no new assets or dependencies were added.

Validation: Arch Linux Debug/Release builds passed with zero warnings/errors,
and the existing allocation/death/config regression suite passed. The updated
Debug DLL was deployed. No new in-game visual, transfer or multiplayer test was
performed while the user's session was running. After F6, check long names,
armor/weight updates and over-capacity feedback, tooltip descriptions/long stats,
category switching and scoped Quick Stack (including All and protected slots),
open-chest transfers, full/broken durability, native input, layout scaling and
style/unload restoration. Existing regression checks do not validate these new
visuals or category-scoped transfers in a real world.


### Character HUD contrast and circular backing, 2026-10-19

Added the shared charcoal text silhouettes to the character name, armor and
weight values, party heading/names/distances, FOOD caption, food timers and
provision counts/keys. Silhouettes follow live text, visibility and layout;
all objects remain children of the existing owned HUD roots.

Portrait and food circles now have continuous opaque dark backing reaching the
rim, with a one-unit dark outer silhouette supporting the thin decorative line.
The portrait capture/mask stays at its existing 84-unit size. Empty food cells
no longer fade their entire CanvasGroup to 30%; their well and duration track
remain visible against bright scenery, with muted empty timers. Existing food
progress, refresh pulse and portrait ornament remain intact.

The first 46-unit food circle is inset 23 units beneath the 92-unit portrait,
so both centers are at X=46. Subsequent circles retain the 60-unit spacing;
provisions use the same inset, while the FOOD caption keeps its left alignment.
This is local presentation only, with no gameplay/authority/config changes.

Validation: Linux Debug and Release builds passed with zero warnings/errors;
allocation (45,562 checks), death and config regression checks passed. Deployed
the Debug plugin for F6 reload. No in-game visual test performed for this change.
Check bright sky/dark interiors, empty and active food timers, portrait fallback,
HUD scaling, party visibility and unload/reload in game.


### Food row alignment correction, 2026-10-19

User screenshot confirmed that centering the first food circle beneath the
portrait pushed the whole row too far right. Removed the 23-unit inset from
food circles and provisions: their left edge now follows the portrait's left
edge. Inset FOOD by 4 units so its text does not hang left of the visual row.
Circle spacing and backing remain unchanged. Debug build/deploy passed with
zero warnings/errors. No in-game visual test of this correction performed.


### Food section follows the stat column, 2026-10-19

At the user's request, FOOD, the circle row and provisions now share X=12 with
the armor/weight icons, rather than the portrait edge. The stat icon positions
and food offsets use the same constant to preserve that alignment. Debug build
passed with zero warnings/errors and deployed for F6 reload. No in-game visual
test performed for this positioning change.


### Empty food state, 2026-10-19

Empty food cells now use a 28%-alpha dark well and a muted 50%-alpha dashed
ring (16 dashes). Their continuous outer silhouette is hidden, so it does not
bridge the dash gaps. Active food retains its solid backing, duration ring and
refresh pulse. Empty timers are blank and disabled; any timer with no positive
remaining duration is also hidden, including its owned text silhouettes.
Generated ring texture/sprite are owned by Theme and disposed on unload.
Debug build/deploy passed with zero warnings/errors; no in-game visual test
performed. Verify empty-to-active-to-empty transitions after F6.


### Inventory summary shield and alignment, 2026-10-19

Replaced the inventory summary's small filled/cross shield with an original
procedural hollow blue shield, sized 16 by 20 units. The existing character HUD
shield is unchanged. Armor and weight figures plus the Weight caption now use
14-unit type in matching 28-unit-high boxes; the shield and 6-unit capacity bar
share their Y=24 centerline. Increased gaps and reserved summary width prevent
the larger label from crowding the figures/meter. The shared chest weight header
receives the same typography and meter spacing. The new sprite/texture follow
Theme ownership/disposal and use no external artwork or dependencies.
Debug build/deploy passed with zero warnings/errors; diff whitespace check
passed. No in-game visual test performed; check F6 reload, armor values and
long inventory names at the user's current inventory scale.


### Supplied SVG inventory shield, 2026-10-19

Replaced the polygon approximation with the user's exact 24x24 SVG path: both
cubic lower curves, 2-unit stroke, round joins, hollow center and #8FC1E8 tint.
Source is retained in Assets/Icons/InventoryShield.svg (user-provided artwork).
Theme samples each cubic into 24 segments and rasterizes the round stroke at
96x96 using the existing owned texture pipeline; no SVG runtime dependency.
The image uses the supplied 15x15 square dimensions, centered on the existing
header Y=24 alignment. Debug build/deploy passed with zero warnings/errors;
no in-game visual test performed for this icon revision.


### Rounded capacity fill and compact summary spacing, 2026-10-19

The inventory/chest weight meter now resizes a nine-sliced rounded fill instead
of using Image.Type.Filled clipping, which cut through the right cap. Set the
sprite pixel multiplier to give both track and fill a radius of half their
6-unit height. Zero fill is hidden; a fill narrower than the height becomes a
small circle so it retains smooth caps without overstating its horizontal span.

Summary positions are measured from the actual armor/weight strings: 6 units
between Weight and its value, 8 between value and meter, and 16 before the Weight
caption for the armor group. Shield, armor, Weight, figures and meter share the
same Y=24 centerline. Removed the previous hard-coded text offsets. The portrait
HUD now also uses the supplied shield shape, retaining its gold coloring.

Validation: Debug and Release builds passed with zero warnings/errors; updated
Debug plugin deployed and diff whitespace check passed. No in-game visual test
performed. Check empty/low/full/over-capacity meter, short/long values, both
inventory and chest summaries, portrait shield, and scaling after F6 reload.


### Optical alignment and direct capsule geometry, 2026-10-19

User screenshot showed the sprite-based meter caps still flattening at the
current HUD scale. Replaced that rendering with CapsuleGraphic: a simple UI
mesh with semicircular ends computed from the displayed rectangle, plus a
0.6-unit transparent fringe for smooth edges. Track and fill share this code;
there are no sprite borders, fill clipping, materials or generated textures.
Geometry is rebuilt by the normal UI layout/dirty path, and the graphic remains
owned by the summary hierarchy for unload. Small fills retain their earlier
width/height handling and zero fill is hidden.

Applied an optical 1.5-unit downward offset to Weight (600) relative to the
700-weight numbers. Reduced the SVG shield box from 15 to 13 units to match its
visible stroked height more closely to the numeric cap height. Other spacing
and the gold portrait shield remain unchanged.

Validation: Debug and Release builds passed with zero warnings/errors; Debug
deployed for F6 reload; diff whitespace check passed. No in-game visual test
performed. Recheck the user's current HUD scale, low/full/zero bar values and
inventory/chest header alignment after reload.


### Sort button labels, 2026-10-19

Inventory and chest sorting now read Name / Kind / Weight, with 44-unit buttons
for readable full labels. Updated the inventory hover hint to Sort by kind.
Sorting behavior is unchanged. The chest footer fits its existing 470-unit
minimum width. Debug build/deploy passed with zero warnings/errors and diff
whitespace check passed. No in-game visual test performed for these labels.


### Inventory key guide contrast, 2026-10-19

Key labels LMB, RMB, SHIFT + LMB, 1–8 and the dynamically bound Inventory/close
key now use white (#FFFFFF) rich-text spans. Action descriptions retain the
existing muted material color. Debug build/deploy passed with zero warnings/
errors and diff whitespace check passed. No in-game visual test performed.


### Worn/provisions panel alignment, 2026-10-19

The character side frame now starts at Y=0 and shares the main inventory's
panelHeight, rather than floating beside its middle. Its gear/provision/optional
arrow cells start at HeaderHeight + Pad, matching the main grid's first row;
size and subsequent row gaps remain shared. WORN / PROVISIONS / ARROWS headings
use Cinzel in the header area, aligned to their slot columns with constrained
auto-sizing. Dividers use muted frame coloring and consistent panel padding.
The optional Better Archery column follows the same placement. Slot positions,
equipment/provision behavior and interaction callbacks are unchanged.

Validation: Debug/Release builds passed with zero warnings/errors and Debug
was deployed; diff whitespace check passed. No in-game visual test performed.
After F6, check normal/extra utility slots, optional quiver column, different
inventory row counts, screen-fit scaling and input/unload restoration.


### Stacked character slots and live dashboard, 2026-10-19

Kept the side frame's exact previous width formula, position and panelHeight.
Inside it, four 48-unit cards show armor/current health/stamina/eitr, with maxima
under resource values. Colors follow the supplied guide; armor uses the same
rounding as the inventory summary. Worn gear now occupies two rows of five
available cells, with provisions in a six-cell row below. Optional quiver/arrow
slots fit in the same frame; cell sizes are constrained by available height and
width. Dividers are horizontal. Slot inventory coordinates and native click,
equip, drag/drop, tooltips and provision behavior are unchanged.

A fixed 52-unit blue strip above Worn lists equipped set names, native piece
counts/required counts, ACTIVE/LOCKED status and localized native effect text.
Only equipped inventory items nominate sets, and Humanoid.GetSetCount supplies
the count: spare matching armor and extra utility effects cannot inflate it.
Active requires both the required count and the actual SEMan status effect.
Counts, requirements and descriptions are read from installed game data, with
no hard-coded Troll bonus. The tooltip comes from the live effect when present,
otherwise its template. Multiple/long effects scroll within a clipped strip;
a small thumb indicates overflow. No-set state is explicit. Native APIs were
verified against the user's installed Humanoid/StatusEffect/SEMan assemblies;
no game source or binaries were added to the repository.

All cards, scrolling UI and text remain children of the existing owned root;
ClearPanel releases its dashboard reference on rebuild/unload. The dashboard
adds no patches, global subscriptions, resources or dependencies. It reads local
player data only and does not change set activation, gameplay authority, saves,
server synchronization or configuration.

Validation: Debug and Release builds passed with zero warnings/errors. Existing
allocation (45,562 checks), death and configuration regression checks passed.
Numeric layout verification covered 738 combinations (24–64 unit cells, 4–12
inventory rows, quiver on/off), confirming slot bounds and unique gear positions.
Debug plugin deployed for F6 reload; diff whitespace check passed. No in-game
visual/equipment/multiplayer test performed for this dashboard. Check partial,
complete and mixed sets, swap/remove/break pieces, native/modded long effects,
scrolling, resource/max changes, utility/provision/quiver actions, small-screen
scaling and unload/reload. Existing regression checks do not validate the new
UI or live native set state in a loaded world.


### Dashboard below slots and stronger empty cells, 2026-10-19

Moved Worn to the top of the same side frame, followed by Provisions and optional
Arrows. The 108-unit stats/set dashboard is now bottom-aligned below the slots,
with the existing bottom padding. Revised cell-height constraints reserve at
least a 12-unit gap above the dashboard. The frame width/height are unchanged.

All guide inventory/chest slots (including gear/provisions) now use a 70%-alpha
dark empty well, a brighter 80%-alpha gray-green dashed outline and clearer
empty placeholders. Hover/cursor strengthens the well to 90% and keeps the gold
highlight. A dedicated thicker dashed edge is owned by Theme; gameplay hotbar
edges and occupied slot colors remain unchanged. No external assets added.

Validation: Debug/Release builds passed with zero warnings/errors; Debug was
deployed. Numeric bounds verification passed 738 combinations of cell size,
inventory rows and optional quiver, confirming slot/dashboard separation and
unchanged frame limits. Diff whitespace check passed. No in-game visual test
performed; check empty-slot contrast, hovered empty cells, slots/dashboard at
current scale, optional arrows and set-strip scrolling after F6 reload.


### Muted set-bonus surface, 2026-10-19

Set strip now uses dark slate blue #24323B (75% inactive / 85% active) and a
muted #678495 border/scroll thumb. Text colors stay readable and the armor
category/icon tint is unaffected. Debug build/deploy passed with zero warnings/
errors; diff whitespace check passed. No in-game visual test performed.

### Forsaken power shortcut, outlines and first-journey state, 2026-10-19

Power caption, name, state/timer and key now use the shared owned HUD text
outlines. The activation chip stays white during cooldown, reads the current
native GP binding (default F), expands for longer labels and explicitly says
UNBOUND when no binding exists. The GP action and default were checked against
the installed ZInput assembly; keyboard binding text refreshes once per second.

When no power is selected, the same footprint shows a subdued dashed seal,
UNCLAIMED / A POWER AWAITS, and "Defeat a Forsaken. Offer its trophy at the
Sacrificial Stones." The inactive key is hidden and the caption moves left;
there is no recharge/progress or ready pulse. This also covers existing players
without a selected power; it does not infer boss progression from missing power
selection. Selected powers retain native ready/cooldown/active state handling.
No gameplay, save, network, dependency or external asset changes. All new
outline objects belong to the existing HUD root and are destroyed with it.

Validation: Debug build/deploy passed with zero warnings/errors; diff whitespace
check passed. No in-game visual/input test performed. After F6, check a fresh
character, selecting a power, ready/active/cooldown, rebound/long/unbound keys,
bright backgrounds and unload/reload.

### Softer Forsaken shortcut, 2026-10-19

Replaced the scaled 96-pixel shortcut tile with dedicated nine-sliced rounded
surfaces (5-unit corners), preserving the antialiased edge at its displayed size
and with long key bindings. Removed the bright offset foot. The face is dark
#101919 at 70% alpha, the bone border is 45%, and the key is bone at 90% instead
of hard white. Other HUD keys/tiles are unaffected. Theme owns and disposes the
two generated sprites/textures; no external assets or gameplay changes.

Validation: Debug build/deploy passed with zero warnings/errors and diff
whitespace check passed. No in-game visual test performed; check the shortcut
after F6 against bright/dark scenery and with a longer binding.

### Compact set bonus without scrolling, 2026-10-19

Replaced the set strip's scrolling content with a vertically centered compact
summary, e.g. Troll 2/4 · Sneak: +15 · Locked. SE_Stats' flavor-description prefix
is omitted when separate native effect lines remain; those lines join with
middle dots. Numeric effects/units remain native, including modded effects.
Each matching set occupies an explicit line. Text fits at 9–11 units and uses
ellipsis for unusually long/many sets rather than scrolling. The full localized
description and active/locked counts remain available on hover through the
installed inventory tooltip prefab. No-set state is also a compact line.

The existing 52-unit strip, colors, dashboard position and frame size stay the
same. Removed ScrollRect, viewport/mask, scrollbar and content-height handling.
The native tooltip component is owned by the dashboard and hides its tooltip on
disable/unload. No set-counting or activation behavior changed.

Validation: Debug build/deploy passed with zero warnings/errors; diff whitespace
check passed. Native SE_Stats tooltip composition and UITooltip disable cleanup
were checked in installed assemblies. No in-game visual/hover test performed;
check partial/complete Troll set, multiple sets, long effects and hover after F6.

Set-name follow-up: the summary and hover title explicitly name the set, e.g.
"Troll set 4/4", retaining the full localized native name and appending "set"
only if it is not already the suffix. Debug build/deploy passed with zero
warnings/errors; no in-game visual test performed.

### Full armor-set display names, 2026-10-19

Replaced the generic suffix with an explicit case-insensitive dictionary keyed
by native set IDs: troll → Troll Leather Set, root_armor → Root Set,
fenring_armor → Fenris Set, AshlandsMediumArmor → Ask Set, berserker_armor → Bear
Set, berserker_armor_undead → Vilebone Set, DeepNorthMediumArmor → Vanguard Set,
lox → Lox Fur Set, harvester → Harvest Set. These English display labels are
shared by the summary and full hover title. Unknown/modded IDs use their full
localized name, capitalize the first character and append Set if absent.

Coverage was checked read-only against all armor prefab ItemDrop shared data in
the installed Valheim l-1.0.16 c4210710 asset bundle: nine distinct nonempty set
IDs with required counts and nonnull set status effects. This also confirmed
the raw Troll identifier is lowercase, explaining the previous label. No game
assets or extracted data are included. UnityPy (MIT) was used only in a temporary
external inspection environment, not added as a project dependency; no outside
code/assets incorporated. Bonus descriptions, counts and activation still come
from native game data. General armor naming was cross-checked with
[Valheim armor list](https://physgun.com/tools/valheim/armor/).

Validation: Debug build/deploy passed with zero warnings/errors; diff whitespace
check passed. No in-game visual test performed; check full names, compact text
fit, hover details and modded fallback after F6.

Name/count spacing follow-up: added a middle-dot separator in both the summary
and hover title, e.g. Troll Leather Set · 3/4 · Sneak: +15 · Locked. Debug
build/deploy passed with zero warnings/errors; no in-game visual test performed.

### Vanilla inventory feature parity, 2026-10-19

Vanilla mode now uses the shared inventory summary (player name, armor shield,
weight and capsule capacity meter) with native fonts. Native wood, slot prefabs,
item icons, grid pointer/gamepad handling and complete item tooltips stay in use.
All/Gear/Materials/Food/Trophies native buttons spotlight matching grid items and
select the Quick Stack scope. The separate Keep toggles and sort-mode cycling
button are replaced by scoped Quick Stack and Name/Kind/Weight buttons. Keys in
the compact footer guide are white while their actions remain muted. Existing
Keep config keys remain for compatibility with unscoped callers; no wire names
or transfer authority/ownership/ward protections change.

The native chest receives the same storage name/weight header, category tabs and
named sorting, plus Take All and Place Stacks forwarded to the original button
events. Original widget activation, grid offsets and panel sizing are saved and
restored. Native sunken art/scroll decorations move with the grid. Grid height
is preserved by adding matching header/footer space to the panel. Player's
native cached base height is likewise adjusted/restored so later row-count
updates retain the reserved header/footer. Native inventory/crafting/info state
and container ownership logic are not replaced.

Per the user's layout choice, the native character board is now a side panel,
aligned with the inventory's top and bottom rather than folding under it. Worn
occupies two rows, then Provisions and optional quiver/arrows. The shared
four-card stats dashboard and compact full-name set strip sit below the slots,
using native fonts and button artwork. Set counts, full armor-set dictionary,
locked/active coloring and hover descriptions share the same data path as the
guide style. Empty native slot labels have stronger opacity. The side panel
stays visible with a chest open. Native panels only shrink as necessary to fit
the available screen/crafting space; the user-configured scale continues to
control the guide style. Native weight caption and numbers use the same font
and baseline without the guide font's optical offset.

All owned UI, spotlight CanvasGroups and tooltip components are removed with
their roots/adapter disposal. Rebuilt native cells' dead spotlight references
are pruned. Style switching restores native sizing before guide frames measure
their layout; shared native sprites/fonts/materials are read-only and never
disposed by this feature. No dependencies or assets added. Native InventoryGui,
InventoryGrid, UITooltip and UI prefab layout were inspected read-only from the
installed game; no extracted game code/data is included.

Validation: Debug and Release builds passed with zero warnings/errors; Debug
deployed. Existing allocation/death/configuration suite passed (45,562 allocation
checks). Numeric bounds checks passed 522 native side-panel slot-size/row-count/
quiver combinations, including header/footer grid-height preservation. Diff
whitespace check passed. No in-game visual, hover, controller, multiplayer or
live style-switch test performed; the running development game was not stopped.
After F6, select HUD.Inventory Style=Vanilla and inspect layout, each category,
scoped stacking, sorting, chest take/place/scroll, side slots and bonus hover,
different row counts/screen sizes, style switching and unload/reload.

### Quieter Ragnarök empty slots, 2026-10-19

Reduced idle empty-slot well opacity from 70% to 55%, dashed border from 80%
light gray-green to 50% muted #737B72, label opacity from 65% to 42%, and glyph
opacity from 70% to 45%. Applies to guide inventory, chest and character cells.
Hover/controller focus still uses the strong well and gold border. Native slots
and occupied guide slots are unaffected. Debug build/deploy passed with zero
warnings/errors; diff whitespace check passed. No in-game visual test performed.

Small follow-up: idle empty borders now 45% alpha (from 50%), labels 38%
(from 42%), glyphs 41% (from 45%). Well opacity and focus treatment stay as
before. Debug build/deploy passed; no in-game visual test performed.

### Ragnarök set-bonus hover card, 2026-10-19

The guide dashboard now uses ItemCard's dark rounded surface, gold outline,
Cinzel heading and Barlow text for set-bonus hover details. Added a plain-detail
entry point to ItemCard, sharing its raised canvas, dynamic height, edge flipping
and screen clamping. Full set names, piece counts, active/locked status and all
native descriptions remain present. Contents refresh while hovered as equipment
changes. Pointer exit and parent disable hide the card; its entire hierarchy is
owned below the dashboard and destroyed with it. No shared native tooltip prefab
or global tooltip instance is modified. Vanilla inventory continues to use the
native tooltip. No dependencies/assets/gameplay changes.

Validation: Debug build/deploy passed with zero warnings/errors; diff whitespace
check passed. No in-game hover/visual test performed. After F6, check set hover,
bright scenery, long/multiple effects, screen edges, close/reopen and switching
between inventory styles.

### Guide 02 — messages, tooltips and level-ups, 2026-10-19

Applied the supplied Messages, Tooltips & Level-ups sheet to existing Ragnarök
presentation paths. No bitmap assets, external code, dependencies or gameplay/
network/save changes were introduced. The native MessageHud, Player, Character,
Skills, SEMan, ItemDrop and HitData APIs/tooltip formats were inspected from the
installed game; extracted files remain outside the repository.

Notifications now use 360×46 rounded smoked-glass cards with semantic accents:
gold unlock/rested, frost-blue wet, red inventory-space/freezing warnings, neutral
ordinary pickups/system messages. Native item icons remain. Four cards maximum,
newest at the bottom, four seconds including transitions. Repeated source text/
icon/amount-kind merges and restarts its lifetime; source identity is independent
of the displayed fallback glyph. Quantities sit at the right in gold. The
existing position and scale settings still apply; optional HUD backdrops have been removed. Native
message logging is retained. Explicit messages requested over a hidden HUD fall
back to the native path; ordinary hidden-HUD messages follow the native guard.

A separately owned non-interactive centre layer shows short system lines 18%
below the crosshair for 2.5 seconds, with bone information, amber blocked/build
states and light-red resource failures. Major messages sit 38% from the top:
0.4-second fade in, 3-second hold, 0.8-second fade out, one at a time with a bounded
queue. Death takes priority, using ember rune rules and the actual last attacker's
localized name when available. Native death camera/loading fade remains in
charge. Discovery uses the native biome/location animation as its event source;
only its presentation is masked, so native queuing/audio stay intact. Its mask
is maintained after animation and restored on disposal; only the added owned
CanvasGroup is removed. New-day and selected-power changes are observed locally
after the initial ready-player baseline, avoiding a false gained-power banner
on login. First-ready/teleport/death state is guarded. Power instructions read
the current GP binding and explicitly handle unbound controls.

Normal skill gains use compact glass pills. Untouched defaults migrate from the
old right-side placement to a position 12 HUD units above the live health/vitals
panel, following its position and scale; custom positions remain. Every tenth level
uses a gold-ring milestone banner under the live compass (or a top-centre
fallback), clamped to screen bounds. Descriptions come from the native skill
definition; no example damage/stamina percentages from the reference are invented.
New UI placement migration is local and leaves config/wire names unchanged.

The interaction prompt and centre text use a dedicated on-world TextOutline mode:
1-unit #0A0D0E ink plus a soft shadow centred 2 units below with a 6-unit footprint.
Nine translucent silhouettes approximate the reference's 60% blurred shadow
without new shaders/materials. Main glyphs stay opaque during their hold phase;
only transition groups fade them. Existing softer panel-label outlines remain.

Item/detail/status hover cards share the gold rounded ItemCard surface, Cinzel
heading and Barlow text. Delay is 250 ms; pointer offset is 16 HUD units with
left flipping and screen clamping/scaling. Controller focus keeps slot-based
anchoring. Item headings show up to eight filled/unfilled quality stars; the
numeric quality footer retains actual values for modded higher-quality items.
Shift comparison picks another equipped item of the same item type, requiring
matching weapon skill for weapons. Only matching native numeric damage/defence/
stamina/eitr rows receive additive base-value deltas. Units must match; native
range text is retained; unsupported formats are left intact. Lower resource
costs are green, higher costs red. Values are read from both native tooltips,
not recalculated from guessed skill formulas. The equipped comparison name and
white-key/muted-action hints appear below the metadata.

Status tiles have lazy owned hover cards with localized native effects and
remaining time, refreshed once per second. Known rested/wet/freezing states get
buff/debuff captions; unknown effects use a neutral status caption. These work
when the UI cursor is available. Set-bonus cards inherit the same delay and
positioning. Vanilla inventory/item tooltips and vanilla notifications retain
their own presentation when those styles are selected. The guide's party-chat
example does not introduce a party channel or imply party membership; existing
shared-map-position semantics are unchanged.

All cards, outlines, status hover components and centre layers belong to the
existing owned roots. Disposal hides/destroys them, restores the native discovery
mask, clears queues and releases action-bar/compass docking references. Existing
Harmony ownership/unpatching handles the two message prefixes; no new global
subscriptions or RPCs were added.

Validation: Debug and Release builds passed with zero warnings/errors; Debug
deployed. Existing allocation/death/configuration checks passed (45,562 allocation
checks). Diff whitespace check passed. Native tooltip-number formats and message/
skill/discovery source paths were inspected read-only. No in-game visual, hover,
comparison, controller, progression, audio, style-switch or multiplayer test was
performed; the running game was not restarted. After F6, use Test notifications,
then verify real pickup merging/max-four/lifetimes, unlocks and statuses, short
build/resource messages, death/discovery/power/day banners, skill 9→10/29→30 and
ordinary levels, custom placements, quality stars, Shift comparisons, status/set
hover delay and edge fitting, HUD hiding, vanilla switching and unload/reload.

### Console popup previews

Rune & Iron inventory, chest and character side panels now default to stronger
85% smoked-glass opacity. Former untouched 95%, 58%, 42% and 72% defaults migrate
through local layout versions 6–9; customized opacity remains. The character,
food/provisions/party and status HUD hides while inventory is open and returns
on close; settings previews still display it. Slot wells use light
12% empty / 18% occupied / 30% hover/equipped/breaking fills over the panel;
dashboard stat wells use 18%, and set bands 25% inactive / 35% active, preserving
the muted blue tint. Footer buttons use 22%. This avoids heavy nested plating
while retaining item art, labels, state outlines and durability bars. Native
wood-themed panels keep their existing rendering.

The weapon wheel now uses shared translucent HUD glass for segments and center,
muted neutral borders and gold selected edges. It retains Valheim item icons.
Its center shows type, quality, localized name, base damage or shield block armor,
and a labeled durability percentage. Slot key/stack labels and moss/yellow/red
durability bars sit beneath the icons. Rune & Iron labels have world outlines;
the broad shadow plate is removed. Existing wheel size, selection and equip input
behavior are retained. Damage figures reflect native quality-scaled base item
values rather than predicted skill/buff-adjusted hits.

HUD Shade/Plate backdrop rendering and its settings controls have been removed.
Existing local backdrop keys are retained but ignored; individual glass slot/card
surfaces remain. The small Ragnarök minimap terrain uses 58% opacity, its rim and
medallion fills share HUD glass, and its key uses the same glass with a muted rim.
Biome/cardinal/key/day/clock/weather labels use world outlines and shadows.
Native map image color, frame objects, mask hierarchy and biome visibility restore
on vanilla switching/unload. Map pins/markers retain their individual styling.

The health track now shows an outlined average HP/second readout on its inside
left edge (`+0.6/s`, for example). It includes native food regeneration and active
healing modifiers/effects, rather than claiming food heals every second; food
healing actually ticks every ten seconds. At full health it reads muted `0/s`.
The readout follows HUD scale and numeric visibility.

Small notification card surfaces use smoked glass at 58% opacity, with a muted
dark blue for wet. Borders use 45%, icon wells 40% and icon rims 65%, while
text and item icons retain their readability.
The same shared 58% HUD glass now backs the Forsaken power circle/key, eaten
food wells, status tiles, provision cells and bottom action slots. Wet status
tiles share the muted blue tint; freezing uses dark red glass. Food silhouette
backing is ring-only so it does not make the translucent disc opaque. Equipped
or breaking action-slot fills replace the base fill instead of stacking over it.
Hotbar hover/press fills rise to 68%/72%, cooldown overlays use 35%, and state
rings, artwork, text and duration/durability indicators retain their contrast.

`ragnarok_popup <type>` previews guide 02 presentation in a loaded world with
Ragnarök notifications enabled. Types: pickup, unlock, rested, wet, warning,
skill, milestone, system, blocked, failure, death, discovery, power, day.
`all` sequences samples at 4.6-second intervals; `clear` cancels the queue and
clears displayed popups. Close F5 to begin; opening it pauses subsequent samples.
Each dispatched sample records its index and next sample in the console and
Unity log. HUD hiding/unavailability pauses rather than discards the queue;
leaving or changing player sessions cancels it. Preview exceptions report the
failed sample and stop the queue rather than silently consuming it.
The command supports tab completion and needs no cheats or server permissions.
It calls UI presentation directly, leaving inventory, skills, powers, health,
world progression, audio and the native message log untouched. Samples use
illustrative English text, with native localized item/skill/key names where available.
Commands and queued previews are removed on unload; any prior command occupying
the same name is restored. Debug/Release builds passed and Debug was deployed;
console execution, visual behavior and F6 registration cleanup still need an
in-game check.

## Crafting interface study implementation, 2026-10-19

The accepted concept is implemented without recipe pinning. The panel stays inside
the installed game's native 570×650 crafting frame, using inventory opacity (85%
by default), rounded smoked glass, Cinzel headings, Barlow labels/numbers and native
item/station icons. Gold Craft/Upgrade tabs and the integrated repair extension invoke the
original controls. No artwork or third-party dependencies were added.
Repair occupies a flush 104×40 extension of the panel's left contour beside the
recipe controls. The body and extension use one rounded silhouette and continuous
border, without a gap, internal seam or extra fill. Availability/hover changes its
label and icon rather than drawing a button plate; inventory sizing reserves its
occupied space. The extension disappears when the station cannot repair or the native repair
action is disabled, including when all eligible equipment is fully repaired.
Crafting remains visible while a chest is open.

Search matches localized recipe names and relevant current-quality ingredients.
Every whitespace-separated term must match somewhere in the name/ingredients;
matching is partial and case-insensitive. Typing is debounced 150 ms; `/` focuses
search, the clear control empties it, and an empty-results action clears filters.
Changing station types or worlds clears search/category/slot/craftable state.

Only categories present in the station's known recipes are shown. Shields are
separate from weapons. Food is grouped by dominant health/stamina/eitr; baked
inputs follow the finished food, while fermented bases and all meads (including
resistance meads) share Meads. Armor exposes available Head/Chest/Legs/Cape/Utility
and Other slots. All resets category/slot; Craftable only is independent.
Name and Craftable-first sorting reorder actual native pairs so mouse/gamepad
selection indices stay aligned. Vanilla style retains its native sorting and
glyph controls, with ingredient search and the additional categories; slot filters
clear when switching to vanilla.

Rows show native artwork, names, categories, upgrade quality and availability.
The selected recipe has italic flavor text and grouped native tooltip text in
the description scroll area, a full-tooltip hover on its icon, station requirements
and compact green/red have/need material tiles. Physical/elemental damage, blocking,
attack costs, nourishment, equipment and effects have separate headings. Native
quality-scaled values and skill ranges remain intact; unknown effect text is retained. Nearby storage counts use the existing authorized resource scope.
Alternative-ingredient recipes are labeled accordingly; more than four materials
retain the existing paging behavior. Quantity, style selection, upgrades, repair
and craft progress continue through Valheim's controls. The final refinement removes stat cards and reduces material tile height from 86
to 60 layout units, giving the item description 70 more vertical layout units.

Owned rows, labels, hover cards, typing registrations and native presentation masks
are cleaned up on rebuild/unload; filters and native recipe presentation reset on
unload. Gameplay authority, resource consumption, crafting time and synchronization
remain on the existing native/nearby-storage paths.
