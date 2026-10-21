using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ragnarok.Features.Mounts;

// Local presentation after the server confirms movement. Never move, replace or
// resize the networked animal/collider: animate its visual child only.
internal sealed class MountArrivalEffects : IDisposable
{
    private sealed class Reveal
    {
        internal ZDOID Animal;
        internal int Tier;
        internal float Deadline, Born;
        internal Transform? Visual;
        internal Tameable? Tame;
        internal Vector3 Scale;
        internal bool Started, Visible;
        internal readonly List<(Renderer Renderer, bool ForcedOff)> Renderers = new();
    }

    private readonly List<Reveal> pending = new();
    private readonly List<GameObject> effects = new();
    private ZNetScene? scene;

    internal void Arrived(ZDOID animal)
    {
        if (scene != ZNetScene.instance) { Dispose(); scene = ZNetScene.instance; }
        if (!scene || !Player.m_localPlayer) return;
        if (animal == ZDOID.None || pending.Exists(p => p.Animal == animal) || pending.Count >= 4) return;
        float ride = Player.m_localPlayer.GetSkills().GetSkillLevel(Skills.SkillType.Ride);
        pending.Add(new Reveal { Animal = animal, Tier = MountProgression.Tier(ride), Deadline = Time.unscaledTime + 8 });
    }

    internal void Tick()
    {
        ZNetScene current = ZNetScene.instance;
        if (scene != current)
        {
            Dispose(); scene = current;
            return;
        }
        if (!current || !Player.m_localPlayer) { Dispose(); return; }
        effects.RemoveAll(go => !go);
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Reveal reveal = pending[i];
            if (!reveal.Started)
            {
                GameObject animal = current.FindInstance(reveal.Animal);
                if (!animal || Vector3.Distance(animal.transform.position, Player.m_localPlayer.transform.position) > 18)
                {
                    if (Time.unscaledTime >= reveal.Deadline) pending.RemoveAt(i);
                    continue;
                }
                var character = animal.GetComponent<Character>();
                var tame = animal.GetComponent<Tameable>();
                GameObject visual = character ? character.GetVisual() : null!;
                if (!character || character.IsDead() || !tame || tame.HaveRider() || !visual || visual.transform == animal.transform)
                { pending.RemoveAt(i); continue; }
                reveal.Visual = visual.transform; reveal.Scale = visual.transform.localScale;
                reveal.Tame = tame; reveal.Born = Time.unscaledTime; reveal.Started = true;
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    reveal.Renderers.Add((renderer, renderer.forceRenderingOff));
                    renderer.forceRenderingOff = true;
                }
                try { Burst(character.transform.position, Mathf.Clamp(character.GetRadius(), 0.8f, 2.5f), reveal.Tier); }
                catch (Exception e)
                {
                    Restore(reveal); pending.RemoveAt(i);
                    Debug.LogWarning("Ragnarok mount arrival effect skipped: " + e.Message);
                    continue;
                }
            }
            float age = Time.unscaledTime - reveal.Born;
            if (!reveal.Visual || !reveal.Tame || reveal.Tame!.HaveRider() || age >= 0.7f)
            { Restore(reveal); pending.RemoveAt(i); continue; }
            if (age >= 0.12f && !reveal.Visible) RevealRenderers(reveal);
            float t = Mathf.Clamp01((age - 0.12f) / 0.55f);
            float ease = 1 - Mathf.Pow(1 - t, 3);
            reveal.Visual!.localScale = reveal.Scale * Mathf.Lerp(0.65f, 1, ease);
        }
    }

    private void Burst(Vector3 point, float scale, int tier)
    {
        Player player = Player.m_localPlayer;
        // Use the installed game's magical spawn effects, with skill-up effects
        // as a fallback. These assets stay in the game; none are redistributed.
        if (!Spawn(player.m_spawnEffects, point, scale)) Spawn(player.m_skillLevelupEffects, point, scale);
        // Each Riding milestone adds a small satellite sparkle. Use owned
        // clones of native effects; extra layers never multiply their audio.
        for (int i = 0; i < tier; i++)
        {
            float angle = (i + 0.25f) * Mathf.PI * 2 / tier;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0.15f, Mathf.Sin(angle)) * scale * 0.55f;
            Spawn(player.m_skillLevelupEffects, point + offset, scale * 0.4f, silent: true, limit: 1);
        }
    }

    private bool Spawn(EffectList list, Vector3 point, float scale, bool silent = false, int limit = 6)
    {
        bool spawned = false;
        if (list?.m_effectPrefabs == null) return false;
        int count = 0;
        foreach (EffectList.EffectData effect in list.m_effectPrefabs)
        {
            GameObject prefab = effect.m_prefab;
            if (!effect.m_enabled || !prefab || count >= limit ||
                (silent && prefab.GetComponentsInChildren<ParticleSystem>(true).Length == 0) ||
                prefab.GetComponentsInChildren<ZNetView>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Collider>(true).Length > 0 ||
                prefab.GetComponentsInChildren<Character>(true).Length > 0) continue;
            GameObject instance = UnityEngine.Object.Instantiate(prefab, point, Quaternion.identity);
            instance.name = "Ragnarok mount arrival effect";
            instance.transform.localScale *= scale;
            if (silent)
                foreach (AudioSource audio in instance.GetComponentsInChildren<AudioSource>(true)) { audio.Stop(); audio.enabled = false; }
            effects.Add(instance); UnityEngine.Object.Destroy(instance, 5);
            count++; spawned = true;
        }
        return spawned;
    }

    private static void RevealRenderers(Reveal reveal)
    {
        foreach (var item in reveal.Renderers) if (item.Renderer) item.Renderer.forceRenderingOff = item.ForcedOff;
        reveal.Visible = true;
    }

    private static void Restore(Reveal reveal)
    {
        if (!reveal.Visible) RevealRenderers(reveal);
        if (reveal.Visual) reveal.Visual!.localScale = reveal.Scale;
    }

    public void Dispose()
    {
        foreach (Reveal reveal in pending) Restore(reveal);
        pending.Clear();
        foreach (GameObject go in effects) if (go) UnityEngine.Object.Destroy(go);
        effects.Clear(); scene = null;
    }
}
