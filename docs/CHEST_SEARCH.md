# Nearby chest search

Press Ctrl+F while playing to open item search. Both Control keys work with the default shortcut. Type part of a localized item name, select with Up/Down and Enter or click a result. Escape or the shortcut closes search.

Results show item icon, total quantity in that chest, chest name and distance. The nearest eight matching item/chest pairs are shown; refine the query if more exist. Search reads normally replicated inventories every half second while open. It does not open chests, transfer ownership, or modify inventory.

Selecting a result closes search and reveals a curved golden guide from the character toward that chest. Glowing dashes continuously flow toward the chest, with a travelling shimmer and a fade-out. It lasts twenty real-time seconds or until that chest is opened. It also clears on death, lost access/range, disabled search, destroyed chest, logout or unload. The guide is a screen-space projection of the world-space path, visible through intervening geometry; portions behind the camera are hidden. It is a locator, not a walkable route.

Only accessible player-built chests are searched. Personal chest privacy and ward rules apply; tombstones, natural loot, carts and ship/root inventories are excluded. Server-controlled defaults under QualityOfLife.ChestSearch: Enabled=true and Range=40 metres (5–100 allowed). Shortcut is a personal setting. The settings page is F10 → HUD → Chest Search.

The search panel follows HUD.Style, using native panel/button artwork and fonts for vanilla, and the Ironbound theme for the custom HUD. While open, gameplay buttons are blocked and the cursor is released; the world continues running.

Validation: build/startup/reload checks. Manual gameplay checks remain: shortcut/input suppression, selecting with keyboard or mouse, both skins, private/warded chests, remote inventory updates, guide tracking, timeout, opening the selected chest and scene changes.
