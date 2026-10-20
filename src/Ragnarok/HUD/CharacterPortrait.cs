using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Ragnarok.HUD;

// Render-only copies: never clone a Player, ZNetView, script, collider or animator.
// Neither the helmet switch nor the portrait camera changes the live character.
internal sealed class CharacterPortrait : IDisposable
{
    private const int PortraitLayer = 30;
    private static readonly AccessTools.FieldRef<VisEquipment, GameObject> Helmet = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_helmetItemInstance");
    private static readonly AccessTools.FieldRef<VisEquipment, GameObject> Hair = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_hairItemInstance");
    private static readonly AccessTools.FieldRef<VisEquipment, GameObject> Beard = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_beardItemInstance");
    private static readonly AccessTools.FieldRef<VisEquipment, int> HairHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_hairItem");
    private static readonly AccessTools.FieldRef<VisEquipment, int> BeardHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_beardItem");
    private readonly RawImage image;
    private readonly Graphic rune;
    private readonly GameObject mask;
    private RenderTexture? texture;
    private float nextCheck;
    private int signature;
    private bool ready, failed;

    internal CharacterPortrait(Theme theme, Transform parent, Graphic rune)
    {
        this.rune = rune;
        Image disc = Ugui.Image(parent, "Portrait mask", theme.Disc, Color.white).At(4, 4, 84, 84);
        mask = disc.gameObject;
        Mask clip = mask.AddComponent<Mask>();
        clip.showMaskGraphic = false;
        image = Ugui.Node(disc.transform, "Face").gameObject.AddComponent<RawImage>().Stretch();
        image.raycastTarget = false;
        mask.SetActive(false);
    }

    internal void Tick(Player? player, bool enabled, bool showHelmet)
    {
        if (!enabled || !player || failed)
        {
            mask.SetActive(false);
            rune.enabled = true;
            return;
        }
        if (Time.unscaledTime >= nextCheck)
        {
            nextCheck = Time.unscaledTime + 1f;
            VisEquipment equipment = player!.GetComponent<VisEquipment>();
            if (equipment && equipment.m_bodyModel && equipment.m_helmet)
            {
                int current = Appearance(player, equipment, showHelmet);
                if (!ready || signature != current)
                {
                    try
                    {
                        Capture(player, equipment, showHelmet);
                        signature = current;
                        ready = true;
                    }
                    catch (Exception error)
                    {
                        // Keep the rune usable if a custom character shader/model cannot render.
                        Debug.LogWarning("[Ragnarok] Character portrait unavailable: " + error.Message);
                        failed = true;
                    }
                }
            }
        }
        mask.SetActive(ready && !failed);
        rune.enabled = !ready || failed;
    }

