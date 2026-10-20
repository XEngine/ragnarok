# Ragnarök

A custom Valheim mod pack for KernelPanic and friends. This repository contains our
own plugin and the development tools.

See [CHANGELOG.md](CHANGELOG.md) for the consolidated change record,
[the Rune & Iron guide](docs/RUNE-IRON-HUD.md) for design details, and
[verification notes](docs/VERIFICATION.md) for completed checks and remaining in-game tests.

**Arch Linux:** native Linux setup and equivalent development commands are documented
in [docs/LINUX.md](docs/LINUX.md). Run `./scripts/setup.sh` to create an isolated
`~/RagnarokDev` environment from your installed Valheim copy, then `./dev.sh play`.
The Windows instructions below remain available for the original development PC.

## Settings window and HUD (0.3.0)

Press **F10** to open the Ragnarök settings window: a dashboard overview plus one page
per config section. Changes apply instantly and save to the config file. Escape or F10
closes it. The key and the window size are under **HUD → Window**.

The style ("Forge & Animus") mixes God of War's obsidian, gold filigree and ember with
Assassin's Creed's hairlines, chamfered corners and crimson selection. Theme colours,
generated sprites and font selection live in `src/Ragnarok/Ui/Shared/Theme.cs`.
The Rune & Iron HUD uses embedded Cinzel, Barlow Semi Condensed and Noto Sans Runic;
font sources and licenses are recorded in `src/Ragnarok/Assets/Fonts/SOURCES.md`.
Everything is built in code with Unity UI (uGUI); there are no asset bundles.
While the window is open, game actions and camera input are blocked and clicks cannot
reach game UI behind it.

**HUD → Style** chooses `Vanilla` (default) or `Ragnarok`. The Ragnarök HUD replaces
the native vitals with health/stamina bars above the bottom-center hotbar, an eitr
strip when applicable, and character/food information at top left; switch back at any time.
It is parented to Valheim's HUD, so it follows the game's UI scale
and hides with Ctrl+F3. Separate settings control the other HUD replacements.
Inside the health bar, a read-only HP-per-second estimate reflects food regeneration
and positive healing effects; it shows zero at full health. It does not change healing ticks.

**Weapon dial** (`[HUD.WeaponDial]`, `Features/QualityOfLife/WeaponDial.cs`): hold the dial key (V by default; the side
mouse buttons can be bound in F10). Every weapon, shield, bow, tool and torch you carry is laid out in
a ring at the screen's centre, hotbar first, up to 12. Move the mouse (or the right stick) towards one
and release the key to take it in hand; release with the pointer in the middle to cancel. While the
dial is open it counts as one of Valheim's radial menus, so the camera holds still and attacks, blocks
and jumps are off, but you can still walk. Picking the item already in hand does nothing.
Rune & Iron uses translucent glass with outlined labels. The center identifies the selected
item's type, quality, name, base damage (or shield block armor) and durability percentage;
each durable item has a condition bar beneath its icon, beside hotbar keys and stack counts.

Buffs and debuffs use translucent rounded tiles with native icons and countdowns.
Labels are optional; B toggles them. Resting, campfire and
shelter join one comfort group showing your comfort level. `[HUD.Status] Enabled = false`
turns the strip off and brings back Valheim's top-right list.

The circular Forsaken power display has a recharge/active-duration ring, outlined
name and state text, and a muted shortcut chip that follows the native binding.
New characters see an introductory empty state without an activation prompt.
`[HUD.Vitals] ShowForsakenPower = false` hides it.

The key guide (`[HUD.Keys]`) replaces Valheim's bottom hint bar with a compact vertical
list at the right centre by default. Combat, building-tool and fishing hints move below
the Ragnarök hotbar as centered white keys and muted action labels. It mirrors whichever vanilla hint set is active
(building, combat, inventory, fishing), so rebinds, gamepad layouts and translations
carry over. `Enabled = false` restores the vanilla bar.

Building, leveling and cultivation use a compact Rune & Iron task card above the
vitals and hotbar: selected tool, task icon/name, description and smaller material
and station requirements. It mirrors Valheim's readouts, including missing-resource
states and nearby-storage checks. The card follows inventory panel opacity and
restores the native readouts when switching to the Vanilla HUD.

