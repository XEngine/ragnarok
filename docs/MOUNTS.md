# Mount companion (0.5.5 prototype)

Install Ragnarok 0.5.5 and Jotunn 2.30.2 or newer on the host/server and clients.
Restart a dedicated server after updating its DLL. Development clients can use F6
after deployment. A local world acts as its own mount server. Earlier servers do
not support the mount handshake; no animal moves without server approval.

## Controls and first test

Use a test character/world first. The full bind/whistle flow has not yet received
an in-game test. The earlier movement-only prototype passed a native isolated-world
test for both loaded and unloaded mounts; automated bond/storage checks also pass.

1. Approach a living, tamed lox, asksvin or other creature with a native saddle
   equipped. Look at it and hold **Shift+H for one second**. The hover hint and
   center message identify the action.
2. Walk away, stand outdoors on clear, dry ground and press **H**. A wave gesture
   and original synthesized whistle precede the summon by 1.2 seconds at Riding 0,
   decreasing linearly to 0.6 seconds at Riding 100 by default.
   The same animal should arrive beside you, retaining its name, saddle and health.
3. A second whistle within ten seconds should be rejected. Attacks, damage or
   enemy targeting require a fifteen-second quiet period for both player and mount.
   Starting combat during the windup cancels the summon. A ridden or fighting mount
   cannot be summoned.
4. Dismount, use a portal, then whistle outdoors. The animal stays where left until
   summoned, including when that sector has unloaded. It never follows a portal
   automatically, despawns to a home, or mounts the player automatically.
5. Hold **Shift+H** while looking at your bound mount again to release it. Binding
   another available mount replaces your previous bond. Another player's bound
   mount cannot be claimed. Release leaves the animal and saddle intact.

F10 > Quality Of Life > Mount companion shows the bound name, health percentage,
saddle/recent-combat condition, Riding level, estimated stamina cost, whistle time,
rebindable keys and
a **Release mount bond** action that also works when the animal is far away or
dead. The server controls enable state, combat quiet period and success cooldown,
even with the general gameplay-settings lock off. HUD and key settings stay local.

## Riding progression

### Temperament

After the whistle and safety checks, the server rolls once for a small reaction:
2% when content, 8% when hungry, 10% when frightened, or 15% when both. Riding
reduces these chances linearly, reaching half at level 100. Two thirds of reactions
add a two-second hesitation followed by another safety/stamina check; one third
refuse the request. A refusal moves nothing, spends no stamina and grants no XP.
Wait five seconds before retrying. Any reaction grants that animal a thirty-second
grace period against further random reactions, including after releasing/rebinding.
Combat and other safety requirements still apply during grace and hesitation.

Hunger uses the native saved feeding time and prefab feeding duration, including
unloaded mounts. Fear counts only when the server has a live alerted AI; an
unloaded animal's saved alert flag is deliberately ignored because it can be stale.
The companion summary includes hunger and observed fear. Rules and random rolls
run on the host/server, with no new client-controlled gameplay options or RPCs.
Grace and the short refusal cooldown clear on server/world reload; bonds and
reward history are unaffected. Feeding helps by reducing the hunger probability.

### Costs and experience

The default summon cost is `maximum stamina * 0.10 * (1 - Riding / 100)`.
Riding 0/25/50/75/100 costs 10/7.5/5/2.5/0 percent of maximum stamina. Enough stamina
is required at the initial and final outdoor arrival checks. The server computes
the amount; only a confirmed arrival sends a completion charge. Failures,
cancellations and unconfirmed transfers receive neither a charge nor summon XP.
Stamina remains native owner-side state: if it changes between the final check
and completion, native consumption clamps it to zero. A zero world stamina rate
retains free stamina. Positive world rates are compensated to keep this percentage
exact; normal attack/resource-cost modifiers do not change the summon formula.

A confirmed summon grants a native Riding XP factor of 0.25 at most once per
five real minutes per player/world. A first successful bond with each uniquely
identified animal grants 0.5. These are XP factors, not whole skill levels, and
native skill/world/status modifiers still apply. Releasing/rebinding the same
animal never grants another binding reward; binding a different animal can.
Reward eligibility and the summon XP cooldown survive reloads and server restarts.
There is no skill requirement or distance restriction, and combat protection is
the same at every level. Milestones at Riding 25/50/75/100 add one/two/three/four
small native particle flourishes to the local arrival effect, with extra audio muted.

The server always synchronizes and locks stamina percent, XP factors, reward
cooldown and expert whistle duration. They live in QualityOfLife.Mounts with the
existing mount controls. The hover hint for a bound mount also shows the estimate;
the calling message shows the cost before arrival.

Server-owned reward history and ordered completion receipts are stored beside
the bond ledger in `<world>.txt.progress`, atomically with a backup. The client
records a per-world applied receipt sequence in its existing character custom
data before applying cost/XP. Only the oldest undelivered server receipt is sent;
matching acknowledgments advance the queue. Duplicate packets/F6 reloads do not
apply a receipt twice. Pending delivery retries on the three-second handshake,
including reconnects. A disk failure disables further mount actions until reload;
it never replays an animal move. Character saves and server saves are separate,
so abrupt process crashes are not a cross-save atomic transaction.

Mount protocol 3 is required for the requester and connected animal owner. Skill
and stamina snapshots come from the authenticated player owner, are checked for
finite/bounded values, and feed server formulas. This is the existing cooperative
Valheim authority model, not protection against a modified client lying about its
own character. No new client-only gameplay setting can override server policy.

