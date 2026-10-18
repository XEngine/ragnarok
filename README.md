# Ragnarök

A custom Valheim mod pack for KernelPanic and friends. This repository contains our
own plugin and the development tools.

## Settings window and HUD (0.3.0)

Press **F10** to open the Ragnarök settings window: a dashboard overview plus one page
per config section. Changes apply instantly and save to the config file. Escape or F10
closes it. The key and the window size are under **Interface**.

The style ("Forge & Animus") mixes God of War's obsidian, gold filigree and ember with
Assassin's Creed's hairlines, chamfered corners and crimson selection. Theme colours,
generated sprites and Valheim's TextMeshPro fonts live in `src/Ragnarok/Ui/Theme.cs`.
Everything is built in code with Unity UI (uGUI); there are no asset bundles.
While the window is open, game actions and camera input are blocked and clicks cannot
reach game UI behind it.

**HUD → Style** chooses `Vanilla` (default) or `Ragnarok`. The Ragnarök HUD replaces
the vanilla health, stamina, eitr and food display with a bottom-left cluster; switch
back at any time. It is parented to Valheim's HUD, so it follows the game's UI scale
and hides with Ctrl+F3. Other vanilla HUD elements are unchanged.

**Weapon dial** (`[HUD.WeaponDial]`, `Ui/WeaponDial.cs`): hold the dial key (V by default; the side
mouse buttons can be bound in F10). Every weapon, shield, bow, tool and torch you carry is laid out in
a ring at the screen's centre, hotbar first, up to 12. Move the mouse (or the right stick) towards one
and release the key to take it in hand; release with the pointer in the middle to cancel. While the
dial is open it counts as one of Valheim's radial menus, so the camera holds still and attacks, blocks
and jumps are off, but you can still walk. Picking the item already in hand does nothing.

Buffs and debuffs move to a bottom-right strip ("12:23 RESTED ◇   7 COMFORT ◇ ◇"):
value, spaced label and a monochrome version of the game's icon. Resting, campfire and
shelter join one comfort group showing your comfort level. `[HUD.Status] Enabled = false`
turns the strip off and brings back Valheim's top-right list.

The diamond emblem beside the bars is your Forsaken power: its rim refills clockwise
while recharging (timer in the centre), drains in ember while the power is active, and
flares once with a key prompt when it is ready. It replaces Valheim's power box;
`[HUD.Vitals] ShowForsakenPower = false` removes it and the bars move to the edge.

The key guide (`[HUD.Keys]`) replaces Valheim's bottom hint bar with a compact vertical
list at the right centre by default. It mirrors whichever vanilla hint set is active
(building, combat, inventory, fishing), so rebinds, gamepad layouts and translations
carry over. `Enabled = false` restores the vanilla bar.

