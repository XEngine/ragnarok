# Rune & Iron HUD

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
