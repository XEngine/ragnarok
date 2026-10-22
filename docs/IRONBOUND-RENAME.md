# Ironbound 0.5.6 alpha

Ironbound is the new name for the mod previously published as Ragnarök. The
repository, source namespaces/projects, DLL, plugin GUID, configuration, UI,
translation catalog, package icon and public release materials now use Ironbound.
The local development workspace/saves remain in their existing RagnarokDev paths.

## Upgrade

1. Close the game/server before updating a normal release installation. Back up
   saves and configuration. Remove all legacy `Ragnarok.dll` copies from both
   `BepInEx/plugins` and `BepInEx/scripts`; do not load both mods together.
2. Install `BepInEx/plugins/Ironbound/Ironbound.dll` from the 0.5.6 alpha package.
   Keep BepInExPack and Jötunn installed. Update all participating clients/servers.
3. The first start copies `com.kernelpanic.ragnarok.cfg` to
   `com.kernelpanic.ironbound.cfg` if the new file does not exist. The old file
   remains intact. Existing Ironbound configuration always wins.
4. Saved `Style = Ragnarok` values for this mod's HUD/panels convert to
   `Style = Ironbound`. Other configuration entries and custom text remain intact.
5. Mount bonds, first-bond XP history, completion receipts and tombstone reclaim
   records keep their original persistent identity. You do not need to rebind a
   mount because of this rename.

## Compatibility identifiers

Some internal identifiers intentionally keep their legacy spelling: existing RPC
names, animal ZDO identity/combat keys, receipt/reclaim keys, ledger file headers
and the saved-data directory `ragnarok/mount-bonds`. Renaming them would strand
existing world/character data or break delivery of completed actions. They are
compatibility contracts, not user-facing branding. Plugin GUID is now
`com.kernelpanic.ironbound`; normal BepInEx loading rejects simultaneous legacy
Ragnarök installation.

## Configuration window

F10 now uses the same quiet charcoal/iron surfaces as the HUD: softly rounded
frames, thin iron edges, muted parchment labels and restrained gold active/focus
states. Buttons, segments, rows, text fields and switch keycaps share that finish.
The header carries the Ironbound name and alpha version. The appearance changes
do not change the setting values, server locks, navigation, scrolling or key capture.

All generated frame textures/sprites remain owned by Theme and are disposed on
unload. No external art or fonts were added. Live visual verification of the new
window is still pending; automated checks cannot confirm actual in-game spacing.
