using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Ragnarok.UI;

// The fonts Ragnarök ships (Assets/Fonts, SIL Open Font License): written out to BepInEx's cache once and
// turned into dynamic TextMeshPro fonts. The common characters are rendered up front, so text shows even
// if a later glyph cannot be added; the Latin Extended file (Turkish, Polish...) rides along as a fallback.
internal static class FontLibrary
{
    // Everything typed in English, German, French, Scandinavian, Turkish and the game's own symbols.
    private const string Common =
        " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~" +
        "¡¢£¤¥¦§¨©ª«¬®¯°±²³´µ¶·¸¹º»¼½¾¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö÷øùúûüýþÿ" +
        "ĞğİıŞşŁłŒœŠšŽžŸ–—‘’‚“”„†‡•…‰‹›€™→←↑↓";

    private static readonly Dictionary<string, TMP_FontAsset?> loaded = new();

    // `file` like "Scada-Regular.ttf"; `extended` the Latin Extended companion, if there is one.
    internal static TMP_FontAsset? Get(string file, string? extended = null)
    {
        if (loaded.TryGetValue(file, out TMP_FontAsset? cached)) return cached;
        TMP_FontAsset? asset = Create(file);
        if (asset && extended != null && Create(extended) is { } more)
            asset!.fallbackFontAssetTable = new List<TMP_FontAsset> { more };
        loaded[file] = asset;
        return asset;
    }

    internal static void Dispose()
    {
        // Runtime-created font assets own their atlas textures and materials as well.
        // Destroy all three on F6 reload; shared game fonts never enter this collection.
        HashSet<UnityEngine.Object> owned = new();
        void Collect(TMP_FontAsset? asset)
        {
            if (!asset || !owned.Add(asset!)) return;
            if (asset!.material) owned.Add(asset.material);
            foreach (Texture2D texture in asset.atlasTextures)
                if (texture) owned.Add(texture);
            if (asset.fallbackFontAssetTable != null)
                foreach (TMP_FontAsset fallback in asset.fallbackFontAssetTable) Collect(fallback);
        }
        foreach (TMP_FontAsset? asset in loaded.Values) Collect(asset);
        foreach (UnityEngine.Object asset in owned) UnityEngine.Object.Destroy(asset);
        loaded.Clear();
    }

    private static TMP_FontAsset? Create(string file)
    {
        try
        {
            string? path = Extract(file);
            if (path == null) return null;
            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(path, 0, 64, 7, GlyphRenderMode.SDFAA, 1024, 1024);
            if (!asset)
            {
                Debug.LogWarning($"[Ragnarok] Could not load font {file}.");
                return null;
            }
            asset.name = "Ragnarok " + Path.GetFileNameWithoutExtension(file);
            asset.TryAddCharacters(Common, out string missing);
            Debug.Log($"[Ragnarok] Font {asset.name} loaded" + (missing.Length > 0 ? $" ({missing.Length} characters not in this file)." : "."));
            return asset;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Ragnarok] Could not load font {file}: {e.Message}");
            return null;
        }
    }

    private static string? Extract(string file)
    {
        using Stream? stream = typeof(FontLibrary).Assembly.GetManifestResourceStream("Ragnarok.Fonts." + file);
        if (stream == null) return null;
        string directory = Path.Combine(BepInEx.Paths.CachePath, "Ragnarok", "Fonts");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, file);
        if (File.Exists(path) && new FileInfo(path).Length == stream.Length) return path;
        using FileStream output = File.Create(path);
        stream.CopyTo(output);
        return path;
    }
}
