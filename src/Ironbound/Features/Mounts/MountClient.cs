using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.Features.Mounts;

internal sealed class MountClient : IDisposable
{
    private readonly MountService service;
    private readonly JotunnIntegration.Binding bind, whistle;
    private readonly List<GameObject> sounds = new();
    private readonly MountArrivalEffects arrival = new();
    private AudioClip? clip;
    private ZDOID holding;
    private float heldSince;
    private bool consumed;

    internal MountClient(MountService service, JotunnIntegration jotunn)
    {
        this.service = service;
        bind = jotunn.AddButton("MountBind", service.Settings.BindKey);
        whistle = jotunn.AddButton("MountWhistle", service.Settings.WhistleKey);
        service.Whistled += PlayWhistle;
        service.ArrivalConfirmed += arrival.Arrived;
    }

    internal void Tick()
    {
        Player player = Player.m_localPlayer;
        if (!player || player.IsDead() || !service.Settings.Enabled.Value || !Free()) { ResetHold(); return; }
        KeyboardShortcut key = service.Settings.BindKey.Value;
        if (bind.Held && Modifiers(key))
        {
            GameObject hover = player.GetHoverObject();
            var tame = hover ? hover.GetComponentInParent<Tameable>() : null;
            var view = tame ? tame.GetComponent<ZNetView>() : null;
            if (!tame || !view || !view.IsValid() || !tame.IsTamed() || !MountTransport.HasSaddle(tame) ||
                Vector3.Distance(player.transform.position, tame.transform.position) > 6) { ResetHold(); return; }
            ZDOID id = view.GetZDO().m_uid;
            if (holding != id) { holding = id; heldSince = Time.unscaledTime; consumed = false; }
            if (!consumed && Time.unscaledTime - heldSince >= 1)
            { consumed = true; service.Send(1, id); }
            return;
        }
        // Releasing only the modifier cannot turn a completed bind into a whistle.
        bool wasHolding = holding != ZDOID.None;
        ResetHold();
        bool bindModifier = key.Modifiers.Any(Held);
        if (!wasHolding && !bindModifier && whistle.Down && Modifiers(service.Settings.WhistleKey.Value))
        {
            if (!MountSafety.PlayerReady(player, service.Settings.CombatQuiet.Value, out string reason)) service.Message(reason);
            else service.Send(3, ZDOID.None);
        }
    }

    private void ResetHold() { holding = ZDOID.None; consumed = false; heldSince = 0; }
    internal void LateUpdate() => arrival.Tick();
    private static bool Modifiers(KeyboardShortcut key) => key.Modifiers.All(Held);
    private static bool Held(KeyCode key) => Input.GetKey(key) || Input.GetKey(key switch
    {
        KeyCode.LeftShift => KeyCode.RightShift, KeyCode.RightShift => KeyCode.LeftShift,
        KeyCode.LeftControl => KeyCode.RightControl, KeyCode.RightControl => KeyCode.LeftControl,
        KeyCode.LeftAlt => KeyCode.RightAlt, KeyCode.RightAlt => KeyCode.LeftAlt, _ => key
    });
    private static bool Free() => !UiState.ModalOpen && !UiState.Typing && !WeaponDial.Open &&
        !InventoryGui.IsVisible() && !Menu.IsVisible() && !Console.IsVisible() && !Minimap.IsOpen() &&
        !StoreGui.IsVisible() && !TextInput.IsVisible() && !Game.IsPaused() &&
        (!Chat.instance || !Chat.instance.HasFocus()) &&
        !(Hud.instance && Hud.instance.m_radialMenu && Hud.instance.m_radialMenu.gameObject.activeSelf);

    private void PlayWhistle(ZDOID playerId)
    {
        GameObject caller = ZNetScene.instance.FindInstance(playerId);
        if (!caller) return;
        if (Player.m_localPlayer && Player.m_localPlayer.GetZDOID() == playerId)
            Player.m_localPlayer.StartEmote("wave", true);
        sounds.RemoveAll(go => !go);
        if (!clip) clip = CreateWhistle();
        var go = new GameObject("Ironbound mount whistle");
        go.transform.position = caller.transform.position + Vector3.up * 1.5f;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip; source.volume = 0.45f; source.spatialBlend = 1; source.minDistance = 3; source.maxDistance = 40;
        source.rolloffMode = AudioRolloffMode.Linear; source.Play();
        sounds.Add(go); UnityEngine.Object.Destroy(go, 2);
    }

    // Original synthesized two-note whistle; no third-party audio asset.
    private static AudioClip CreateWhistle()
    {
        const int rate = 22050;
        var samples = new float[(int)(rate * 0.85f)];
        double phase = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)rate;
            float note = t < 0.36f ? t : t - 0.43f;
            float envelope = Mathf.Clamp01(note / 0.035f) * Mathf.Clamp01((0.35f - note) / 0.08f);
            if (t >= 0.36f && t < 0.43f) envelope = 0;
            phase += 2 * Math.PI * (t < 0.4f ? 1750 + 250 * Mathf.Sin(note * 5) : 2050 - 300 * note) / rate;
            samples[i] = (float)Math.Sin(phase) * envelope * 0.32f;
        }
        var audio = AudioClip.Create("Ironbound original mount whistle", samples.Length, 1, rate, false);
        // Select the array overload explicitly: the newer game also exposes a
        // ReadOnlySpan overload, which cannot be imported by our net472 target.
        AccessTools.Method(typeof(AudioClip), "SetData", new[] { typeof(float[]), typeof(int) })
            .Invoke(audio, new object[] { samples, 0 });
        return audio;
    }

    public void Dispose()
    {
        service.Whistled -= PlayWhistle; service.ArrivalConfirmed -= arrival.Arrived;
        arrival.Dispose(); bind.Dispose(); whistle.Dispose(); ResetHold();
        foreach (GameObject go in sounds) if (go) UnityEngine.Object.Destroy(go);
        sounds.Clear(); if (clip) UnityEngine.Object.Destroy(clip); clip = null;
    }
}
