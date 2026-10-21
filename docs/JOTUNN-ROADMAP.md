# Jotunn adoption phases

## Phase 1: localization

Implemented the Jotunn localization lifecycle and embedded English/Turkish catalogs.
The first converted surfaces are settings navigation/page summaries, common controls,
overview captions and chest-search messages. The game language chooses the catalog;
unsupported languages and untranslated individual setting labels/descriptions retain
English. Other HUD/window text can be converted incrementally with the same API.

Catalogs live in `src/Ragnarok/Assets/Translations`. Keys have the `ragnarok_ui_`
prefix; retain identical format placeholders in each language. `UiLocalization.Text`
and `Format` always take readable English fallbacks. Language changes rebuild the
settings UI and close stale chest-search UI. F6 unload removes owned Jotunn/native
translation entries, restores pre-existing values and preserves foreign replacements.

## Phase 2: controller support

Implemented optional Jotunn controller bindings for settings (`HUD.Window.GamepadButton`)
and the weapon dial (`QualityOfLife.WeaponDial.GamepadButton`). Both default to None;
keyboard/mouse choices remain unchanged. The dial tracks which device opened it,
uses the existing right-stick aiming and selects when that device's button is released.
Large enum controls use readable previous/next choices. Controller action and layout
presentation entries are replaced on rebind and removed on unload. Bindings remain
personal and are not synchronized to the server.

## Phase 3: multiplayer versions

Release plugin metadata uses Jotunn `VersionCheckOnly` with `Minor` strictness:
when both peers have Ragnarok, major/minor versions must match; patch differences
are allowed. This preserves use on servers without Ragnarok. Shared settings still
come exclusively from the connected server through the existing RPC protocol.

Jotunn's dependent-mod registry is based on BepInEx's startup plugin list. ScriptEngine
development instances do not receive that normal release handshake guarantee. Debug
builds omit the compatibility attribute: ScriptEngine's eager Cecil reader cannot
resolve its enum arguments from the nested Jotunn plugin directory. The required
Jotunn dependency and runtime check remain enabled in both configurations. The
existing authenticated settings packet already includes the server version: exact
differences remain logged and now appear in the F10 overview's Build card. This
display does not claim that every development DLL with the same version is identical.

## Phase 4: map integration

Implemented `HUD.Minimap.ChestSearchMarker` (off by default). After selecting a chest
search result, an optional Jotunn map overlay marks that selected chest for the same
20-second guide. It respects exploration fog and the existing per-frame permission,
range, life-state and chest-open checks. No new chest scan, persistent pin, RPC or
resource transaction is introduced. It disappears with guide expiry or access loss.

The layer uploads pixels only when the selected map position changes. Unload removes
its registration, texture and toggle/listener and redraws the shared composite to
remove stale pixels. Jotunn retains ownership of its shared map renderer. Limited
private hooks accommodate initializing after Minimap.Start on F6 and the incomplete
public RemoveMapOverlay cleanup API in Jotunn 2.30.2.

## Phase 5: original content

Deferred by the user: Ragnarok currently has no original items, building pieces,
status effects or skills to register. Use the relevant Jotunn managers when a future
content request defines actual behavior. Do not add placeholder content or new save
data solely to exercise registration APIs.

## Validation

Automated tests link the production localization/input/map adapters with the installed
Harmony library and Unity/Jotunn doubles. They cover language parity/placeholders,
English fallback, registration/rebind/unload, shared blocks, mouse bindings, controller
presentations and map marker cleanup. Actual UI, controller hardware, map rendering
and real-peer handshake checks still require in-game validation; see VERIFICATION.md.
