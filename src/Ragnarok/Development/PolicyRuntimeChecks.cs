#if DEBUG
using System;
using System.IO;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Ragnarok.Configuration;
using Ragnarok.Features.Equipment;
using Ragnarok.Features.NearbyStorage;
using Ragnarok.Features.QualityOfLife;
using Ragnarok.Networking;
using UnityEngine;

namespace Ragnarok.Development;

// Explicit smoke-test checks only. Temporary config and synthetic inventories/GUI;
// no player, character, world, tombstone or chest is loaded or modified.
internal static class PolicyRuntimeChecks
{
    internal static void Run(ManualLogSource log)
    {
        Config(log);
        Slots();
        Crafting();
        log.LogInfo("Ragnarok policy checks PASS: mandatory policies, personal caps, reload/disconnect restoration, slot hydration/creation and craft reset/unload.");
    }

    private static void Config(ManualLogSource log)
    {
        ServerConfig? previous = ServerConfig.Instance;
        ServerConfig? sync = null;
        string path = Path.Combine(Path.GetTempPath(), "Ragnarok-policy-check-" + Guid.NewGuid().ToString("N") + ".cfg");
        try
        {
            ConfigFile file = new(path, false);
            FeatureSettings features = new(file);
            StorageSettings storage = new(file);
            ConfigEntry<int> optional = file.Bind("Test", "Optional", 3);
            ConfigEntryBase[] required =
            {
                storage.Enabled, storage.Range, features.AutoEquipOnReclaim
            };
            ConfigEntryBase[] all = new ConfigEntryBase[required.Length + 1];
            required.CopyTo(all, 0);
            all[required.Length] = optional;
            sync = new ServerConfig(file, log, "checks", all, required);
            storage.Range.Value = 10f;
            storage.PersonalRange.Value = 8f;
            file.Save();

            void Apply(float cap, bool locked = false)
            {
                ZPackage package = new();
                package.Write("checks"); package.Write(locked); package.Write(all.Length);
                foreach (ConfigEntryBase entry in all)
                {
                    package.Write(ConfigSchema.WireSection(entry.Definition.Section, entry.Definition.Key));
                    package.Write(entry.Definition.Key);
                    package.Write(entry == storage.Range ? cap.ToString(System.Globalization.CultureInfo.InvariantCulture) :
                        entry == optional ? "9" : entry.SettingType == typeof(bool) ? "false" : "25");
                }
                package.SetPos(0);
                sync.ApplyPackage(package);
            }

            Apply(20f);
            Assert(sync.RemoteVersion == "checks", "accepted server version is available for display");
            Assert(storage.Range.Value == 20f && optional.Value == 3, "mandatory policy survives unlocked optional settings");
            Assert(!features.AutoEquipOnReclaim.Value && !storage.Enabled.Value,
                "all requested permissions are server-defined");
            foreach (ConfigEntryBase entry in required) Assert(sync.Controlled(entry), "policy is locked");
            Assert(!sync.Controlled(storage.PersonalRange) && storage.EffectiveRange == 8f, "personal radius remains editable");
            file.Reload();
            Assert(storage.Range.Value == 20f, "reload retains effective policy");
            storage.Range.Value = 7f;
            Assert(storage.Range.Value == 20f, "local edit cannot override policy");
            file.Reload();
            Apply(5f);
            Assert(storage.EffectiveRange == 5f && storage.PersonalRange.Value == 8f, "lower cap preserves personal preference");
            storage.PersonalRange.Value = 2f;
            Assert(storage.EffectiveRange == 2f, "player can lower radius");
            bool rejected = false;
            try { Apply(float.NaN); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected && storage.Range.Value == 5f, "invalid policy is rejected atomically");
            Apply(5f, true);
            Assert(optional.Value == 9 && sync.Controlled(optional), "optional policy lock");
            Apply(5f);
            Assert(optional.Value == 3 && !sync.Controlled(optional), "unlock restores only optional settings");
            sync.Dispose();
            Assert(sync.RemoteVersion == null, "disconnect clears displayed server version");
            sync = null;
            Assert(storage.Range.Value == 7f && features.AutoEquipOnReclaim.Value, "disconnect restores edited personal policy");

            // Same-value reloads have no SettingChanged event.
            sync = new ServerConfig(file, log, "checks", all, required);
            Apply(20f);
            string contents = File.ReadAllText(path);
            File.WriteAllText(path, contents.Replace("Range = 7", "Range = 20"));
            file.Reload();
            sync.Dispose();
            sync = null;
            Assert(storage.Range.Value == 20f, "reload remembers personal value equal to held value");
        }
        finally
        {
            sync?.Dispose();
            ServerConfig.Instance = previous;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void Slots()
    {
        Inventory inventory = new("Ragnarok slot checks", null, 8, 4);
        var add = AccessTools.MethodDelegate<Func<Inventory, ItemDrop.ItemData, int, int, int, bool, bool>>(
            AccessTools.Method(typeof(Inventory), "AddItem", new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) }));
        Func<bool> enabled = CharacterSlots.Enabled, extras = CharacterSlots.ExtraUtilityEnabled;
        try
        {
            CharacterSlots.Enabled = CharacterSlots.ExtraUtilityEnabled = () => false;
            Assert(add(inventory, Item("helmet"), 1, 0, CharacterSlots.GearRow, true), "disabled gear hydration");
            Assert(add(inventory, Item("utility"), 1, 6, CharacterSlots.GearRow, true), "disabled extra utility hydration");
            Assert(add(inventory, Item("food"), 1, 0, CharacterSlots.ProvisionRow, true), "disabled provision hydration");
            Assert(add(inventory, Item("quiver"), 1, 8, CharacterSlots.GearRow, true), "legacy quiver hydration");
            Assert(inventory.GetItemAt(0, CharacterSlots.QuiverRow) != null, "quiver migrated before native width check");
            CharacterSlots.Empty(inventory);
            Assert(inventory.NrOfItems() == 4 && !CharacterSlots.AnyOutsideGrid(inventory), "disabled slots migrate without loss");

            inventory.GetAllItems().Clear();
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 8; x++)
                {
                    ItemDrop.ItemData item = Item("filler");
                    item.m_gridPos = new Vector2i(x, y);
                    inventory.GetAllItems().Add(item);
                }
            Assert(add(inventory, Item("overflow"), 1, 0, CharacterSlots.GearRow, true), "full bag retains saved gear");
            CharacterSlots.Empty(inventory);
            Assert(inventory.NrOfItems() == 33 && inventory.GetItemAt(0, CharacterSlots.GearRow) != null,
                "disabled full bag retains overflow");
            inventory.RemoveItem(inventory.GetItemAt(0, CharacterSlots.GearRow));
            Vector2i slot = CharacterSlots.GearSlot(0);
            Assert(SlotItemCreation.FindDestination(inventory, false, slot) == slot, "upgrade destination survives full bag");
            ItemDrop.ItemData replacement = Item("upgraded");
            replacement.m_quality = 2;
            Assert(SlotItemCreation.AddCreated(inventory, replacement, slot) &&
                inventory.GetItemAt(slot.x, slot.y) == replacement && inventory.NrOfItems() == 33,
                "upgrade replacement retains its actual item and reserved position");
            Assert(!SlotItemCreation.HasDestination(inventory, slot), "occupied slot is not a free destination");
        }
        finally
        {
            CharacterSlots.Enabled = enabled;
            CharacterSlots.ExtraUtilityEnabled = extras;
        }
    }

    private static ItemDrop.ItemData Item(string name) => new()
    {
        m_shared = new ItemDrop.ItemData.SharedData { m_name = name, m_maxStackSize = 1, m_weight = 1 },
        m_stack = 1
    };

    private static void Crafting()
    {
        GameObject fixture = new("Ragnarok inactive craft checks");
        fixture.SetActive(false);
        try
        {
            InventoryGui gui = fixture.AddComponent<InventoryGui>();
            var timer = AccessTools.FieldRefAccess<InventoryGui, float>("m_craftTimer");
            var touch = AccessTools.FieldRefAccess<InventoryGui, bool>("m_touchMultiCrafting");
            gui.m_multiCraftAmount = 5; gui.m_multiCraftDuration = 6; gui.m_craftDuration = 2; timer(gui) = -1;
            CraftAmount.Amount = 9;
            CraftAmount.Apply(gui);
            timer(gui) = 0;
            CraftAmount.Reset();
            Assert(gui.m_multiCraftAmount == 9, "active craft quantity stays stable");
            timer(gui) = -1;
            CraftAmount.Apply(gui);
            Assert(gui.m_multiCraftAmount == 5 && gui.m_multiCraftDuration == 6 && !touch(gui),
                "deferred reset restores native defaults");
            CraftAmount.Amount = 7;
            CraftAmount.Apply(gui);
            timer(gui) = 0;
            CraftAmount.Unload();
            Assert(timer(gui) == -1 && gui.m_multiCraftAmount == 5 && !touch(gui), "unload cancels overridden batch and restores defaults");
        }
        finally
        {
            CraftAmount.Unload();
            UnityEngine.Object.Destroy(fixture);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Ragnarok policy check failed: " + message);
    }
}
#endif