The hammer build menu uses a category sidebar and compact icon tiles with small
craftable counts. Piece names appear in the native hover readout. Search matches pieces and materials; Only what I can
build follows native availability. Nearby preview counts respect the server's
building-storage rules; the preview toggle does not change actual resource
withdrawal settings. Favorites and recent pieces use the game's own lists.
Hover or focus a piece to show the native name, description and requirements;
click uses the original piece-button handler, including native touch behavior.
The menu follows inventory opacity and restores the original picker with Vanilla HUD.

The menu opens in a centered 1120 by 580 window, scaling down for smaller screens.
Local `[HUD.BuildMenu]` settings retain filters, with rebindable category
(Q/E), favorite (F) and search (/) shortcuts. The close guide reads the native binding.
Controller navigation supports card selection, trigger category changes, X favorites,
left-stick search, Y repair and B close. The top-left hammer selects native repair
mode. Compact column (4-16) and row (3-10) controls sit beside the piece count,
defaulting to 5 by 4 icon tiles; the old untouched 12 by 6
default migrates once, preserving other custom density settings. Station upgrades
retain the native upgrade-arrow marker.

HUD parts draw directly over the world without the optional Shade/Plate backdrops.
Legacy `Background`/`BackgroundOpacity` HUD keys are ignored and hidden from settings.
The minimap uses translucent framing and outlined labels; individual HUD slot fills
retain their smoked-glass styling.
Rune & Iron inventory, crafting, chest and character side panels default to 95% smoked
glass, with lighter inner slot/dashboard fills. `[HUD.Inventory] BackgroundOpacity`
still adjusts their panel opacity; former defaults migrate automatically. The
top-left character, food and status HUD hides while the inventory is open.

HUD settings are split into `[HUD]` (style, scale), `[HUD.Vitals]` and `[HUD.Status]`;
the F10 window shows the two parts as sub-pages under HUD. Values from 0.3.0's single
`[HUD]` section are migrated automatically on first load.

HUD parts can be placed anywhere: `Position` picks one of nine screen points
(TopLeft … BottomRight; a 3×3 grid in the F10 window), and `MarginX` / `MarginY` set the
distance from those edges in 1080p pixels (-500 to 500; on a centred axis, positive moves
right/up). Defaults put vitals at bottom-center (0, 118), character at top-left (40, 34),
status under the character name (150, 72), and Forsaken power at bottom-left (40, 34).

The inventory (`[HUD.Inventory] Style`, default `FollowHud`) replaces Valheim's player grid with
the Rune & Iron panel: character name, armor and carry weight/meter in the header,
All/Gear/Materials/Food/Trophies tabs below, rounded slots with gold selected/equipped
edges, dashed empty slots and a compact gold-outlined item tooltip. Valheim item icons are retained.
There is no extra hotbar strip or category legend below the grid. Valheim's
own grid keeps running underneath, so click, Shift-split, Ctrl-move and right-click work as before.
Chests share the styling and tabs, with their name and weight in the header
and sorting, Take all and Place stacks below. Both retain Valheim's inventory logic.

**Quick Stack** (below the grid) puts carried items into chests within the nearby-chest
range that already hold the same item, nearest first, like Valheim's "Stack all" for every
chest at once (only the open chest when one is open). Either inventory style's selected
category also selects what Quick Stack transfers; the button names that category.
All includes every eligible category. Equipped, quest, dragged and assigned
gear/provision/quiver items stay protected. Category tabs replace the three Keep chips
in both styles. Saved `[QualityOfLife.QuickStack]` Keep preferences remain available
for unscoped callers. Ward, ownership, server-enable and
multiplayer rules remain the same as nearby crafting.

**Character panel** (right of the inventory, in either inventory style, `[Inventory] CharacterSlots`): worn gear sits in its
own slot (head, chest, legs, cape, utility, trinket) and six **provision** slots hold food, meads and
other consumables. Items there do not use grid slots. Putting gear on (right click, hotbar, or a
drop on its slot) moves it into the slot; taking it off, or dragging it into the grid, moves it back.
The slots are positions in fixed rows far below the player's grid, so they save, weigh and work like
normal inventory; vanilla's grid and "inventory full" checks simply do not see them. With the vanilla
inventory the side panel uses Valheim's own slot prefab, font and wood artwork, aligned with
the inventory's top and bottom. Turning it off moves everything back into the grid as room allows.
Quick Stack never takes items from these slots. In both styles, the side frame
stacks worn gear over provisions and optional arrows, with the stats/set dashboard below the slots.
Four compact cards show current armor,
health, stamina and eitr (with resource maxima). A fixed set-bonus strip shows equipped piece
counts and compact native effects, with incomplete bonuses marked Locked. Each set gets one
summary line; hover for full descriptions, including longer or modded effects. The strip does
not scroll. This display does not change equipment effects or increase the panel size.
Vanilla also has the shared name/armor/rounded weight header, category tabs, Quick Stack scope,
Name/Kind/Weight sorting and white-key/muted-action guide. Its chest has a matching storage header,
tabs and native Take All/Place Stacks actions. Native item icons, grid interactions and tooltips
remain in use. The combined native panels shrink together only when screen/crafting space requires it;
the character dashboard stays visible with an open chest. Switching styles or unloading restores
native sizes, offsets and hidden widgets.

