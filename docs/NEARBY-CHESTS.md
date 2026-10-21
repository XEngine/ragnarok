# Nearby Chests

One feature covers crafting, upgrading, building, station supplies, Quick Stack and
Ctrl+F item search. Default: enabled with a 100-metre server maximum.
`QualityOfLife.NearbyChests.Range` is the server maximum (1-500 metres).
`PersonalRange` is each player's preferred radius; the effective radius is the
smaller of these values. All chest actions, including search, use this same effective radius. Only loaded,
accessible player-built chests are available; extending the radius does not load
remote bases. Chest sorting remains limited to the currently open chest.
This is an original implementation using the installed Valheim APIs and Harmony;
no third-party mod code is included.

## In-game acceptance checks

Use a separate local test character/world. Place chests with the hammer so they are
player-built, then close them. Learn the materials normally before trying recipes.

1. Put wood/stone/resin/coal/copper ore in closed chests within the configured radius. Carry only the
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
   Choices follow vanilla's order over carried items first: carry an ingredient that
   appears later in the recipe while a chest holds an earlier one; the carried one is
   used. Only when nothing carried qualifies do chests take part, again in vanilla order.
6a. Windmill/smelter/kiln/cooking station/fermenter with Use (no item selected): carry
   one accepted input while a chest holds another that vanilla lists first. The carried
   input goes in. With nothing carried, the chest input vanilla lists first goes in.
7. Change the shared range and press F7: crafting, building, station supply, Quick
   Stack and search must agree at the boundary. Disable Enabled: all five stop,
   including an active search/guide. Re-enable and verify they return.
8. Press F6 repeatedly. Build and fuel once afterward: no doubled consumption.
9. Save and reload the world. Verify the reduced chest contents persist.

## Two-player checks (not yet performed)

- Install the same build on both clients and the server. The server supplies the
  feature permissions and maximum range; personal ranges may differ.
- Set the server maximum to 25m and A's personal range to 10m. A must see/use stock
  at 10m or less, not stock at 20m. Raising A's preference above 25m must never
  allow stock beyond 25m. Give owner B a 5m personal range: B must still grant A's
  valid request within the server's 25m cap, independently of B's preference.
- Disable Nearby Chests on the host, including with
  `Gameplay.Server.LockSettings=false`; all chest actions must stop on clients
  and clients must be unable to edit the switch. Host F6 reload must resynchronize connected clients.
- Player B opens then closes a public chest and stays idle. Player A crafts from
  its stock. Craft progress must wait for the handoff, then complete with the item
  in A's inventory and exactly the recipe cost removed. Station supply still uses
  a retry after a pending handoff.
- Keep B's chest open: A must not pull from it. Close it and retry.
- Test B's private chest and a ward that excludes A: A must not see/spend that stock.
- Both players simultaneously try to spend the last resource. Total resources must
  be conserved, including after a world reload. Ownership is checked before spending;
  this concurrency behavior still needs validation with real peers.

## Implementation boundaries

Successful-craft notification regression: with player B idle and owning a chest
containing the recipe ingredient, give A enough stock in their backpack or A-owned
chests. A's craft must not display a pending-handoff message, including when it
consumes the last available stock. Repeat with upgrades and multi-craft. Then give
A insufficient owned stock but enough in B's chest: progress must wait with the
pending message, then continue automatically after the handoff. Cancel during the
wait: no output and no ingredients spent. With an unreachable/incompatible owner,
the operation must cancel after eight seconds with a timeout message; no item or
resources may move. Repeat after disabling Nearby Chests and with no-cost mode.

The mod scopes Inventory count/find/remove patches to vanilla crafting, building and
station methods. It does not make chest contents generally appear in the backpack,
unlock recipes, provide remote equipment/ammunition or change station capacities.

Preview queries can include remote stock. Actual consumption only uses locally owned
chests, refreshed from their network state. When needed, a custom RPC asks the current
owner to hand over the chest using the game's owner-approved handoff sequence.
The owner checks the sending peer against the player, distance, privacy, wards and
occupancy. No client unilaterally claims a chest. Consumption runs synchronously on
the game thread and uses normal Inventory removal methods, preserving save callbacks.

The host/server always synchronizes and locks Enabled and Range, including when
`Gameplay.Server.LockSettings` is off. PersonalRange can only lower that shared cap.
Search shortcut and map-marker display are personal and live
on the same Nearby Chests settings page. Search uses the shared registry and
read-only inventory refresh; it never requests ownership. In-use chests are excluded
from search as well as resource discovery. Quick Stack may fill the currently open
owned chest, but it must still be an accessible player-built chest within range.

Migration preserves the existing Nearby Chests Enabled, Range and PersonalRange.
Old crafting/building/station switches and separate Quick Stack/search switches and
search radii are removed. Shortcuts and marker preferences migrate to the
combined section. Legacy KeepWeapons, KeepConsumables and KeepTools options are
removed; the selected inventory category controls stacking. Both clients and the server should use 0.4.0: older versions have
independent permissions that cannot represent the combined policy.

The granting owner enforces the server maximum, and the consumer additionally
enforces its own smaller radius; privacy, wards, occupancy and ownership checks
remain in place. These are policies for cooperating modded peers, not anti-cheat
protection against modified clients.

Read-only resource counts are shared for up to 0.2 seconds, invalidated when the
player/range/world level changes or an executing scope completes. Actual spending
uses fresh counts, access and ownership checks. Recipe/station gameplay and
network transfer tests require an actual world and are separate from menu smoke tests.
