# Bound mount and whistle summon — proposed design

The 0.5.0 prototype now implements this design. See [controls and test steps](MOUNTS.md).
Only the earlier movement primitive has passed an isolated in-game test; the new
service, controls and multiplayer flow still need runtime verification.

## Confirmed choices

- One exclusively bound mount per player. Binding a new mount releases the previous
  bond; another player's bound mount cannot be claimed.
- Mounts stay where left after dismounting and using a portal. Whistle later to
  bring the same mount to the player; no automatic despawn or home relocation.
- First version uses an existing suitable player gesture plus whistle audio.
  A custom hand-to-mouth animation can be added later.
- Summoning is unavailable during combat.
- Initial summon destinations are restricted to safe outdoor ground. Interior
  destinations, including buildings and dungeons, are outside the first version.
  Distance remains unlimited within the same world.

## Player interaction

Approach a living, tamed, saddle-equipped rideable creature. Its hover text offers a
rebindable Bind Mount action. A short hold confirms binding; an owned bond offers
Release Bond instead. Binding does not replace native mounting, petting or feeding.
Show the bound mount's name and a Release action in settings so a missing/unloaded
mount does not prevent changing the bond. Releasing leaves the animal intact.

A separate rebindable Whistle shortcut plays the gesture and sound. After a short
wind-up, the mount appears on safe ground approximately 3–5 metres beside the
player. No automatic mounting. The mount retains its identity, name, health, tame
state, saddle and persistent data; summoning does not heal, duplicate or resurrect it.

Distance is unlimited within the current world, including across unloaded sectors.
World identity is part of the bond; a character cannot import an animal from another
world. A dead/destroyed animal invalidates its bond. The bond survives disconnects
and restarts; releasing a bond does not affect ordinary taming or riding access.

## Recommended initial rules

These are proposed defaults, not additional user-confirmed choices:

- Player must be alive, dismounted, outside an attack/dodge/teleport animation and
  out of combat. Use a 15-second quiet period after attacking, taking damage or
  being targeted, and check active nearby enemy aggression.
- Validate both when whistling starts and immediately before the move. Damage,
  attacking, death or teleporting during the wind-up cancels the request.
- The mount must also be alive, not fighting and not ridden by anyone. Do not
  forcibly dismount another player or rescue a mount from an active fight.
- A short server-controlled summon cooldown, initially 10 seconds. No invented
  item/currency requirement. Notify the player why a summon was refused.
- Enforce the confirmed outdoor-only arrival rule with room for that creature.
  Refuse unsafe water, obstructions, inaccessible warded ground, building interiors
  and dungeon interiors. Do not move the
  mount if no safe arrival point exists. This limits unsafe destinations rather
  than distance; dungeon/boat support would be a separate, explicit extension.
- Removing the saddle prevents summoning until it is restored. Do not create or
  grant saddles. Existing compatible rideables are the initial scope; other mods'
  mounts require a valid Tameable, saddle and supported network representation.

## Multiplayer authority and persistence

This feature requires the mod on the host/dedicated server and participating client.
The server owns bond decisions, combat gates, cooldowns and summon transactions.
Use the existing authenticated networking/config synchronization pattern; client
preferences cover only shortcuts and presentation. No client-only distant teleports.

Persist the world ID, mount ZDOID and bonded player character ID in server-owned
records; store the reciprocal bond on the mount where appropriate. Use player IDs
for bonds, not the mount's current networking owner, which can migrate. Enforce
one player per mount and one mount per player, with validated proximity, tame/saddle
state and authenticated requester identity for binding/releasing.

For a loaded mount, obtain cooperation from its authoritative owner before moving.
For an unloaded mount, resolve its existing persistent ZDO and coordinate its move
and destination-sector instantiation with the server. Never spawn a replacement
animal as a shortcut. A transaction lock/request ID prevents simultaneous summons,
stale replies and duplicate completion. Disconnects/restarts must leave exactly
one surviving animal and a recoverable bond.

Server combat checks must use available authoritative attack/damage/aggro data,
including owner-provided data where necessary; a client boolean is insufficient.
Unloaded mounts must not bypass recent-combat checks merely by leaving loaded
range. Persist the needed combat timestamps/state and reject uncertain transitions.
This follows cooperating modded-peer authority; it is not an anti-cheat guarantee.

## Jotunn and assets

Use the existing Jotunn dependency for rebindable input and any necessary cosmetic
asset loading. Keep server settings in the existing synchronization system. First
verify that a native gesture can be reused without interfering with player control.
Use original or appropriately licensed whistle audio; record provenance. A true
whistle animation would require a compatible animation asset and a separate pass.
No new mount species, saddle item or crafting recipe is part of this feature.

## Implementation phases

1. Prove identity-preserving movement: loaded local mount, mount owned by another
   peer, then unloaded-sector mount across a long distance. Check destination
   loading, saddle/name/health persistence, save/reload and exactly-one-animal
   behavior. Failure here blocks the unlimited-distance promise; do not disguise it
   with respawn/copy behavior.
2. Implement server-owned bind/release records and migration/validation. Test two
   players competing for one mount, switching bonds, death, saddle removal,
   reconnect, world changes and server restarts.
3. Implement the validated summon transaction, safe arrival, combat stamps,
   cooldowns, cancellation and owner/disconnect recovery. Test both client and
   dedicated-server authority before exposing normal gameplay controls.
4. Add hover hints, settings status/release, gesture, audio and feedback through
   the existing UI/Jotunn lifecycle. Keep all input registrations, hooks, queues
   and owned objects safe across scene changes and F6 reload.
5. Validate in an isolated multiplayer test world: portal then whistle, unloaded
   base, active fights, occupied mounts, blocked destinations, concurrent requests,
   logout mid-request, save/reload and compatibility with installed terrain mods.

## Evidence and limits

Inspected the user's installed game assemblies for Tameable.HaveSaddle/HaveRider,
Character.TeleportTo, Player.StartEmote/IsTargeted, BaseAI.GetTargetCreature,
ZDOMan.GetZDO/ForceSendZDO, ZDO.SetPosition and ZNet.GetWorldUID. These establish
the required primitives, not a completed implementation. Availability of a method
does not establish safe unloaded-sector transfer by itself; phase 1 must prove it.

The first transport prototype passed an isolated single-player runtime test on
2026-10-21. A saddled, tamed lox moved while loaded, streamed out in a distant
sector, and returned using the same ZDO/network identity. Saddle, name and health
were preserved, with exactly one animal after return. The test uses a temporary
character and world under the external mount-checks directory.

The installed game's base Character.TeleportTo is a stub for creatures. The
prototype instead moves the owner-controlled transform and persistent ZDO,
including its sector index. It never clones the animal. Server transfer of an
unloaded animal is restricted to unowned or offline-owned records; connected
owners still require a cooperative transaction before multiplayer support.

Exclusive bond storage and atomic world-specific persistence have automated
checks. Normal bind/whistle controls, server transactions, cooperative remote-owner
movement, gesture/audio and the settings release action are implemented and build
successfully. Runtime verification of this new flow, save/reload movement and
multiplayer testing remain outstanding. The user requested leaving Valheim running
and skipping the extended isolated test for now.
