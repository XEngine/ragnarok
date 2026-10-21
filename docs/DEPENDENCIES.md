# Jotunn dependency

Ragnarok requires Jotunn 2.30.2 or newer on clients and dedicated servers.

- Upstream: [Valheim-Modding/Jotunn](https://github.com/Valheim-Modding/Jotunn).
- License: [MIT](https://github.com/Valheim-Modding/Jotunn/blob/master/LICENSE).
- Distribution: [ValheimModding-Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
- Development setup archive: [Jotunn 2.30.2](https://thunderstore.io/package/download/ValheimModding/Jotunn/2.30.2/).
- Archive SHA-256: `8AAE92DA2BE0EB6820CD4CF57E2F6C1D6AD0D738D4915966E7C3D7A94E9A9B0F`.

Windows/Linux setup installs this archive's plugin files under the isolated game's
`BepInEx/plugins/Jotunn` only when no Jotunn DLL exists. Existing installations are
preserved. Build references use the installed DLL with copy-local disabled. Neither
Jotunn binaries nor upstream source are included in this repository or Ragnarok's
release archive; install the dependency separately for releases.

Integration uses the public InputManager, ButtonConfig and GUIManager APIs. No
upstream source was copied. See [architecture](ARCHITECTURE.md) for the limited
private cleanup adapter needed for ScriptEngine reloads.