**Minimap info** (`[HUD.Minimap]`, either HUD style): day and clock in a strip above the minimap,
current weather and the next one (with the time until it starts) below. The next weather repeats
EnvMan's own seeded pick for the next weather period, so it is exact unless an event, boss fight or
special zone overrides the weather (then it shows a dash). If the minimap sits too close to the top of
the screen for the clock strip, it is moved down just enough.

**Minimap frame** (`[HUD.Minimap] Style`, FollowHud by default): Valheim's small map keeps working;
its frame art is hidden (map picture, pins, markers, wind arrow and masks are kept) and a Ragnarök
round gold-rimmed frame drawn around translucent terrain, with smoked-glass medallions and map key.
Biome, cardinal, key, day/clock and weather labels have outlines and shadows.
Everything is restored on Vanilla or unload.

**Notifications** (`[HUD.Notifications]`, FollowHud by default): unlocks, pickups and status messages
use rounded dark cards with semantic gold/blue/red accents. Up to four stack, newest at the bottom,
for four seconds; repeated pickups merge with a gold count. Placement remains
configurable; optional HUD backdrop plating has been removed. Centre system lines use outlined bone/amber/red text; death, discovery, new-day and
newly selected power banners use the larger Rune & Iron treatment. Discovery audio stays native,
and intercepted messages still enter the native log. The settings page has a **Test notifications** button.

In a world, use the F5 console for UI-only popup previews:
`ragnarok_popup all` plays every sample in sequence; close F5 to start playback.
Use `ragnarok_popup clear` to cancel, or choose `pickup`, `unlock`, `rested`, `wet`,
`warning`, `skill`, `milestone`, `system`, `blocked`, `failure`, `death`, `discovery`,
`power` or `day`. Tab completion lists the options. No `devcommands` needed;
Ragnarök notifications must be enabled. Samples do not change skills, powers,
inventory, health or world progression, or enter the native message log.
Skill gains use compact pills; untouched default placement docks above the health/vitals panel.
Every tenth level gets a banner below the compass, with the game's skill description. Custom toast
positions are retained. All of this follows the notification style; Vanilla uses native messages.

Ragnarök **tooltips** appear after 250 ms, sit 16 HUD units to the right of the pointer, and flip/clamp
at screen edges. Item cards show quality stars and native durability/effects. Hold **Shift** to compare
supported numeric stats against comparable equipped gear; green/red deltas use the native displayed
base values, while stamina/eitr costs treat lower values as improvements. Status tiles also show
native descriptions and remaining time in matching hover cards when the UI cursor is available.

**Compass** (`[HUD.Compass]`, either HUD style): a Rune & Iron heading rail at the top centre that slides with
the camera; Cinzel directions, gold north, ticks every 5° and fading rail ends. Colored diamonds below
show a name and distance; a separate footer keeps the gold bearing/direction, current biome and day visible.
Markers include your bed (parchment, any distance), the nearest six
portals within `PortalRange` (blue, from `ZDOMan.GetPortalList`), ships within 400 m that you
are not aboard (parchment), and other players with their names (gold), only those who share
their position on the map (`ZNet.GetPlayerList`, `m_publicPosition`).