Destinations exclude roofs, dungeons, water, steep terrain, obstructed ground and
wards without access. Move to a clearer outdoor area if no safe spot is found.
Summons remain within one world. They cannot resurrect animals, heal them or
import them from another world.

## Authority and persistence

The authenticated player connection, not a supplied player ID, identifies requests.
The animal carries a unique `ragnarok.mount.identity` string in its persistent
ZDO data. Ledger version 2 stores this marker and a cached network ID. Only the
marker resolves identity: this game's save/load can remap network IDs, so a cached
ID might later identify a building piece or a different animal. The server indexes
markers after world load; duplicate or missing markers fail closed.

The current animal owner stamps the marker before a new bond is committed. The
server requests a world save after successful marking, in addition to atomically
committing its ledger. Release leaves an unused marker on the animal; this carries
no ownership by itself. Binding another player to that animal reuses the marker
and checks exclusive ledger ownership. Old version-1 rows are preserved but never
resolved through their unsafe cached IDs; rebind once or use guarded recovery.

An administrator can prepare `<world UID>.txt.recover` beside the ledger, with
`player ID<TAB>exact display name` per line. At the next load, recovery requires
exactly one living, tamed, saddled match, an old unmarked bond, and no live animal
instance or connected owner. It stamps the existing animal and commits the new
marker without moving, healing or replacing it. Ambiguous matches are refused;
the request file is archived after processing. No name-based auto-selection runs
during normal summons.

The server persists exclusive bonds under its local saves directory:
`ragnarok/mount-bonds/<world UID>.txt`. Writes replace the whole file atomically,
retaining the previous file as `.bak`. Back up these files with the world's saves.
Corrupt or cross-world bond files disable the feature rather than resetting bonds.

The server checks replicated player/mount combat, health, saddle/rider state,
wards and bounded arrival before the gesture and again after the windup. It
authenticates the player through the connection's character ZDO and profile ID;
a loaded Player object on the server is not required. The handshake can advertise
the service even while the player's initial ZDO is still loading.

Dedicated servers do not necessarily simulate the player's area. The requesting
client therefore checks its own action/teleport/combat state and loaded terrain,
roofs, slope, water and capsule clearance twice, using a fresh server-issued
request token at each stage. The server rejects non-finite proposals, offsets
beyond 12 horizontal metres or 6 vertical metres from its replicated player
position, wrong peers/tokens, inaccessible wards and stale/disconnected players.
Local hosted worlds run the same physics checks directly. Client physics evidence
is trusted within Valheim's cooperative ownership model; the server does not claim
to independently simulate distant collision geometry.

A connected animal owner validates
and executes the move; the server may take ownership only of an unowned or
offline-owned unloaded animal. The original ZDO and its sector index move; no
replacement animal is spawned. The owner rechecks authority, health, saddle,
rider and combat when executing. Owner changes or unavailable owners fail safely.
An uncertain acknowledgment reports an unconfirmed transfer, with no automatic
movement retry; inspect nearby before trying again.

This uses Valheim's cooperative client ownership model, not an anti-cheat boundary.
Combat hooks stamp owned characters and send authenticated owner reports to the
server. Bonds and settings are server-owned; a client cannot bind for another player.
Outstanding requests, RPC registrations, input bindings, owned audio objects and
Harmony patches are cleaned up on unload/world changes as appropriate. Bonds remain
on disk. Completed owner movement is never undone by an unload or lost reply.

## Verification still needed

Player combat detection uses confirmed attack/damage/alerted-target event stamps
for the configured quiet period. Continuous engagement requires an alerted hostile
whose current creature target is that player. Passive sensing and hostiles alerted
at something else do not count; the native attack timer is excluded because it
starts at zero on spawn. Receiving damage still starts the quiet period, including
environmental damage. The HUD and local whistle validation share these rules.

Mount readiness does not treat the saved alert bit as proof of combat: it also
represents fear and can outlive loaded AI simulation. Recent attack/damage stamps
still enforce the quiet period. A loaded mount is blocked while its AI is alerted
and targets a living hostile, or while ridden. A connected owner repeats the live
checks before movement; unloaded offline records retain the combat cooldown,
health, tame, saddle and rider checks without an indefinite saved-alert lock.

Successful summons now play a local magical arrival burst for the summoning
player, borrowing the installed game's spawn effects (skill-up effects as a
fallback). A brief concealment and eased visual growth materialize the original
animal over 0.7 seconds. Only its visual child is animated; health, saddle,
identity, collider and authoritative movement are unchanged. No assets are
redistributed. Effects and borrowed renderer/scale state are cleaned up on
completion, mounting, scene changes and unload. The arrival animation still needs
an in-game visual check, including the Riding milestone layers.

The new service flow, hover/control feel, wave/audio, native physics placement,
ward behavior and two-peer transfers need an in-game test. The extended isolated
runner is `powershell -NoProfile -ExecutionPolicy Bypass -File ./scripts/check-mounts.ps1`;
it requires Valheim closed and creates its own external character/world. Also test
two players competing for a mount, a remote owner, disconnect during windup/transfer,
save/restart, a dead or unsaddled mount, roofs/water/wards, F6 reload and terrain mods.
No multiplayer runtime test has been performed for this prototype.

The wave reuses the installed game's animation. The whistle is synthesized by
Ragnarok's own code; no third-party animation, audio or source is bundled.
