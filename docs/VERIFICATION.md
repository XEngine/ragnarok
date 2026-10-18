# Development setup verification

Verified locally on 2026-10-17 against Steam Valheim build 25909357.

Version 0.2.0 nearby-chest verification:

- 45,562 allocation assertions, including 5,000 randomized multi-inventory cases:
  player-first ordering, split costs, conservation, no partial withdrawal on shortage.
- Debug-only runtime checks against real Valheim Inventory instances: split spending,
  stack depletion, change/save callbacks, item quality and world-level filtering,
  single-item removal and unchanged stock on a failed withdrawal.
- All Harmony patches installed, and inventory checks passed on initial load and
  after ScriptEngine reload in the real game runtime.
- Actual recipe/build/station interactions, ward/ownership behavior and two-peer
  concurrency remain manual acceptance checks in [NEARBY-CHESTS.md](NEARBY-CHESTS.md).

- Setup script: completed successfully using pinned archive checksums.
- Debug and Release: zero build warnings and errors.
- Development deployment: DLL and symbols copied to BepInEx/scripts.
- Source watcher: a C# edit triggered a rebuild and deployment.
- Runtime smoke test: Valheim loaded the plugin through ScriptEngine; a file-change
  trigger unloaded the old instance and loaded a new instance. No logged errors in
  the successful test.
- Local sharing archive: contains only our plugin plus installation instructions.
- Game process and source watcher used for testing were stopped afterward.

Run the bounded runtime check with Steam open and Valheim closed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

This launches the isolated game hidden in batch mode, temporarily enables
ScriptEngine's file watcher, verifies initial load and reload, stops only that
process and restores the tool configurations. It does not load a world. The debug
flag `-ragnarok-inventory-checks` runs isolated inventory checks on each plugin load;
these checks are absent from Release builds.

The smoke test uses the same reload method as F6 through ScriptEngine's file
watcher. Actual F6/F7/F8 keyboard interaction and badge appearance still need a
manual check in the visible game. Multiplayer is not tested.


## Module refactor, 2026-10-18

- Source reorganized into HUD, UI shared/windows, Features, Configuration, Networking and Development; earlier HUD changes retained.
- Debug and Release builds passed with zero warnings/errors.
- Regression suite passed: 45,562 allocation assertions, death-penalty boundaries, and configuration migration/legacy-wire-name checks.
- `scripts/check-config.ps1` passed against the installed BepInEx reader using temporary files: custom values, repeat migration, new-format precedence, unknown entries and original-backup preservation.
- Fixed partial-startup cleanup, dedicated-server equipment initialization, server-only config packet acceptance/RPC cleanup, atomic file-watcher notifications and owned UI texture/material cleanup.
- No in-game or multiplayer test was performed for this refactor. The development game was running and was not interrupted. Reload with F6 after deployment; inspect the new settings groups and verify host/client locking and unload behavior.
- See [ARCHITECTURE.md](ARCHITECTURE.md) for module ownership and configuration compatibility.