**Crafting panel** (`[HUD.Crafting] Style`, FollowHud by default): drawn over Valheim's crafting
panel, which keeps running unseen (list, selection, requirements, timing, multi-craft, upgrade, repair).
Smoked glass follows inventory opacity, with Craft / Upgrade pills, native repair and item icons.
Repair sits in a flush extension of the panel's left contour, sharing its background and border.
The whole extension is hidden unless the current station supports repair and a native repair
action is available, including when all equipment is already repaired. Opening a chest
keeps crafting visible and usable.
Search matches recipe names and current ingredients (all words must match); `/` focuses it.
Labeled filters show only categories the current station offers: weapons, shields, armor, tools,
ammo/materials or health-, stamina-, eitr-focused foods and meads. Armor adds Head / Chest / Legs /
Cape / Utility filters. All and Craftable only are separate, with Name / Craftable-first sorting.
The selected recipe shows italic flavor text and a scrollable text breakdown grouped into
physical damage, elemental damage, blocking/protection, attack/resource use, nourishment,
equipment and effects. Values and skill damage ranges come from the native tooltip at the
selected quality/world level; unfamiliar effect text is retained. There are no stat cards.
Smaller material tiles retain names, icons, green/red have/need quantities and readiness.
Hover the recipe icon for the complete native item tooltip. Style, quantity,
upgrades and the craft progress bar keep using Valheim's controls. No Pin action is added.
Search/category/slot/craftable state resets when moving between station types or worlds.

With Valheim's crafting panel the same search and filters sit above its recipe list in Valheim's style
(`[QualityOfLife.Crafting] VanillaFilters`). Both panels share one filter state (`Features/QualityOfLife/CraftFilter.cs`), which
filters Valheim's own recipe list after it is built. Vanilla adds debounced name/ingredient search
with the `/` shortcut, a clear button, category/craftable filters, Name/Craftable-first sorting,
and armor subfilters that reserve space only while Armor is selected. It retains the native
recipe list, wood/button art, fonts, descriptions, repair, variants and crafting progress.
New-item crafting gains a typed or stepped quantity, bounded to affordable stock (up to 99);
upgrades remain one item. Native requirement amounts show total available/required, with the
bag/chest breakdown in the hover tooltip using the native inventory tooltip backdrop.
Nearby stock follows the existing server-controlled
crafting storage settings. With the vanilla HUD, filled provision slots show
as Valheim hotbar slots just past the hotbar, with their keys (`Features/QualityOfLife/VanillaProvisionBar.cs`).

**Info panel** (follows the inventory style): the wooden board above the inventory, with the character
name and the Compendium, Skills, Trophies, Achievements and PvP buttons, redrawn with
rounded charcoal surfaces, thin borders, parchment labels and gold active toggles
over the hidden original (`Ui/Windows/InfoPanel.cs`). Each Valheim button gets a twin with its glyph and tooltip
that presses the original, so buttons added by other mods appear too.

