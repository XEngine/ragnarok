#if DEBUG
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ironbound.Development;

// Debug-only: drives the main menu into a throwaway character and world so in-game UI can be
// captured without a person at the keyboard. The client ignores -savedir (dedicated servers
// only), so saves are redirected here and verified, keeping real characters and worlds untouched.
internal static class WorldPreview
{
    internal const string Name = "IronboundPreview";

    internal static bool Isolate(string directory)
    {
        string full = Path.GetFullPath(directory);
        Directory.CreateDirectory(full);
        Utils.SetSaveDataPath(full);
        SaveSystem.InvalidateCache(SaveDataType.Character);
        SaveSystem.InvalidateCache(SaveDataType.World);
        return Path.GetFullPath(Utils.GetSaveDataPath(FileHelpers.FileSource.Local)) == full;
    }

    internal static IEnumerator Enter()
    {
        yield return new WaitUntil(() => FejdStartup.instance);
        yield return new WaitForSecondsRealtime(4f);
        FejdStartup fejd = FejdStartup.instance;
        Traverse t = Traverse.Create(fejd);

        if (!PlayerProfile.HaveProfile(Name.ToLowerInvariant()))
        {
            t.Field("m_csNewCharacterName").GetValue<TMP_InputField>().text = Name;
            fejd.OnNewCharacterDone(true);
            yield return null;
        }
        t.Method("SetSelectedProfile", Name.ToLowerInvariant()).GetValue();
        fejd.OnCharacterStart();
        yield return null;

        if (!World.HaveWorld(Name))
        {
            t.Field("m_newWorldName").GetValue<TMP_InputField>().text = Name;
            t.Field("m_newWorldSeed").GetValue<TMP_InputField>().text = "Ironbound";
            fejd.OnNewWorldDone(true);
            yield return null;
        }
        t.Field("m_world").SetValue(t.Method("FindWorld", Name).GetValue<World>());
        fejd.m_openServerToggle.isOn = false;
        fejd.m_publicServerToggle.isOn = false;
        fejd.m_crossplayServerToggle.isOn = false;
        fejd.OnWorldStart();

        yield return new WaitUntil(() => Player.m_localPlayer && !Player.m_localPlayer.IsTeleporting());
        yield return new WaitForSecondsRealtime(8f);
    }

    // Typical mid-game kit so slots show stacks, durability, quality, food and equipped markers.
    internal static void Stock(Player player)
    {
        Inventory inventory = player.GetInventory();
        if (inventory.CountItems("$item_axe_bronze") > 0) return;
        string[] kit =
        {
            "AxeBronze", "SwordBronze", "ShieldBronzeBuckler", "Bow", "ArrowFlint:60", "HelmetBronze",
            "ArmorBronzeChest", "ArmorBronzeLegs", "CapeDeerHide", "Wood:50", "Stone:38", "Resin:12",
            "CookedMeat:8", "QueensJam:5", "CarrotSoup:4", "Bronze:9", "Coins:241", "Hammer", "Torch"
        };
        foreach (string entry in kit)
        {
            string[] parts = entry.Split(':');
            GameObject prefab = ObjectDB.instance.GetItemPrefab(parts[0]);
            if (!prefab) continue;
            ItemDrop.ItemData item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = parts.Length > 1 ? int.Parse(parts[1]) : 1;
            if (item.m_shared.m_maxQuality > 1) item.m_quality = 2;
            if (item.m_shared.m_useDurability) item.m_durability = item.GetMaxDurability() * 0.7f;
            inventory.AddItem(item);
        }
        foreach (ItemDrop.ItemData item in inventory.GetAllItems().ToList())
            if (item.IsEquipable() && item.m_shared.m_name is "$item_axe_bronze" or "$item_shield_bronzebuckler"
                    or "$item_helmet_bronze" or "$item_chest_bronze" or "$item_legs_bronze" or "$item_cape_deerhide")
                player.EquipItem(item);
    }

    // Writes a readable tree of a UI hierarchy: rect, sprites, colours and text settings.
    internal static void Dump(Transform root, string path)
    {
        StringBuilder text = new();
        Write(root, 0, text);
        File.WriteAllText(path, text.ToString());
    }

    private static void Write(Transform node, int depth, StringBuilder text)
    {
        text.Append(' ', depth * 2).Append(node.gameObject.activeSelf ? "" : "(off) ").Append(node.name);
        if (node is RectTransform r)
            text.Append($"  [{r.rect.width:0}x{r.rect.height:0} @{r.anchoredPosition.x:0},{r.anchoredPosition.y:0} a{r.anchorMin}-{r.anchorMax} p{r.pivot}]");
        foreach (Component c in node.GetComponents<Component>())
        {
            switch (c)
            {
                case RectTransform: break;
                case Image image:
                    text.Append($" Image({(image.sprite ? image.sprite.name : "null")},{image.type},#{ColorUtility.ToHtmlStringRGBA(image.color)}{(image.enabled ? "" : ",off")})");
                    break;
                case TMP_Text label:
                    text.Append($" Text(\"{Short(label.text)}\",{(label.font ? label.font.name : "null")},{label.fontSize:0},#{ColorUtility.ToHtmlStringRGBA(label.color)})");
                    break;
                default:
                    text.Append(' ').Append(c ? c.GetType().Name : "missing");
                    break;
            }
        }
        text.Append('\n');
        foreach (Transform child in node) Write(child, depth + 1, text);
    }

    private static string Short(string s) => (s.Length > 30 ? s.Substring(0, 30) + "…" : s).Replace("\n", " ");
}
#endif
