# Upload handoff — Ironbound 0.5.6 alpha

Run `./dev.ps1 package` on Windows or `./dev.sh package` on Linux after building
against your isolated installed game. Windows packaging uses Python 3; Linux uses
the existing Python workflow. Python's standard library builds both ZIPs and
validates their contents. `scripts/make-release-icon.ps1` regenerates the checked-in
256×256 icon on Windows; packaging itself needs no graphics tools.
Run `python -B tests/test_release_package.py` for metadata/icon/archive rejection
regressions. ZIP payloads match across Windows/WSL; compressed bytes may differ
between Python/zlib versions. Checksums describe the actual generated archives.

## Thunderstore

- Upload `artifacts/Ironbound-0.5.6-alpha-Thunderstore.zip` in the **Valheim** community.
- Select your own author/team namespace at upload. It is not hardcoded in the manifest.
- Package name: `Ironbound`; display title: Ironbound; version: `0.5.6`.
- Root manifest, README, changelog and icon are included. Dependencies are pinned
  to verified package strings for BepInExPack 5.4.2351 and Jötunn 2.30.2.
- Choose interface/quality-of-life/gameplay categories offered by the site, and
  mark it as a alpha in the listing. Do not classify it as a modpack.
- Check the rendered README and dependency links in the upload preview before
  submitting. A namespace/name/version already published needs a new version.

## Nexus Mods

- Create a Valheim mod named **Ironbound — HUD and Quality of Life**.
- Suggested summary: “A configurable HUD and quality-of-life overhaul with unified
  nearby storage, inventory tools, and a Riding-based mount companion.”
- Upload `artifacts/Ironbound-0.5.6-alpha-Nexus.zip` as the **0.5.6 alpha** main file.
- Paste `release/NEXUS-DESCRIPTION.txt` into the BBCode editor. Use
  `release/icon.png` as the original branding image; add genuine current gameplay
  screenshots to the gallery. The icon is branding, not a gameplay screenshot.
- Add BepInExPack Valheim and Jötunn as external requirements using their official
  links from the README. Manual installation is documented; do not claim Vortex
  deployment is tested.
- Set the appropriate AI disclosures. Nexus currently distinguishes AI-Generated
  Content (including generated code/UI), AI Media (including generated page copy),
  and AI Assisted. This project contains agent-written code/UI and listing copy;
  use **AI-Generated Content** and **AI Media** for this prepared submission rather
  than assuming AI Assisted alone is sufficient.
- Use permissions consistent with MIT for original code/documentation/artwork.
  Preserve the copyright/license notice. Font licenses are independent and must
  be preserved. Historical undocumented power PNGs are excluded from the grant.

## Verification and release status

The packager rejects version mismatch, malformed metadata/icon, missing licensing
notices, unexpected archive files and DLL hash mismatches. It emits
`artifacts/SHA256SUMS.txt` for the two versioned archives. The optional
`Ironbound-local.zip` is an alias of the manual/Nexus archive for the existing workflow.

Packaging validation is not site moderation approval, a virus-scan result or a
substitute for in-game testing. Latest HUD positioning, temperament branches,
loaded/unloaded owner handoff and mod compatibility need the live checks recorded
in docs/VERIFICATION.md and docs/AUDIT-0.5.5.md. Public copy discloses alpha status.
GitHub's alpha prerelease is the public download/announcement. Preparing these
ZIPs does not submit them to Thunderstore/Nexus or change server state. Use
ANNOUNCEMENT.md for community posts in channels where promotion is welcome.

The numeric package/plugin version remains `0.5.6` for Thunderstore's required
three-number manifest format. Filenames, public copy, assembly informational
version and the GitHub prerelease/tag explicitly mark the build as alpha.

## Requirements checked 2026-10-22

- https://wiki.thunderstore.io/mods/creating-a-package
- https://wiki.thunderstore.io/mods/packaging-your-mods
- https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/
- https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/
- https://help.nexusmods.com/article/28-file-submission-guidelines

Local validation matches documented package structure; it does not submit the
archive or manifest to a remote validation service.
