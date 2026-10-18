# Ragnarök local test build

Install BepInExPack_Valheim 5.4.2351, then extract this archive into your Valheim folder.
The plugin belongs in BepInEx/plugins/Ragnarok/Ragnarok.dll.

Do not install a second copy in BepInEx/scripts. ScriptEngine is for development only.
Version 0.2.0 enables crafting, building and station supply from chests within 20 metres.
Your inventory is used first for each material, then eligible nearby chests.
Press Use to fuel/feed stations; nothing refills automatically.
Press F8 to hide the badge. Configuration is in BepInEx/config/com.kernelpanic.ragnarok.cfg.
Press F7 to reload that configuration. F6 code reload requires the development setup.

The NearbyChests config section controls Enabled, Range, Crafting, Building and Stations.
Wards, private ownership and occupied chests are respected. Carts, ships, tombstones
and naturally generated loot containers are excluded.

For multiplayer, install the same version on all clients and the dedicated server.
Use matching configs; settings are not server-synchronized. A shared chest may need
an ownership handoff, in which case wait briefly and repeat the action.
Use a test character/world first: full gameplay and multiplayer testing remain pending.
