# Contributing to Ironbound

Ironbound is an **alpha**, and feedback, bug reports and pull requests are welcome.
You do not need to write code to help: test a feature, reproduce a bug, improve
translations, review a change or share a useful compatibility report.

## Report bugs and suggest features

[Open an issue](https://github.com/XEngine/ironbound/issues/new/choose). Include
your Valheim/Ironbound versions, other mods, the steps to reproduce the problem,
what you expected and what happened. Screenshots help with HUD problems. Attach
the relevant `BepInEx/LogOutput.log` excerpt after removing private connection
details. Mention whether the problem occurs in vanilla or Ironbound HUD style,
single-player or a dedicated server, and after a fresh launch or F6 reload.

For mount issues, tell us whether the mount was loaded nearby or far away,
who owned/rides it, its hunger/combat state and the exact on-screen message.
Suggestions should describe the gameplay problem and intended behavior. Search
existing issues first so related work stays together.

## Send a pull request

Fork the repository, create a branch and keep the change focused. Include the
problem, resulting behavior, checks performed and any remaining in-game tests in
the PR description. Draft PRs and early design feedback are welcome. For larger
gameplay or UI changes, open an issue first so we can discuss scope together.

1. Follow AGENTS.md and the setup instructions in README.md (Windows) or
   docs/LINUX.md. Compile against your own installed game and Jötunn assemblies.
2. Keep SDKs, game copies, saves, private settings and compiled dependencies out
   of Git. Use an isolated dev profile/world rather than ordinary saves.
3. Windows: `./dev.ps1 build` and `./dev.ps1 test`. Linux: `./dev.sh build` and
   `./dev.sh test`. Packaging checks: `python -B tests/test_release_package.py`.
4. Add meaningful regression coverage for changed behavior. Report which game
   version/mod combination you tested, and explicitly state if no live test ran.
5. Clean up patches, RPCs, subscriptions, input registrations and owned Unity
   objects on unload. Design server authority/config synchronization before
   gameplay changes. Do not duplicate animals, inventories or native resources.

Public CI runs the game-independent regression suite and packaging metadata tests.
It cannot compile the plugin or run Unity without privately installed game/SDK
assemblies; those checks remain local. Do not upload those assemblies to CI.

## License and assets

Original project code, documentation and procedural artwork are under MIT; see
LICENSE. Contributions are accepted under the same license. Fonts retain their
SIL Open Font Licenses. Document the source and license of new outside assets or
code; do not copy game assets into the repository. Legacy power PNGs removed from
the current tree remain in historical commits with undocumented provenance and
are excluded from the MIT grant. Native icons are now resolved at runtime.

Be constructive and specific in issues/reviews. Testing reports and small fixes
are as welcome as larger features. If Ironbound helps your playthrough, sharing
the project with other Valheim players or starring it helps people find it.
