# Ragnarök development

- This is a standalone Valheim C# project; source lives in D:\RagnarokDev\Source and builds run on Windows.
- Run Windows builds with powershell -NoProfile -ExecutionPolicy Bypass -File ./dev.ps1 build.
- Local machine paths live in ignored dev.local.json.
- Compile against the user's installed game assemblies; do not commit or redistribute them.
- Keep development saves and plugins isolated from the ordinary Steam installation.
- ScriptEngine development plugins go in BepInEx/scripts; release plugins go in BepInEx/plugins.
- Every feature must clean up patches, subscriptions and owned objects on unload.
- Config synchronization and client/server authority must be designed before gameplay changes ship.
- Keep dependencies minimal. Add Jötunn/Unity tooling when a concrete feature requires it.
- Record sources and licenses when incorporating outside code or assets.
- Verify builds and document whether a change actually received an in-game test.