**Compendium** (`[HUD.Compendium] Style`, FollowHud by default): Valheim's texts dialog redrawn
as a Ragnarök screen over the (hidden, still running) original. Tabs to Skills and Trophies on top;
chapters on the left (Journal: active effects, log, stats; Hugin's Lessons; Runestones; Notes), the
open chapter expanded; the entry in the middle; a rounded preview card on the right
with the glyph of the boss the entry mentions (else your Forsaken power). All four
books share charcoal surfaces, thin borders, rounded tabs and selection cards,
parchment text, gold highlights, capsule scrollbars and the inventory opacity setting.
The framed window scales down to fit narrower displays; Close uses a matching Esc keycap.

**Skills** (`[HUD.Skills] Style`, FollowHud by default): Valheim's skills dialog in the same frame
(`Ui/Windows/BookWindow.cs`, shared with the Compendium). Every skill on the left with its level and a mastery
line; the chosen one in the middle with its level large, the bonus from gear and food, progress to the
next level, mastery towards 100 and its description; its glyph in the preview card.

**Trophies** (`[HUD.Trophies] Style`, FollowHud by default): Valheim's trophy panel in the same
frame (`Ui/Windows/TrophiesBook.cs`). Every trophy in the game as a tile wall in Valheim's order: the ones you
have taken in colour, the rest as dark shapes. The chosen one's name, lore and use in the middle, with
a collection bar (taken / total); its glyph in the preview card.

**Achievements** (`[HUD.Achievements] Style`, FollowHud by default): Valheim's achievements panel
in the same frame (`Ui/Windows/AchievementsBook.cs`), with a fourth tab. Every badge as a tile wall in Valheim's
tier order, unlocked ones lit. The chosen one's name, description and conditions are in the middle, read
from the same player stats Valheim's details use. Valheim's secrecy is kept: a locked secret stays hidden
and an unmet condition shows as ???. Valheim's "cheated" note shows bottom left when it applies.

**Sort** (in the inventory footer, both styles; `[QualityOfLife.InventorySort]`): Name, Kind or Weight. Partial
stacks of the same item are merged first, then the grid is laid out in reading order. The hotbar row
stays as it is by default (`KeepHotbar`); character, provision and quiver slots never move.

**Provision bar** (`[QualityOfLife.Provisions]`): filled provision slots show beside the food circles
in the Ragnarök HUD with their hotkeys (default Left Alt+1…6, rebindable in the settings window).
A key eats or drinks from its slot like a hotbar key; Valheim's hotbar does not also fire on it.

**Better Archery** (optional, soft dependency): when its quiver is enabled, the character panel shows
the worn leather quiver and its three **Arrows** slots (a row in the Ragnarök dashboard,
a column in the vanilla panel) (Better Archery's own rows; its ammo-only rule and Alt+1-3 keys apply). The grid is its base
rows only. Character slots live at fixed rows 64/65, and are shielded from Valheim's
`DropInvalidItems` (which runs on every spawn) and from Better Archery's hidden-row repairs, which
would otherwise drop them.

**Extra utility slots** (`[Inventory] ExtraUtilitySlots`): the Utility column has three slots. The
first is Valheim's own utility item; items in the other two are not equipped, but their status
effect is added to the player's equipment effects (a transpiler on
`Humanoid.UpdateEquipmentStatusEffects`), so Megingjord, wishbone and Wisplight work together. Only
the first shows on the character. Right-clicking an extra item makes it the worn one (they swap).

Debug builds can capture a preview from the main menu, then quit:
`valheim.exe -ragnarok-ui-preview D:\RagnarokDev\preview`.

## Tombstone recovery

With `[QualityOfLife.DeathRecovery] AutoEquipOnReclaim` enabled, using your own
gravestone takes everything directly when it fits in your bag plus available
character/provision slots and within your current carry-weight limit. Previously
worn gear goes straight into empty character slots and re-equips without the
equip timer, even when the bag grid is full. Take All uses the same slot-aware
recovery. Genuine overflow stays in the gravestone; being underweight does not
create more bag slots. Gear must have been recorded at death with recovery enabled.
Saved provision positions are recorded for deaths after this update.

## Server settings

The server (a dedicated server, or the player hosting) decides the gameplay settings for everyone
who joins: `[QualityOfLife.NearbyChests]` Enabled, Range, Crafting, Building and Stations; `[QualityOfLife.QuickStack]
Enabled`; `[Inventory]` CharacterSlots and ExtraUtilitySlots. It sends its values after each
player's handshake and again whenever one changes. While its `[Gameplay.Server] LockSettings` is on (the
default), players use the server's values. Their F10 window shows those rows dimmed with "SET BY
SERVER" and they cannot change them. Off: everyone uses their own.

Players' own values stay in their config file: the file is always written with them, never with the
server's, and they come back on leaving. A dedicated server re-reads its config file whenever it is
saved, so edits reach players without a restart. A server without Ragnarök controls nothing. Every
other setting (HUD, styles, keys, scale) stays with each player. Admins are not exempt.

Server side: `ServerConfig.cs`. It uses Valheim's own peer RPCs (`Ragnarok_ServerConfig`, plus
`Ragnarok_ServerConfigRequest` so an F6 reload mid-session asks again), with no extra library.

## Nearby chest resources (0.2.0)

Craft, upgrade, build and supply stations using resources in player-built chests
within **20 metres of your character**. Your carried stock of each resource is
used first, then the nearest eligible chests. Materials can be split across chests.

Press Use as usual to add smelter/blast-furnace ore and fuel, kiln inputs, fire/torch
fuel, cooking-station food/fuel and fermenter ingredients. Stations do not refill
automatically. Normal capacities, recipe knowledge and station requirements still apply.

Chest access respects wards and private ownership. Open/in-use chests, tombstones,
world-loot containers, carts and ship inventories are excluded.

For multiplayer, install the same Ragnarök version on every client and the dedicated
server, if used. Remote chest owners must approve an ownership handoff before spending.
If you see "waiting for a shared chest", wait briefly and press Use/craft/build again.
This prevents writing to another player's stale chest inventory. The server decides these
settings (see Server settings). Each chest owner checks its configured
range and permissions before granting access. Multiplayer gameplay still needs testing.

Edit the `[QualityOfLife.NearbyChests]` section in the config and press **F7**:

```ini
[QualityOfLife.NearbyChests]
Enabled = true
Range = 20
Crafting = true
Building = true
Stations = true
```

See [the gameplay test checklist](docs/NEARBY-CHESTS.md).

