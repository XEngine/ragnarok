using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Presentation only: the roster respects map visibility and reads replicated health only
// from loaded characters. Distant players have a distance, never an invented health value.
internal sealed class CharacterHud : IDisposable
{
    private readonly InterfaceSettings settings;
    private readonly RectTransform root, roster;
    internal readonly RectTransform FoodRoot;
    private readonly TextMeshProUGUI name, partyTitle, armor, weight;
    private readonly Image weightIcon;
    private readonly TextMeshProUGUI[] names = new TextMeshProUGUI[3], distances = new TextMeshProUGUI[3];
    private readonly Image[] health = new Image[3];
    private readonly GameObject[] rows = new GameObject[3];
    private float refresh;
    private readonly CharacterPortrait portrait;

    internal CharacterHud(InterfaceSettings settings, Theme theme, Transform parent)
    {
        this.settings = settings;
        root = Ugui.Node(parent, "Ragnarok Character");
        root.sizeDelta = new Vector2(310, 436);
        Ugui.Image(root, "Portrait", theme.Disc, Palette.Hex(0x182321)).At(4, 4, 84, 84);
        Ugui.Image(root, "Portrait Rim", theme.HudPortraitRing, Color.white).At(0, 0, 92, 92);
        Image rune = Ugui.Image(root, "Rune", theme.HudRune, Palette.Gold).At(27, 28, 38, 38);
        portrait = new CharacterPortrait(theme, root, rune);
        name = Ugui.Text(root, "Name", "", theme.Heading, 26, Palette.Bone).At(110, -5, 205, 43);
        name.richText = false;
        name.overflowMode = TextOverflowModes.Overflow;
        name.enableAutoSizing = true;
        name.fontSizeMin = 16;
        name.fontSizeMax = 26;
        // Compact stats under the rune: brass glyphs and parchment figures, without a frame.
        Ugui.Image(root, "Armor icon", theme.GlyphShield, Palette.Gold).At(12, 99, 14, 14);
        armor = Ugui.Text(root, "Armor", "", theme.Strong, 14, Palette.Bone).At(34, 95, 64, 22);
        weightIcon = Ugui.Image(root, "Carry weight icon", theme.GlyphWeight, Palette.Gold).At(12, 121, 14, 14);
        weight = Ugui.Text(root, "Carry weight", "", theme.Strong, 13, Palette.Bone).At(34, 117, 112, 22);
        weight.overflowMode = TextOverflowModes.Overflow;
        FoodRoot = Ugui.Node(root, "Food").At(0, 156, 310, 110);
        roster = Ugui.Node(root, "Visible players").At(0, 240, 270, 145);
        partyTitle = Ugui.Text(roster, "Heading", "PARTY", theme.Strong, 11, Palette.Bone, spacing: 3).At(0, 0, 270, 18);
        for (int i = 0; i < rows.Length; i++)
        {
            RectTransform row = Ugui.Node(roster, "Player").At(0, 26 + i * 52, 270, 44);
            rows[i] = row.gameObject;
            Ugui.Image(row, "Badge", theme.HudTile, Palette.Hex(0x182321)).At(0, 0, 42, 42);
            Ugui.Image(row, "Badge Rim", theme.HudTileEdge, Palette.Moss).At(0, 0, 42, 42);
            Ugui.Image(row, "Rune", theme.HudRune, Palette.Bone).At(11, 10, 20, 22);
            names[i] = Ugui.Text(row, "Name", "", theme.Strong, 14, Palette.Bone).At(54, 4, 130, 24);
            names[i].richText = false;
            names[i].overflowMode = TextOverflowModes.Truncate;
            distances[i] = Ugui.Text(row, "Distance", "", theme.Strong, 12, Palette.Muted, TextAlignmentOptions.MidlineRight).At(185, 4, 55, 24);
            health[i] = Ugui.Image(row, "Health", theme.White, Palette.Health).At(54, 30, 186, 4);
            health[i].type = Image.Type.Filled;
            health[i].fillMethod = Image.FillMethod.Horizontal;
        }
    }

    internal void SetVisible(bool visible) => root.gameObject.SetActive(visible);

    internal void Tick(bool preview, int statusCount)
    {
        settings.Character.Apply(root);
        root.localScale = Vector3.one * settings.HudScale.Value;
        portrait.Tick(preview ? null : Player.m_localPlayer, settings.CharacterPortrait.Value, settings.PortraitHelmet.Value);
        // Leave room for additional buff rows when using the reference placement.
        float foodY = Mathf.Max(156, 40 + StatusStrip.HeightFor(statusCount, settings.StatusLabels.Value));
        FoodRoot.anchoredPosition = new Vector2(0, -foodY);
        roster.anchoredPosition = new Vector2(0, -foodY - 104 - (settings.ProvisionBar.Value ? 44 : 0));
        if (Time.unscaledTime < refresh) return;
        refresh = Time.unscaledTime + 0.5f;
        Player player = Player.m_localPlayer;
        armor.text = (preview ? 93 : player ? Mathf.RoundToInt(player.GetBodyArmor()) : 0).ToString();
        float carried = preview ? 67 : player ? player.GetInventory().GetTotalWeight() : 0;
        float capacity = preview ? 450 : player ? player.GetMaxCarryWeight() : 0;
        bool overburdened = capacity > 0 && carried > capacity;
        weight.text = Mathf.CeilToInt(carried) + " <size=85%>/ " + Mathf.CeilToInt(capacity) + "</size>";
        weight.color = overburdened ? Palette.Alert : Palette.Bone;
        weightIcon.color = overburdened ? Palette.Alert : Palette.Gold;
        string playerName = preview ? "Ragnhild" : player ? player.GetPlayerName() : "";
        if (!preview && (string.IsNullOrWhiteSpace(playerName) || playerName == "...") && Game.instance)
            playerName = Game.instance.GetPlayerProfile().GetName();
        name.text = playerName.ToUpperInvariant();
        int count = 0;
        if (!preview && settings.Party.Value && player && ZNet.instance)
        {
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
            {
                if (!info.m_publicPosition || info.m_characterID.IsNone() || info.m_characterID == player.GetZDOID()) continue;
                if (count == rows.Length) break;
                names[count].text = info.m_name;
                float distance = Vector3.Distance(player.transform.position, info.m_position);
                distances[count].text = distance >= 1000 ? (distance / 1000).ToString("0.0") + "km" : Mathf.RoundToInt(distance) + "m";
                health[count].enabled = false;
                foreach (Player other in Player.GetAllPlayers())
                    if (other && other.GetZDOID() == info.m_characterID)
                    {
                        health[count].enabled = true;
                        health[count].fillAmount = Mathf.Clamp01(other.GetHealth() / Mathf.Max(1, other.GetMaxHealth()));
                        break;
                    }
                count++;
            }
        }
        partyTitle.text = "PARTY  \u00b7  " + count;
        roster.gameObject.SetActive(count > 0);
        for (int i = 0; i < rows.Length; i++) rows[i].SetActive(i < count);
    }

    public void Dispose() { portrait.Dispose(); if (root) UnityEngine.Object.Destroy(root.gameObject); }
}
