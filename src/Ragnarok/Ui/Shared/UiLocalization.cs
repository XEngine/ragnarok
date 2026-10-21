using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Ragnarok.UI;

// Local UI text only: configuration identifiers and network values stay unchanged.
internal sealed class UiLocalization : IDisposable
{
    private const string Prefix = "ragnarok_ui_";
    private static UiLocalization? current;
    private readonly CustomLocalization translations;
    private readonly Dictionary<string, CustomLocalization> registry;
    private readonly Dictionary<string, string>? originalTable;
    private readonly Dictionary<string, string> originalValues;
    private readonly string guid;
    private bool disposed;

    internal static string Language => PlayerPrefs.GetString("language", "English");
    internal static string Token(string id) => Prefix + id.Replace('.', '_').ToLowerInvariant();

    internal UiLocalization(BepInPlugin metadata)
    {
        guid = metadata.GUID;
        registry = AccessTools.Field(typeof(LocalizationManager), "Localizations")?.GetValue(LocalizationManager.Instance)
            as Dictionary<string, CustomLocalization> ?? throw new NotSupportedException("Jotunn localization cleanup registry changed.");
        if (registry.ContainsKey(guid)) throw new InvalidOperationException("Ragnarok localization is already registered.");
        originalTable = NativeTable() ?? throw new NotSupportedException("Valheim localization cleanup table changed.");
        originalValues = originalTable?.Where(pair => pair.Key.StartsWith(Prefix, StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, string>();
        translations = new CustomLocalization(metadata);
        try
        {
            foreach (string language in new[] { "English", "Turkish" })
            {
                using Stream stream = typeof(UiLocalization).Assembly.GetManifestResourceStream("Ragnarok.Translations." + language + ".json")
                    ?? throw new FileNotFoundException("Embedded translations missing: " + language);
                using var reader = new StreamReader(stream);
                translations.AddJsonFile(language, reader.ReadToEnd());
            }
            if (translations.GetTranslations("English").Count == 0 || !LocalizationManager.Instance.AddLocalization(translations))
                throw new InvalidOperationException("Ragnarok translations could not be registered.");
            current = this;
        }
        catch { Dispose(); throw; }
    }

    internal static string Text(string id, string fallback)
    {
        if (current == null) return fallback;
        string token = Token(id);
        // Unknown settings supplied by future modules keep their readable English fallback.
        if (!current.translations.Contains("English", token)) return fallback;
        return current.translations.TryTranslate("$" + token);
    }

    internal static string Format(string id, string fallback, params object[] values)
    {
        try { return string.Format(CultureInfo.CurrentCulture, Text(id, fallback), values); }
        catch (FormatException) { return string.Format(CultureInfo.CurrentCulture, fallback, values); }
    }

    private static Dictionary<string, string>? NativeTable() => Localization.instance != null
        ? AccessTools.Field(typeof(Localization), "m_translations")?.GetValue(Localization.instance) as Dictionary<string, string> : null;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (registry.TryGetValue(guid, out CustomLocalization found) && ReferenceEquals(found, translations)) registry.Remove(guid);
        Restore(originalTable);
        var table = NativeTable();
        if (!ReferenceEquals(table, originalTable)) Restore(table);
        translations.ClearAll();
        if (ReferenceEquals(current, this)) current = null;
    }

    private void Restore(Dictionary<string, string>? table)
    {
        if (table == null) return;
        foreach (string token in translations.GetTranslations("English").Keys)
        {
            if (!table.TryGetValue(token, out string value)) continue;
            // Another mod may have replaced a token after us; keep that replacement.
            if (!translations.GetLanguages().Any(language => translations.GetTranslations(language).TryGetValue(token, out string owned) && value == owned)) continue;
            if (ReferenceEquals(table, originalTable) && originalValues.TryGetValue(token, out string previous)) table[token] = previous;
            else table.Remove(token);
        }
    }
}