    private static int Appearance(Player player, VisEquipment equipment, bool helmet)
    {
        unchecked
        {
            int hash = player.GetInstanceID() * 397 ^ helmet.GetHashCode();
            hash = hash * 397 ^ player.GetHairColor().GetHashCode();
            hash = hash * 397 ^ HairHash(equipment);
            hash = hash * 397 ^ BeardHash(equipment);
            foreach (Renderer renderer in equipment.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                hash = hash * 397 ^ renderer.GetInstanceID();
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh)
                    hash = hash * 397 ^ skin.sharedMesh.GetInstanceID();
                foreach (Material material in renderer.sharedMaterials)
                    if (material)
                    {
                        hash = hash * 397 ^ material.GetInstanceID();
                        if (material.HasProperty("_SkinColor")) hash = hash * 397 ^ material.GetColor("_SkinColor").GetHashCode();
                    }
            }
            return hash;
        }
    }

    private void Capture(Player player, VisEquipment equipment, bool showHelmet)
    {
        GameObject rig = new("Ragnarok Portrait Studio") { hideFlags = HideFlags.HideAndDontSave };
        rig.SetActive(false);
        rig.transform.position = new Vector3(0, 10000, 0);
        try
        {
            var copies = new Dictionary<Transform, Transform>();
            var renderers = new List<KeyValuePair<Renderer, Renderer>>();
            GameObject? helmet = Helmet(equipment), hair = Hair(equipment), beard = Beard(equipment);
            CopyTree(player.transform, rig.transform, copies, renderers, showHelmet ? null : helmet,
                showHelmet ? null : hair, showHelmet ? null : beard);
            Transform model = copies[player.transform];
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            // Use the prefab's neutral skeleton rather than the current animation/IK pose.
            // Runtime equipment children are retained, attached to the restored bones.
            GameObject template = ZNetScene.instance.GetPrefab(Utils.GetPrefabName(player.gameObject));
            if (!template) template = ZNetScene.instance.GetPrefab("Player");
            if (!template) throw new InvalidOperationException("The character's neutral model is not available.");
            model.localScale = template.transform.localScale;
            RestorePose(template.transform, model);
            BindSkins(copies, renderers);
            if (!showHelmet)
            {
                AddHair(HairHash(equipment), player, equipment, copies);
                AddHair(BeardHash(equipment), player, equipment, copies);
            }

            // Frame the restored head, never the live character's crouching/looking position.
            Vector3 focus = copies[equipment.m_helmet].position + Vector3.down * 0.05f;
            GameObject cameraObject = new("Portrait Camera");
            cameraObject.transform.SetParent(rig.transform, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 0.39f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Hex(0x182321);
            camera.cullingMask = 1 << PortraitLayer;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.transform.position = focus + Vector3.forward * 1.2f;
            camera.transform.LookAt(focus, Vector3.up);
            AddLight(rig.transform, new Vector3(0.5f, 0.6f, 1), 1.3f, new Color(1, 0.9f, 0.76f));
            AddLight(rig.transform, new Vector3(-0.6f, 0.2f, 1), 0.65f, new Color(0.7f, 0.82f, 1));
            if (!texture)
            {
                texture = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)
                {
                    name = "Ragnarok Character Portrait", hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1, filterMode = FilterMode.Bilinear
                };
                texture.Create();
                image.texture = texture;
            }
            camera.targetTexture = texture;
            RenderTexture previous = RenderTexture.active;
            try
            {
                rig.SetActive(true);
                camera.Render();
            }
            finally
            {
                rig.SetActive(false);
                camera.targetTexture = null;
                RenderTexture.active = previous;
            }
        }
        finally
        {
            rig.SetActive(false);
            UnityEngine.Object.Destroy(rig);
        }
    }

    private static void RestorePose(Transform template, Transform copy)
    {
        for (int i = 0; i < template.childCount; i++)
        {
            Transform rest = template.GetChild(i);
            Transform bone = copy.Find(rest.name);
            if (!bone) continue;
            bone.localPosition = rest.localPosition;
            bone.localRotation = rest.localRotation;
            bone.localScale = rest.localScale;
            RestorePose(rest, bone);
        }
    }

    private static void CopyTree(Transform source, Transform parent, Dictionary<Transform, Transform> copies,
        List<KeyValuePair<Renderer, Renderer>> renderers, GameObject? helmet = null, GameObject? hair = null, GameObject? beard = null)
    {
        if (source.gameObject == helmet || source.gameObject == hair || source.gameObject == beard) return;
        GameObject node = new(source.name) { layer = PortraitLayer };
        Transform target = node.transform;
        target.SetParent(parent, false);
        target.localPosition = source.localPosition;
        target.localRotation = source.localRotation;
        target.localScale = source.localScale;
        copies[source] = target;
        foreach (Renderer original in source.GetComponents<Renderer>())
        {
            Renderer? copy = null;
            if (original is SkinnedMeshRenderer skin)
            {
                SkinnedMeshRenderer result = node.AddComponent<SkinnedMeshRenderer>();
                result.sharedMesh = skin.sharedMesh;
                result.updateWhenOffscreen = true;
                result.localBounds = skin.localBounds;
                copy = result;
            }
            else if (original is MeshRenderer && source.TryGetComponent(out MeshFilter filter))
            {
                node.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                copy = node.AddComponent<MeshRenderer>();
            }
            if (!copy) continue;
            copy!.enabled = original.enabled;
            copy.sharedMaterials = original.sharedMaterials;
            copy.shadowCastingMode = ShadowCastingMode.Off;
            copy.receiveShadows = false;
            MaterialPropertyBlock properties = new();
            original.GetPropertyBlock(properties);
            copy.SetPropertyBlock(properties);
            renderers.Add(new KeyValuePair<Renderer, Renderer>(original, copy));
        }
        for (int i = 0; i < source.childCount; i++) CopyTree(source.GetChild(i), target, copies, renderers, helmet, hair, beard);
        node.SetActive(source.gameObject.activeSelf);
    }

    private static void BindSkins(Dictionary<Transform, Transform> copies, List<KeyValuePair<Renderer, Renderer>> renderers)
    {
        foreach (var pair in renderers)
        {
            if (!(pair.Key is SkinnedMeshRenderer original) || !(pair.Value is SkinnedMeshRenderer copy)) continue;
            Transform[] bones = original.bones;
            for (int i = 0; i < bones.Length; i++)
                bones[i] = bones[i] && copies.TryGetValue(bones[i], out Transform bone) ? bone : null!;
            copy.bones = bones;
            if (original.rootBone && copies.TryGetValue(original.rootBone, out Transform root)) copy.rootBone = root;
            if (original.sharedMesh)
                for (int i = 0; i < original.sharedMesh.blendShapeCount; i++) copy.SetBlendShapeWeight(i, original.GetBlendShapeWeight(i));
        }
    }

    private static void AddHair(int hash, Player player, VisEquipment equipment, Dictionary<Transform, Transform> copies)
    {
        GameObject? prefab = hash != 0 && ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(hash) : null;
        if (!prefab) return;
        Transform attach = prefab!.transform.Find("attach") ?? prefab.transform.Find("attach_skin");
        if (!attach) return;
        bool skinned = attach.name == "attach_skin";
        var extra = new Dictionary<Transform, Transform>();
        var renderers = new List<KeyValuePair<Renderer, Renderer>>();
        Transform parent = copies[skinned ? equipment.m_bodyModel.transform.parent : equipment.m_helmet];
        CopyTree(attach, parent, extra, renderers);
        Transform root = extra[attach];
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.gameObject.SetActive(true);
        Transform offset = prefab.transform.Find("equipoffset");
        if (offset) { root.localPosition += offset.position; root.localRotation *= offset.rotation; }
        foreach (var pair in renderers)
        {
            Renderer renderer = pair.Value;
            if (renderer is SkinnedMeshRenderer skin)
            {
                Transform[] bones = equipment.m_bodyModel.bones;
                for (int i = 0; i < bones.Length; i++) bones[i] = copies[bones[i]];
                skin.bones = bones;
                skin.rootBone = copies[equipment.m_bodyModel.rootBone];
            }
            if (equipment.m_overrideHairMaterial) renderer.sharedMaterial = equipment.m_overrideHairMaterial;
            MaterialPropertyBlock properties = new();
            renderer.GetPropertyBlock(properties);
            Vector3 hue = player.GetHairColor();
            properties.SetColor(Shader.PropertyToID("_SkinColor"), new Color(hue.x, hue.y, hue.z));
            renderer.SetPropertyBlock(properties);
        }
    }

    private static void AddLight(Transform parent, Vector3 direction, float intensity, Color color)
    {
        GameObject node = new("Portrait Light");
        node.transform.SetParent(parent, false);
        node.transform.rotation = Quaternion.LookRotation(-direction.normalized);
        Light light = node.AddComponent<Light>();
        light.type = LightType.Directional;
        light.cullingMask = 1 << PortraitLayer;
        light.intensity = intensity;
        light.color = color;
        light.shadows = LightShadows.None;
    }

    public void Dispose()
    {
        image.texture = null;
        if (texture) { texture!.Release(); UnityEngine.Object.Destroy(texture); }
        texture = null;
        UnityEngine.Object.Destroy(mask);
    }
}
