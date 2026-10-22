# Ragnarok 0.5.5 alpha - installation

This archive contains the Release build of Ragnarok, our Valheim plugin.
It does not include Valheim, BepInEx, development tools, saves or personal settings.

## Windows installation

1. Close Valheim. Back up your characters and worlds before trying the mod.
2. Install BepInExPack_Valheim (the development setup uses 5.4.2351):
   https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
   Follow that pack's installation instructions. Its loader files belong beside
   `valheim.exe`; avoid an extra nested `BepInExPack_Valheim` folder.
3. Install [Jotunn 2.30.2 or newer](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)
   into `BepInEx/plugins`, on clients and dedicated servers. Keep exactly one copy.
4. Extract this archive into that same Valheim folder. The final plugin path is:
   `BepInEx/plugins/Ragnarok/Ragnarok.dll`.
5. Remove any older Ragnarok DLL elsewhere in `BepInEx/plugins` or
   `BepInEx/scripts` so only one copy loads. ScriptEngine is not required.
6. Start Valheim normally through Steam. Try a test character/world first.
7. Press **F10** for Ragnarok settings. The default HUD style is Vanilla;
   choose **HUD > Style > Ragnarok** for the custom HUD and interface.

If you use a mod manager, install BepInEx into the selected profile and copy
the archive's `BepInEx/plugins/Ragnarok` folder into that profile's
`BepInEx/plugins` folder. Launch through the manager's modded launch option.

## Settings and multiplayer

Jotunn 2.30.2 or newer is required and is not bundled in the Ragnarok archive.
Ragnarok uses its typed input and GUI APIs on clients; its GUI integration does
not initialize on dedicated servers. Existing gameplay synchronization and server
authority are unchanged. Missing or older Jotunn prevents Ragnarok from loading,
including when using ScriptEngine.

The first launch creates `BepInEx/config/com.kernelpanic.ragnarok.cfg`.
F10 changes save automatically. Press **F7** to reload manual config edits.
F6 code reload is only available in the separate development setup; restart
Valheim after replacing this release DLL.

Install the same Ragnarok build on every player and on the dedicated server,
if used. The host/server synchronizes shared gameplay settings and locks them
by default (`Gameplay.Server.LockSettings`). HUD appearance and key bindings
remain personal. A server without Ragnarok does not synchronize these settings.

Nearby Chests enable state and shared maximum range, and death-reclaim
auto equip are always server-defined and locked, including when the general
gameplay lock is off. They are under **Quality Of Life** in F10. In Nearby Chests, **Server Maximum Range** (`Range` in the file) is the limit;
**Personal Range** can reduce your radius, never increase it beyond that limit.
Your personal settings are retained in your file and restored after disconnect.
The hotbar uses only the top inventory row. The second hotbar and its switch key
have been removed; old settings are cleaned up automatically.

The [Mount companion prototype](MOUNTS.md) adds one exclusive saddled mount per
player. Hold Shift+H near a tamed, saddled mount to bind/release; press H outdoors
to summon after combat. Mount rules are always server-controlled. Update both
server and clients to 0.5.5; restart the server before trying the new controls.
Summons use Riding-scaled stamina, award limited Riding experience, and may
occasionally hesitate or refuse based on the mount's condition. See MOUNTS.md
for the exact rules and known verification limits.

Nearby-chest crafting, building and station supply use a default range of
100 metres, subject to the server's settings and chest access/ownership checks.
Your inventory is used first. Press Use to feed/fuel stations; they do not
refill automatically. If a chest ownership handoff is pending, wait briefly
and repeat the action.

## Troubleshooting and removal

- If F10 does nothing, check `BepInEx/LogOutput.log` for Ragnarok loading errors
  and verify the DLL path above. Share that log when reporting a problem.
- Back up saves before removing the mod. Move items out of extra equipment,
  provision and inventory slots into ordinary inventory/chests first, then save
  and quit. Remove `BepInEx/plugins/Ragnarok` while Valheim is closed.

This is a alpha release. Builds and automated checks pass; the latest UI and mount
temperament changes and every multiplayer ownership scenario have not all received
an in-game test. Begin with a backed-up test world. Linux runtime and Vortex
deployment have not been verified for this release.