Each HUD part (vitals, status, key guide) has `Background` and `BackgroundOpacity`:
`Shade` is a soft feathered field (Assassin's Creed), `Plate` a chamfered obsidian plate
with a gold rim (God of War), `None` draws nothing. Default: Shade at 50%.

HUD settings are split into `[HUD]` (style, scale), `[HUD.Vitals]` and `[HUD.Status]`;
the F10 window shows the two parts as sub-pages under HUD. Values from 0.3.0's single
`[HUD]` section are migrated automatically on first load.

Both parts can be placed anywhere: `Position` picks one of nine screen points
(TopLeft … BottomRight; a 3×3 grid in the F10 window), and `MarginX` / `MarginY` set the
distance from those edges in 1080p pixels (-500 to 500; on a centred axis, positive moves
right/up). Defaults: vitals bottom-left, status bottom-right, 40 px.

The inventory (`[Inventory] Style`, default `FollowHud`) replaces Valheim's player grid with
the Ragnarök panel: chamfered slots, quality diamonds, gold rings on equipped gear, a crimson
gamepad cursor, armour and weight in the header, and a Ragnarök-styled version of the vanilla item tooltip beside the hovered slot. Valheim's
own grid keeps running underneath, so click, Shift-split, Ctrl-move and right-click work as before.
Chests and crafting still use Valheim's panels.

**Quick Stack** (right of the grid) puts carried items into chests within the nearby-chest
range that already hold the same item, nearest first, like Valheim's "Stack all" for every
chest at once. Equipped and quest items never move. The **Keep** chips (also
`[Inventory.QuickStack]`) leave weapons/shields/ammo, food and consumables, and tools/torches
in your inventory; all three are on by default. The same ward, ownership and multiplayer rules
as nearby crafting apply. With the vanilla inventory, the same button and chips appear in
Valheim's own style right of the armour and weight plaques.

**Character panel** (right of the inventory, in either inventory style, `[Inventory] CharacterSlots`): worn gear sits in its
own slot (head, chest, legs, cape, utility, trinket) and six **provision** slots hold food, meads and
other consumables. Items there do not use grid slots. Putting gear on (right click, hotbar, or a
drop on its slot) moves it into the slot; taking it off, or dragging it into the grid, moves it back.
The slots are positions in fixed rows far below the player's grid, so they save, weigh and work like
normal inventory; vanilla's grid and "inventory full" checks simply do not see them. With the vanilla
inventory the same panel is drawn from Valheim's own slot prefab and wood panel, under the vanilla
Quick Stack block. Turning the panel off moves everything back into the grid as room allows.
Quick Stack never takes items from these slots.

**Minimap info** (`[HUD.Minimap]`, either HUD style): day and clock in a strip above the minimap,
current weather and the next one (with the time until it starts) below. The next weather repeats
EnvMan's own seeded pick for the next weather period, so it is exact unless an event, boss fight or
special zone overrides the weather (then it shows a dash). If the minimap sits too close to the top of
the screen for the clock strip, it is moved down just enough.

**Minimap frame** (`[HUD.Minimap] Style`, FollowHud by default): Valheim's small map keeps working;
its frame art is hidden (map picture, pins, markers, wind arrow and masks are kept) and a Ragnarök
frame drawn around the map: dark chamfered plate, gold hairline rim, corner gems, crimson north mark,
and the biome name in gold capitals. Everything is restored on Vanilla or unload.

**Notifications** (`[HUD.Notifications]`, FollowHud by default): Valheim's unlock messages (new
recipe, piece, station, material, trophy, item) become cards with the item in a gold diamond, and
its top-left messages (pickups, toggles) become slim rows; repeated pickups merge with a running
count. Placement and backdrop like the other HUD parts; centre messages stay Valheim's; everything
still goes to the message log. The settings page has a **Test notifications** button.

**Compass** (`[HUD.Compass]`, either HUD style): a heading strip at the top centre that slides with
the camera; N in crimson, cardinals in gold, ticks every 15°, the bearing in degrees under the notch.
Markers under the strip, each with its distance: your bed (gold house, any distance), the nearest six
portals within `PortalRange` (blue ring, from `ZDOMan.GetPortalList`), and ships within 400 m that you
are not aboard (longship), and other players with their names (yellow figure), only those who share
their position on the map (`ZNet.GetPlayerList`, `m_publicPosition`).

**Crafting panel** (`[Inventory.Crafting] Style`, FollowHud by default): drawn over Valheim's crafting
panel, which keeps running unseen (list, selection, requirements, timing, multi-craft, upgrade, repair).
Station header with Craft / Upgrade tabs, a search box (game keys are ignored while it has focus),
category chips (weapons, armour, ammo, tools, food & meads, materials, other; none lit = all) and a
"craftable only" chip; recipe list on the left, the recipe, its requirements and the craft button
(progress bar while crafting) on the right.