## Windows development layout

- Source: `D:\RagnarokDev\Source`
- Development game: `D:\RagnarokDev\Valheim`
- Separate test saves: `D:\RagnarokDev\saves`
- Local .NET SDK 10.0.402: `D:\RagnarokDev\dotnet`
- Valheim BepInEx pack 5.4.2351 and ScriptEngine r11.2.
- Plugin targets .NET Framework 4.7.2, using local Valheim/Unity/BepInEx assemblies.

The SDK compiles the plugin; Valheim runs it with its own Unity/Mono runtime.
No global PATH change is needed.

## Everyday workflow

Double-click **Play.cmd** to build and launch the development game (keep Steam open).
Double-click **Watch.cmd** to rebuild/deploy automatically as code changes.

Or open PowerShell in this folder and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 play
```

The plugin shows a small Ragnarök status badge. Create a new test character
and world using local saves. The launcher passes a separate `-savedir`; Steam Cloud
is not a substitute for local test-save isolation.

After editing code:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 deploy
```

Then press **F6** in the game. **F7** reloads settings and **F8** toggles the badge.
Settings live at
`D:\RagnarokDev\Valheim\BepInEx\config\com.kernelpanic.ragnarok.cfg`.
Try changing `BadgeText`, save, and press F7.

Other commands:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 build
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 watch
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 logs
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 doctor
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 package
powershell -NoProfile -ExecutionPolicy Bypass -File .\dev.ps1 test
```

`watch` rebuilds and deploys after source edits; F6 still controls when code reloads.
Stop watch/logs with Ctrl+C. `package` creates `artifacts/Ragnarok-local.zip` containing
our Release plugin and installation instructions, without game DLLs or development tools.
This is a local sharing archive, not a published Thunderstore package.

## Editor

Open `Ragnarok.sln` in a C# editor. Configure its .NET CLI path to
`D:\RagnarokDev\dotnet\dotnet.exe` if it does not find the local SDK.
`Environment.props` gives IDE builds the local game path.
The scripts work without installing a full IDE.

## Recreate or refresh the setup

After a Steam game update, close Valheim and run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\setup.ps1
```

Optional `-SourceGame` and `-DevRoot` parameters support other paths.
Setup copies the game into the development directory, installs the pinned SDK
and verified mod-tool archives, writes local paths and builds/deploys our plugin.
It copies without deleting stale files; after major game updates a fresh development
directory may be preferable. Source-game modifications other than the excluded
BepInEx loader may also be copied.

`dev.local.json` and `Environment.props` are ignored machine-specific files.
Game binaries, SDKs and test saves stay outside Git.

## Reload boundaries

ScriptEngine reloads plugin instances, not the whole Unity game. Every future feature
must remove its Harmony patches, subscriptions and spawned objects on unload.
Use a unique Harmony owner per instance if reload lifetimes overlap.
New assets, registered items, startup hooks or network state may require a restart.
Always test a normal fresh launch before sharing a release.

Add Jötunn or Unity asset tools when the chosen features require them.

Ideas go in [docs/INSPIRATION.md](docs/INSPIRATION.md).

## References

- [Valheim BepInEx pack](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- [ScriptEngine source and reload lifecycle](https://github.com/BepInEx/BepInEx.Debug)
- [Jötunn development guide](https://valheim-modding.github.io/Jotunn/guides/quickstart.html)

See [docs/VERIFICATION.md](docs/VERIFICATION.md) for completed checks and the runtime smoke-test command.


## Source modules and settings

See [module architecture and configuration migration](docs/ARCHITECTURE.md) for ownership, settings groups, migration behavior and verification commands.

- `src/Ragnarok/HUD`: live HUD elements and their vanilla replacement/restoration logic.
- `src/Ragnarok/Ui/Shared` and `Ui/Windows`: shared rendering and inventory/crafting/book windows.
- `src/Ragnarok/Features`: gameplay modifiers, quality-of-life tools, equipment and nearby storage.
- `src/Ragnarok/Configuration`: presentation/feature settings, schema migration and settings window.
- `src/Ragnarok/Networking`: server configuration authority and transport.
- `src/Ragnarok/Development`: opt-in runtime checks and isolated preview helpers.

The F10 settings window now groups options under **Gameplay**, **Quality Of Life**, and **HUD**. Window and inventory appearance belong to HUD; convenience behaviors have their own Quality Of Life pages. Existing configuration values migrate automatically with a `.before-modules.bak` backup.
