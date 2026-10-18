# Nearby chest resources

Default: enabled, 20 metres from the player, all three feature switches enabled.
This is an original implementation using the installed Valheim APIs and Harmony;
no third-party mod code is included.

## In-game acceptance checks

Use a separate local test character/world. Place chests with the hammer so they are
player-built, then close them. Learn the materials normally before trying recipes.

1. Put wood/stone/resin/coal/copper ore in closed chests within 20m. Carry only the
   required tool. Build a piece, craft an item, refuel a fire, and add fuel/ore to a
   smelter. Check that precisely the normal cost disappears from the chest.
2. Split a recipe cost between your inventory and two chests. Verify your carried
   material is spent first, followed by the nearest chest, with no extra loss.
3. Leave the combined stock one short. Craft/build must fail without spending any
   resource or producing an item/piece.
4. Walk beyond the configured range. Chest-only resources must no longer count.
5. Fill a smelter or cooking station. Press Use again and verify no additional
   ingredient/fuel is consumed. Check cooking fire/fermenter roof requirements.
6. Test a normal upgrade, a multi-craft batch, and a recipe accepting one of several
   ingredients. Verify ingredient type, quality and quantities.
7. Change Range and each feature switch, press F7, and verify the new behavior.
8. Press F6 repeatedly. Build and fuel once afterward: no doubled consumption.
9. Save and reload the world. Verify the reduced chest contents persist.

## Two-player checks (not yet performed)

- Install the same version on both clients and the server. Use matching settings.
- Player B opens then closes a public chest. Player A crafts/feeds from it. The first
  attempt may display the pending-handoff message; repeat after a moment.
- Keep B's chest open: A must not pull from it. Close it and retry.
- Test B's private chest and a ward that excludes A: A must not see/spend that stock.
- Both players simultaneously try to spend the last resource. Total resources must
  be conserved, including after a world reload. Ownership is checked before spending;
  this concurrency behavior still needs validation with real peers.

## Implementation boundaries

The mod scopes Inventory count/find/remove patches to vanilla crafting, building and
station methods. It does not make chest contents generally appear in the backpack,
unlock recipes, provide remote equipment/ammunition or change station capacities.

Preview queries can include remote stock. Actual consumption only uses locally owned
chests, refreshed from their network state. When needed, a custom RPC asks the current
owner to hand over the chest using the game's owner-approved handoff sequence.
The owner checks the sending peer against the player, distance, privacy, wards and
occupancy. No client unilaterally claims a chest. Consumption runs synchronously on
the game thread and uses normal Inventory removal methods, preserving save callbacks.

Live config is local. Range is enforced by the granting owner and the consumer;
this is not a server-locked configuration system. Recipe/station gameplay and
network transfer tests require an actual world and are separate from menu smoke tests.
