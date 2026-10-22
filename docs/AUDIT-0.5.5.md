# Ragnarök 0.5.5 wrap-up audit — 2026-10-22

## Assessment

Suitable for continued testing and local sharing with the existing friends group.
No critical blocker was identified in the reviewed paths. This is a targeted
source and release-readiness review, not a claim that every feature or mod
combination has received full multiplayer testing. Wider release should wait for
the live checks below.

## Scope and findings

Reviewed the pending temperament/HUD changes, plugin initialization and cleanup,
server configuration authority, nearby-storage access/ownership and spending,
mount request authentication, stable bonds, movement, completion receipts and
arrival presentation. Reviewed dependency references and Windows/Linux packaging.

Fixed during wrap-up:

- Installation instructions advertised 0.5.0 while the plugin is 0.5.5. Updated
  the install guide and README's mount version.
- Both packaging implementations omitted MOUNTS.md although INSTALL.md linked
  to it. Both now include the mount guide and changelog alongside installation
  instructions. The Windows archive was regenerated and inspected.

Reviewed behavior:

- Mount requests identify the authenticated peer's player record. A connected
  animal owner approves movement; offline/unowned transport stays server-owned.
  Movement preserves the original animal instead of creating a replacement.
- Temperament rolls once per transaction on the server. Delay repeats the final
  safety/stamina checks; refusal bypasses movement, stamina charges and XP.
  Grace is per animal and cleaned up on expiry, world change and unload.
- Completion receipts are persisted before delivery and duplicate application is
  suppressed with a per-character/world sequence. Only confirmed arrivals charge.
- Chest access checks range, container type, use state, permissions and wards.
  Inventory writes require owner-approved authority; spending rechecks access
  rather than trusting display-count caches.
- Plugin cleanup removes Harmony patches, RPC handlers, input subscriptions,
  UI objects and render callbacks. Mount effects restore visual scale/renderers;
  HUD masks restore native alpha. Existing cleanup regression coverage passes.
- Game/Jötunn references remain non-copying. Font license texts stay embedded.
  The package contains no game/dependency DLLs, saves or local settings.

## Verification performed

- Windows Debug build/deployment: zero warnings and errors (latest buff layout).
- Windows Release build/package: zero warnings and errors.
- Full pure/stub regression suites: passed, including temperament distribution,
  bonds, progression receipts, configuration migration, inventory allocation,
  status categories, hover parsing, localization and unload behavior.
- Debug and Release eager metadata/dependency checks: passed.
- Migration against installed BepInEx ConfigFile: passed, including repeat
  migration, precedence, backup preservation and shared chest range binding.
- Archive file allowlist: exactly Ragnarok.dll, INSTALL.md, MOUNTS.md and
  CHANGELOG.md (plus directory entries). DLL SHA-256 matches the Release build.
- All five embedded font-license texts retained; no tracked DLL/PDB/save files or
  dev.local.json found. Git whitespace check passed.
- Linux packaging script syntax parsed under WSL. Linux runtime/build/package
  execution was not performed in this audit.
- Existing dedicated-server log contains confirmed arrivals, and no matching
  mount-operation exception/progression-failure/cleanup-failure entries were
  observed during this check. These observations do not prove the new random
  branches or newest HUD layout were exercised.

## Remaining validation and known limits

1. **Live multiplayer:** confirm hungry hesitation/refusal, grace and Riding
   reduction; interrupt hesitation with combat; check stamina and XP on success
   versus failure. Repeat across loaded/unloaded mounts and connected owner
   handoff, including distant Ashlands destinations and reconnect/restart.
2. **Visual regression:** F6 reload, first server entry, mount/hammer overlap,
   passive/ongoing name/timer order, debuffs, food display and vanilla/custom
   style switching. Test the installed mod mix, including MyDirtyHoe and Better
   Archery. Automated coverage cannot settle visual overlap or all mod conflicts.
3. **Crash consistency:** server receipts and character saves are independent.
   Duplicate suppression is not an atomic transaction across both saves during
   an abrupt crash or rollback. See MOUNTS.md; no exactly-once crash guarantee.
4. **Cooperative authority:** client-owned stamina, Riding and destination physics
   are validated for bounds and safety but are not an anti-cheat system.
5. **Distant fear:** fear requires server-observed live AI. Unloaded saved alert
   flags are intentionally ignored; hunger still works from saved feeding data.

The user's running client was left open, consistent with their request to skip
isolated-world testing. The dev server remains running; this audit did not restart
it or touch the player's saves. Release package: artifacts/Ragnarok-local.zip.
