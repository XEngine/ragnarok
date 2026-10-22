# Native Linux development

Run from the repository checkout on x86-64 Linux. Install Valheim's native Linux
build through Steam first. Setup detects default native Steam and Flatpak Steam
locations; use `--source-game PATH` for another library.

```bash
./scripts/setup.sh
# Optional paths:
./scripts/setup.sh --source-game /path/to/Valheim --dev-root /path/to/RagnarokDev
```

Requirements: Python 3.9+, Bash, curl, rsync, file, and the system libraries needed
by .NET and Valheim. No PowerShell, Wine, global SDK installation or root access is
required. Setup installs the exact SDK from `global.json` locally and uses the same
SHA-256-verified BepInExPack 5.4.2351 and ScriptEngine r11.2 archives as Windows.
Setup also installs checksum-verified Jotunn 2.30.2 if no copy exists in the isolated
game. Jotunn 2.30.2+ is required to build and run Ironbound; existing installations
are preserved. Keep exactly one Jotunn.dll under the development BepInEx/plugins.
Builds discover it recursively; an explicit `JotunnPath` MSBuild property can point
to another installed copy. Dependencies are not bundled in Ironbound release archives.
The SDK resolves .NET Framework reference assemblies during restore; the game runs
the resulting net472 plugin with its own Unity/Mono runtime.

Default layout:

- Source: the current checkout.
- Development game: `~/RagnarokDev/Valheim` (a separate copy, without source mods).
- Test saves: `~/RagnarokDev/saves` (always passed with `-savedir`).
- SDK: `~/RagnarokDev/dotnet`.
- Downloads: `~/RagnarokDev/downloads`.

Setup also builds `tools/LocalSaves`, an offline development tool that changes the
copied `assembly_utils.dll` so `FileHelpers.CloudStorageSupported` returns false.
The launcher verifies this guard before starting the game. This is necessary because
`-savedir` alone does not isolate Steam Cloud: Valheim can enumerate and clean up
Cloud saves from its menu. The original Steam assemblies and account-wide Cloud
settings are untouched by the guard. Setup reapplies it whenever the copy is refreshed.
It creates no runtime patches or objects and is never included in release archives.
It uses the installed BepInEx copy of [Mono.Cecil](https://github.com/jbevain/cecil),
licensed under MIT; no outside source code was copied into this repository.

Setup writes ignored `dev.local.json` and `Environment.props` files. These are
machine-specific; do not transfer the Windows versions here. It preserves existing
development BepInEx and ScriptEngine configurations. Re-running setup refreshes the
game without deleting stale files. Use a fresh development root after major updates
if necessary. Game assemblies, SDKs and saves remain outside Git.

```bash
./dev.sh doctor
./dev.sh build
./dev.sh build --configuration Release
./dev.sh test
./dev.sh deploy
./dev.sh play
./dev.sh watch
./dev.sh logs
./dev.sh smoke
./dev.sh package
```

Keep Steam running and signed in before `play` or `smoke`. `play` builds, deploys and
launches the isolated game; close Valheim before using it. `deploy` and `watch` copy
the plugin to `BepInEx/scripts`; press F6 in game to reload. F7 reloads configuration,
F8 toggles the development badge, and F10 opens settings. The configuration is in
`~/RagnarokDev/Valheim/BepInEx/config/com.kernelpanic.ironbound.cfg`.

For a Flatpak Steam installation, the launcher runs inside Steam's Flatpak runtime
so Steamworks can find the client. It grants access to the development game and saves
only for that invocation; Steam launch options and persistent Flatpak overrides are
unchanged. `steamFlatpak` in `dev.local.json` controls this behavior. Native Steam
uses the host launcher directly. Do not launch the copied binary directly from the
host with Flatpak Steam.

Optional `gameArgs` in the local JSON supplies additional game arguments, for example
`"gameArgs": ["-force-glcore"]`. Setup preserves this option when refreshing the same
development game. This office PC uses OpenGL because its Vulkan batch-menu test
crashed in Unity while changing VSync. The graphics option is local to this PC.

`smoke` runs a bounded batch-mode menu session with graphics initialized, enables ScriptEngine's file watcher
temporarily, checks initial plugin load, runtime inventory checks and unload/reload,
and Steam initialization, then stops its process and restores the watcher configuration. Logs are
`Valheim/BepInEx/LogOutput.log` and `Valheim/startup-smoke.log`. It does not load a
world or establish visual/gameplay/multiplayer correctness. `package` creates
`artifacts/Ironbound-local.zip` with only our Release DLL and installation instructions.

The Windows migration check `scripts/check-config.ps1` remains Windows-only; the
portable `test` command covers pure configuration migration and wire-name checks.
