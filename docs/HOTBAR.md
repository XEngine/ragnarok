# Ragnarök action bar

With HUD.Style = Ragnarok, the hotbar becomes a bottom-centred eight-slot action panel: textured iron frame, brass rails, carved knot, recessed item wells, key numbers, stack counts, item quality, gold durability strips, crimson equipped marks and a light controller-selection border. Empty slots remain visible.

F10 → HUD → Hotbar:

- Enabled: true by default. Off uses the original hotbar even with Ragnarök HUD.
- Scale: 0.5–1.5, default 1.
- OffsetX: horizontal offset from bottom centre, default 0.
- BottomMargin: distance above the bottom edge, default 24 HUD pixels.

These are local display settings. Vanilla HUD always uses Valheim's original hotbar. Switching style or unloading restores the original bar's rendering and input properties.

Keyboard and controller input stays in Valheim's HotkeyBar/Player code. Touch clicks forward to the original hotbar click handler. The custom panel does not introduce skills, cooldowns, extra equipment slots, or different item-use rules. Existing provision slots remain in their own HUD display.

Build/startup/reload checks validate loading. Manual checks remain for visual placement at different resolutions, controller selection, empty and full bars, equipped/queued states, durability, and switching between HUD styles in a world.
