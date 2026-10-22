using System;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace Ragnarok.HUD;

internal sealed class VitalsRecoveryHud : IDisposable
{
    private readonly TextMeshProUGUI rate, healthRate;
    private readonly TextOutline rateOutline, healthOutline;
    private StaminaRecoveryMeter meter = new();
    private Player? observed;
    private float lastSample = -10;

    internal VitalsRecoveryHud(Theme theme, RectTransform parent, float barEnd)
    {
        rate = Ugui.Text(parent, "Stamina recovery per second", "+0.0/s", theme.Numbers, 10, Palette.Muted)
            .At(barEnd + 8, 42, 65, 18);
        rateOutline = new TextOutline(rate, opacity: 0.7f);
        healthRate = Ugui.Text(parent, "Health recovery per second", "+0.0/s", theme.Numbers, 10,
            Palette.Muted, TextAlignmentOptions.Right).At(-73, 42, 65, 18);
        healthOutline = new TextOutline(healthRate, opacity: 0.7f);
        StaminaRecoveryPatch.Updated += Observe;
    }

    private void Observe(Player player, float before, float dt)
    {
        if (player != Player.m_localPlayer) return;
        if (observed != player) { observed = player; meter = new StaminaRecoveryMeter(); }
        meter.Observe(before, player.GetStamina(), dt);
        lastSample = Time.unscaledTime;
    }

    internal void Tick(bool preview)
    {
        Player player = Player.m_localPlayer;
        if (preview) { rate.text = "+12.4/s"; healthRate.text = "+1.5/s"; }
        else if (!player || player.IsDead() || player.InIntro() || player.IsTeleporting())
        {
            observed = null; meter = new StaminaRecoveryMeter();
            rate.text = healthRate.text = "+0.0/s";
        }
        else
        {
            float currentRate = observed == player && Time.unscaledTime - lastSample < 0.5f ? meter.Rate : 0;
            rate.text = "+" + currentRate.ToString("0.0") + "/s";
            float foodRegen = 0;
            foreach (Player.Food food in player.GetFoods()) foodRegen += food.m_item.m_shared.m_foodRegen;
            float modifier = 1;
            player.GetSEMan().ModifyHealthRegen(ref modifier);
            float health = HealthRecoveryRate.PerSecond(foodRegen, modifier, player.GetHealth() < player.GetMaxHealth() - 0.01f);
            healthRate.text = "+" + health.ToString("0.0") + "/s";
        }
        rateOutline.Sync(); healthOutline.Sync();
    }

    public void Dispose()
    {
        StaminaRecoveryPatch.Updated -= Observe;
        observed = null;
        // Labels/outlines belong to the vitals root and are destroyed with it.
    }
}

// Read-only instrumentation: native updates still decide all stamina behavior.
[HarmonyPatch(typeof(Player), "UpdateStats", new[] { typeof(float) })]
internal static class StaminaRecoveryPatch
{
    internal static event Action<Player, float, float>? Updated;
    private static void Prefix(Player __instance, out float __state) => __state = __instance.GetStamina();
    private static void Postfix(Player __instance, float __0, float __state) => Updated?.Invoke(__instance, __state, __0);
}
