# Plant hover status

F10 → HUD → Plants → Show Growth Status (on by default, personal display setting).

The normal plant hover text keeps Valheim's localized health reason and adds growth status. Healthy planted crops and saplings show an estimated countdown based on their seeded GetGrowTime result and replicated planting timestamp. Unhealthy plants show Growth blocked instead of a misleading countdown. Mature plant pickables show Ready to harvest. Regrowing plant pickables show their respawn countdown when their remaining collider can be hovered; some disappear completely until they respawn.

Time is elapsed world seconds displayed as hours/minutes/seconds, not accelerated day/night hours. Timers are estimates: growth and respawn wait for normal world updates and valid conditions. A completed timer says it is awaiting an update, rather than claiming that an unconverted plant is harvestable. Hovering does not trigger growth, health updates, respawns or ownership changes. The added text follows the existing hover display in either HUD style.

Build and startup/reload checks validate patch loading. Actual hover appearance, unhealthy plants, completed growth and remote-world timing still need gameplay verification.
