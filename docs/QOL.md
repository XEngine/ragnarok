# Powers and harvesting

## Sailing and ballistas

F10 → Quality of Life → Sailing / Ballista. All settings below are synchronized and locked by the server when Server.LockSettings is enabled. Install the mod on the host/server and every client because physics and turret AI may be owned by a client.

```ini
[Gameplay.Sailing]
ForwardPropulsionMultiplier = 1
ReversePropulsionMultiplier = 1
RudderSpeedMultiplier = 1
SteeringStrengthMultiplier = 1
WaterImpactDamageMultiplier = 1

[Gameplay.Ballista]
IgnorePlayers = false
InfiniteAmmo = false
FireRateMultiplier = 1
TurnSpeedMultiplier = 1
TargetRangeMultiplier = 1
ProjectileSpeedMultiplier = 1
ProjectileSpreadMultiplier = 1
```

These are vanilla-preserving defaults. Multipliers apply to each ship/turret's own original values, including later-game ships that use the Ship component. They take effect on the next calculation; prefab assets and saved ammo counts are not rewritten.

- Sailing propulsion, rudder speed and steering strength accept 0.1–5x. Forward affects both sail force and forward rowing; reverse affects reverse rowing. Propulsion is force, not a guaranteed velocity: wind, waves and drag still matter.
- Water impact damage accepts 0–10x: 0 disables wave-impact damage, 0.5 halves it. Collision, enemy, capsizing and biome-hazard damage remain unchanged.
- Ballista fire rate and turning speed accept 0.1–10x. Fire rate 2 halves the cooldown; aiming and target acquisition remain necessary.
- Target range and projectile speed accept 0.1–5x. Detection still respects visibility and firing angles. Bolt speed is also used for target-leading calculations; projectile lifetime is unchanged.
- Projectile spread accepts 0–5x: 0.5 halves random spread and 0 removes it. This does not guarantee hits against moving targets or compensate for every trajectory effect.
- IgnorePlayers stops selecting and firing at player targets, including a previously selected player. Already-fired projectiles and stray bolts aimed at another target can still hurt players. Tamed-creature and trophy targeting rules otherwise remain unchanged.
- InfiniteAmmo requires at least one loaded bolt to choose ammo type, then shots do not consume it. Turning the option off resumes normal spending of the stored ammo. Native turrets that already use default unlimited ammo retain that behavior.

Verification: build and game startup/reload verify patch installation against the installed game methods. Manual multiplayer checks remain: ship handling in both directions, water-impact damage, changing ship/turret ownership, player target rejection, ammo preservation when toggling, and ballista range/rate/trajectory.

## Stamina and eitr costs

F10 → Quality of Life → Stamina & Eitr. Server-synchronized settings:

```ini
[Gameplay.ResourceCosts]
RunStaminaModifierPercent = 0
AttackStaminaModifierPercent = 0
JumpStaminaModifierPercent = 0
AttackEitrModifierPercent = 0
```

Each defaults to 0 (normal). -50 halves the cost, -100 removes the cost, +50 adds 50%, and +100 doubles it; range -100 to +900. Attack settings include weapon draw and reload. They do not change blocking, dodge rolls, swimming, building, eitr regeneration or maximum resource pools. Existing world, skill and equipment modifiers still apply, and vanilla action prerequisites remain (zero cost does not bypass animation, movement or minimum-resource gates). Changes apply to the next cost calculation; a queued reload retains its already-calculated drain until requeued.

## Skill experience

F10 → Quality of Life → Skill Experience, or server config:

```ini
[Gameplay.Skills]
ExperienceGainModifierPercent = 50
```

Default 0 means normal XP. +50 awards 1.5x, +100 awards 2x, -50 awards half, and -100 stops XP gain. Range: -100 to +900 (up to 10x). Applies to all skills and multiplies world/buff scaling. Existing levels, partial progress, the level-100 cap and vanilla's discarded XP overflow on level-up remain unchanged. This setting is server-synchronized and takes effect on the next XP award. Runtime checks exercise the patched game XP method with positive, negative and zero-gain settings, world scaling and the level cap.

## Skill loss on death

F10 → Quality of Life → Death Penalty, or the server config:

```ini
[Gameplay.Death]
SkillLossModifierPercent = -50
```

Default 0 preserves normal loss. -50 halves the normal level penalty, -100 disables it (also preserving partial skill XP), and +100 doubles it. The range is -100 to +100. This scales the loss after world difficulty modifiers: if the normal penalty is 5%, -50 makes it 2.5%, not 50% of total skill levels. With a nonzero penalty, vanilla still clears progress toward the next skill level. Death-protection checks are retained; the separate full skill-reset world rule is not overridden. The setting is synchronized and locked by the server like other QoL settings.

Configure these in F10 under Quality of Life, or in `BepInEx/config/com.kernelpanic.ragnarok.cfg` on the host/server:

```ini
[Gameplay.ForsakenPowers]
DurationMinutes = 10
CooldownMinutes = 15

[Gameplay.Harvesting]
YieldMultiplier = 3

[Gameplay.Server]
LockSettings = true
```

The power defaults are 10 and 15 minutes. Harvesting defaults to 1x; the example enables 3x (allowed range 1–10). Powers use minutes; duration accepts 0.1–60 and cooldown accepts 0.1–120. Changes to powers apply on the next activation, including the duration of buffs received by nearby players. Existing cooldowns are not reset.

With LockSettings enabled (the default), the host's values are synchronized and locked for clients. Install Ragnarök on the server and all players: harvests execute on the network owner of the plant or hive, which can be a client. Dedicated servers reload edited configuration automatically; local hosts can use F10 or reload with F7.

The harvest multiplier covers harvestable crops plus berries, mushrooms, flowers, thistle and beehive honey. It multiplies primary yield after world resource modifiers and plant skill bonuses. Extra unrelated drops, mining, enemies, loose-item pickup, hive capacity and honey production speed are unchanged. Honey uses normal extraction and plants use normal drops/auto-pickup; inventory space and auto-pickup settings still apply. Picking up and dropping an existing stack does not multiply it again.

Manual multiplayer checks: set 3x on the host, confirm the client setting is locked, harvest one berry and extract a hive, then transfer area ownership to another client and repeat. Test a shared Forsaken power for a 10-minute buff and a 15-minute caster cooldown. Test 1x and changed world resource rates separately.

## Processing stations

Each station has a separate F10 Quality of Life page and server-synchronized config section. Example:

```ini
[Gameplay.Smelter]
FuelCapacity = 50
InputCapacity = 40
SpeedMultiplier = 2
```

Capacity is an absolute item count (1–1000); **0 preserves the original capacity**. Speed defaults to 1 and accepts 0.1–20; 2 halves the processing time. Settings apply to existing stations and work in progress. Fields are restored on mod unload; saved queues, fuel and timestamps are not rewritten.

| Section | Capacity settings | Speed |
| --- | --- | --- |
| Gameplay.Smelter, Gameplay.BlastFurnace, Gameplay.EitrRefinery | FuelCapacity, InputCapacity | SpeedMultiplier |
| Gameplay.CharcoalKiln, Gameplay.Windmill, Gameplay.SpinningWheel | InputCapacity | SpeedMultiplier |
| Gameplay.SapExtractor, Gameplay.Beehive | OutputCapacity | SpeedMultiplier |
| Gameplay.Fermenter | None (one batch) | SpeedMultiplier |
| Gameplay.Oven | FuelCapacity | SpeedMultiplier |
| Gameplay.CookingStations | None (model-defined slots) | SpeedMultiplier |

Wind, roof, smoke, fire and root-resource requirements remain in effect. Smelter fuel cost per product is unchanged. Oven fuel burn rate remains unchanged; cooking and burning deadlines both accelerate. Fermenter yield is unchanged. Beehive production speed and storage are separate from the harvesting yield multiplier.

Lowering capacities does not delete saved contents while Ragnarök is active; stations must drain below their new limit before accepting more. Removing the mod returns to vanilla behavior, including vanilla's honey-storage clamping. All players and the server need the mod for consistent behavior when station ownership changes.

Runtime checks exercise defaults, live smelter capacity edits, repeated speed changes, capacity reset and unload restoration on isolated components. Manual checks still needed: fill beyond vanilla capacity, reconnect/reload the world, transfer station ownership between clients, and time output for each station type.