With Valheim's crafting panel the same search and filters sit above its recipe list in Valheim's style
(`[Inventory.Crafting] VanillaFilters`). Both panels share one filter state (`Ui/CraftFilter.cs`), which
filters Valheim's own recipe list after it is built. With the vanilla HUD, filled provision slots show
as Valheim hotbar slots just past the hotbar, with their keys (`Ui/VanillaProvisionBar.cs`).

**Info panel** (follows the inventory style): the wooden board above the inventory, with the character
name and the Compendium, Skills, Trophies, Achievements and PvP buttons, redrawn as a Ragnarök plate
over the hidden original (`Ui/InfoPanel.cs`). Each Valheim button gets a twin with its glyph and tooltip
that presses the original, so buttons added by other mods appear too.

**Compendium** (`[Inventory.Compendium] Style`, FollowHud by default): Valheim's texts dialog redrawn
as a Ragnarök screen over the (hidden, still running) original. Tabs to Skills and Trophies on top;
chapters on the left (Journal: active effects, log, stats; Hugin's Lessons; Runestones; Notes), the
open one expanded and the rest fading; the entry in the middle; a diagonally cut art panel on the
right with rising embers and the glyph of the boss the entry mentions (else your Forsaken power).

**Skills** (`[Inventory.Skills] Style`, FollowHud by default): Valheim's skills dialog in the same frame
(`Ui/BookWindow.cs`, shared with the Compendium). Every skill on the left with its level and a mastery
line; the chosen one in the middle with its level large, the bonus from gear and food, progress to the
next level, mastery towards 100 and its description; its glyph in the art panel.

**Trophies** (`[Inventory.Trophies] Style`, FollowHud by default): Valheim's trophy panel in the same
frame (`Ui/TrophiesBook.cs`). Every trophy in the game as a tile wall in Valheim's order: the ones you
have taken in colour, the rest as dark shapes. The chosen one's name, lore and use in the middle, with
a collection bar (taken / total); its glyph in the art panel.

**Achievements** (`[Inventory.Achievements] Style`, FollowHud by default): Valheim's achievements panel
in the same frame (`Ui/AchievementsBook.cs`), with a fourth tab. Every badge as a tile wall in Valheim's
tier order, unlocked ones lit. The chosen one's name, description and conditions are in the middle, read
from the same player stats Valheim's details use. Valheim's secrecy is kept: a locked secret stays hidden
and an unmet condition shows as ???. Valheim's "cheated" note shows bottom left when it applies.

**Sort** (under Quick Stack, both inventories; `[Inventory.Sort]`): A-Z, Type or Weight. Partial
stacks of the same item are merged first, then the grid is laid out in reading order. The hotbar row
stays as it is by default (`KeepHotbar`); character, provision and quiver slots never move.

**Provision bar** (`[Inventory.Provisions]`): filled provision slots show beside the food diamonds
in the Ragnarök HUD with their hotkeys (default Left Alt+1…6, rebindable in the settings window).
A key eats or drinks from its slot like a hotbar key; Valheim's hotbar does not also fire on it.

**Better Archery** (optional, soft dependency): when its quiver is enabled, the character panel shows
the worn leather quiver in the middle gear cell and an **Arrows** column with the quiver's three
slots (Better Archery's own rows; its ammo-only rule and Alt+1-3 keys apply). The grid is its base
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

## Server settings

The server (a dedicated server, or the player hosting) decides the gameplay settings for everyone
who joins: `[NearbyChests]` Enabled, Range, Crafting, Building and Stations; `[Inventory.QuickStack]
Enabled`; `[Inventory]` CharacterSlots and ExtraUtilitySlots. It sends its values after each
player's handshake and again whenever one changes. While its `[Server] LockSettings` is on (the
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

Edit the `[NearbyChests]` section in the config and press **F7**:

```ini
[NearbyChests]
Enabled = true
Range = 20
Crafting = true
Building = true
Stations = true
```

See [the gameplay test checklist](docs/NEARBY-CHESTS.md).

## Ready on this machine

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
