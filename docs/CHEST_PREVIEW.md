# Crosshair chest preview

Aim at an accessible chest within Valheim's normal interaction range to see its contents without opening it. The compact Ragnarök panel shows occupied slots as item icons with stack counts, plus the chest name and occupied/total slot count. Empty slots are omitted; an empty chest shows EMPTY. Tombstones are excluded.

F10 → HUD → Chest Preview:

- Enabled: on by default; works with either HUD style. Follows HUD.Style automatically: vanilla uses Valheim's panel, inventory slot artwork and text styles; Ragnarok uses the custom theme. Switching HUD style rebuilds the preview without restarting.
- OffsetX / OffsetY: panel top-left relative to screen centre. Positive X moves right; positive Y moves down. Both default to 80 HUD pixels.
- Scale: 0.5–2, default 1 (36-pixel item cells).
- Columns: 1–12, default 6.

Position is clamped to the HUD bounds, and oversized grids shrink to fit. These are personal presentation settings, not server-locked gameplay rules.

The preview respects container privacy and ward access. It does not open containers, claim ownership, request transfers, or modify their inventories. It reads the locally replicated inventory, so remote changes appear after Valheim's normal container synchronization. The panel hides when looking away, dead, opening inventory/settings/main menu, or hiding the HUD. It does not intercept clicks.

Build and startup/reload checks validate loading. Manual checks: empty/full chests, ward/private restrictions, remote inventory updates, both HUD styles, resolution changes, custom position/scale/columns, and scene transitions. Visual layout and multiplayer behavior require gameplay verification.
