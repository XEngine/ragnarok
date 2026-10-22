# Ironbound

**A configurable HUD and quality-of-life overhaul for Valheim, by KernelPanic.**

**Open source under MIT. Testers and contributors welcome!**
[Report a bug or suggest a feature](https://github.com/XEngine/ironbound/issues/new/choose),
[send a pull request](https://github.com/XEngine/ironbound/pulls), or read the
[contribution guide](https://github.com/XEngine/ironbound/blob/main/CONTRIBUTING.md).
Testing reports, translations, compatibility fixes and UI improvements all help.
If you enjoy the mod, share it with your Valheim group or star the repository.

Bring your vitals, inventory, storage and mount companion into one interface.
Use the custom Rune & Iron style or keep the vanilla HUD and choose individual
features in the settings.

**Renamed from Ragnarök in 0.5.6.** Remove old `Ragnarok.dll` copies before installing
`Ironbound.dll`. Your old config is copied forward on first start if an Ironbound
config does not already exist. Existing mount bonds and progression are preserved.
The F10 window now matches the HUD's charcoal, iron and gold frame theme.

**0.5.6 is a alpha.** Automated checks and local development testing have been
performed, but the latest HUD layouts, mount temperament and every multiplayer
ownership scenario have not all received an in-game test. Back up saves and try
a test world first. This package contains the Ironbound plugin, not a modpack.

## Features

- **Rune & Iron HUD:** custom vitals, character portrait, food beside Forsaken
  power, grouped passive/ongoing effects and debuffs below the compass.
- **Clear feedback:** separate pickup and recipe notifications, skill milestone
  cards beside the minimap, crosshair ammunition count and temporary tamed-animal
  map dots for loaded animals in explored areas.
- **Recovery numbers:** stamina recovery per second beside its bar and sustained
  food/status health regeneration beside the health bar.
- **Nearby storage:** craft, build, supply stations, quick-stack and search chests
  using one shared range and normal access/ward checks. Personal range can reduce
  the server limit. Station supply is manual, not automatic.
- **Inventory and crafting:** custom panels, recipe search/filtering, equipment
  and provision slots, sorting and a weapon dial. The hotbar uses its normal top row.
- **Mount companion:** bind one living, tamed, saddle-equipped mount per player;
  whistle to summon that same animal onto safe outdoor ground, including from
  unloaded areas. Requires Ironbound on the server and clients.
- **Riding progression:** summon cost falls from 10% maximum stamina at Riding 0
  to zero at 100. First bonds and successful summons award limited Riding XP.
  Hungry or frightened mounts may occasionally hesitate or refuse; refusal costs
  no stamina. Combat, rider and destination safety checks still apply.
- **Configuration:** F10 settings with personal HUD choices and server-controlled
  shared gameplay rules. Selected interface text supports English and Turkish;
  some feature labels/descriptions remain English.

## Requirements

- Valheim: compiled against the installed **1.0.20** game assemblies.
- [BepInExPack Valheim 5.4.2351](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
- [Jötunn 2.30.2 or newer](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).

Install the same Ironbound build on all participating clients and the dedicated
server for shared gameplay features. The server runs the gameplay/synchronization
side without creating the graphical interface. Other mods are optional and are
not bundled. Compatibility with an arbitrary mod list is not guaranteed.

## Installation

**Thunderstore/r2modman:** install Ironbound into your Valheim profile, let the
manager install dependencies, and launch the modded game.

**Manual/Nexus:** install BepInExPack and Jötunn first. Extract the archive into
your Valheim directory so the DLL ends up at
`BepInEx/plugins/Ironbound/Ironbound.dll`. For a managed profile, use that profile's
folder. For a dedicated server, use its installation folder.

Keep only one Ironbound DLL. Remove previous copies from both `BepInEx/plugins`
and `BepInEx/scripts` while the game/server is closed. ScriptEngine is not required
for this release. Restart after updating a release DLL.

The default HUD is **Vanilla**. Open **F10 → HUD → Style → Ironbound** to enable
the custom interface, then adjust feature pages to taste. The configuration file
is `BepInEx/config/com.kernelpanic.ironbound.cfg`.

## Default controls

| Key | Action |
| --- | --- |
| F10 | Open/close settings |
| H | Whistle for your bound mount outdoors |
| Hold Shift+H | Bind/release a nearby tamed, saddled mount |
| Alt+4 / Alt+5 / Alt+6 | Provision shortcuts |
| F7 | Reload manual configuration edits |

Other shortcuts can be inspected or rebound in F10. Release installations do not
use the development F6 reload workflow.

## Mount rules and limits

Your mount stays where you leave it after using a portal. Summoning does not
create a copy, heal it or revive a dead mount. Fighting, ridden or protected
mounts cannot be summoned. Stand on safe outdoor ground away from roofs,
dungeons and water. Some locations legitimately have no safe arrival spot.

Temperament reaction chances are 2% when content, 8% hungry, 10% frightened and
15% for both, halved at Riding 100. Most reactions delay arrival by two seconds;
the rarer refusal permits retry after five seconds. A thirty-second grace period
prevents repeated random reactions. Fear requires server-observed live AI;
unloaded saved alert flags are ignored. Hunger works from saved feeding data.

Completion receipts suppress duplicate stamina/XP delivery during ordinary
reconnects and reloads. Character and server saves are independent, so abrupt
crashes or save rollbacks are not an atomic exactly-once transaction. This is
designed for cooperative play, not as an anti-cheat system.

## Updating or removing

Back up characters, worlds and configuration before updating. Existing settings
are migrated when supported; the server defines shared mount/storage rules.

Before removing Ironbound, move items out of additional equipment, provision or
inventory slots into normal inventory/chests, save, and close the game. Remove
the DLL only after doing so. Removing the mod is not a substitute for a save backup.

## Support and credits

Report issues at [GitHub Issues](https://github.com/XEngine/ironbound/issues), with
your game/mod versions, reproduction steps and `BepInEx/LogOutput.log`. Remove
private connection details before sharing logs. Include your other mods when
reporting a compatibility problem.

Jötunn and BepInEx are installed separately. Font licenses and attribution are
included in `licenses/` and `THIRD_PARTY_NOTICES.md`. Native game icons/effects
are used from the installed game at runtime and are not included as game files.
The package icon is original procedural artwork. Code and release copy were
developed with AI assistance. See the repository for development history.
Original project code/documentation/artwork are covered by the included MIT
LICENSE; third-party fonts retain their own licenses. Legacy power PNGs removed
from the current tree remain in historical commits with undocumented provenance
and are excluded from that grant.
