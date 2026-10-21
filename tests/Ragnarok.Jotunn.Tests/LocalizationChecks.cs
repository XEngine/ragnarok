using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using BepInEx;
using Jotunn.Managers;
using Ragnarok.UI;
using UnityEngine;

internal static class LocalizationChecks
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static Dictionary<string, string> Read(string language)
    {
        using var stream = typeof(UiLocalization).Assembly.GetManifestResourceStream("Ragnarok.Translations." + language + ".json")!;
        using var reader = new StreamReader(stream);
        return new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(reader.ReadToEnd());
    }
    internal static void Run()
    {
        var english = Read("English"); var turkish = Read("Turkish");
        Check(english.Count == turkish.Count && english.Keys.All(turkish.ContainsKey), "catalog key parity");
        foreach (var pair in english)
        {
            Check(pair.Key.StartsWith("ragnarok_ui_") && Regex.IsMatch(pair.Key, "^[a-z0-9_]+$"), "namespaced token: " + pair.Key);
            Check(!string.IsNullOrWhiteSpace(pair.Value) && !string.IsNullOrWhiteSpace(turkish[pair.Key]), "nonempty translation: " + pair.Key);
            string[] Slots(string text) => Regex.Matches(text, @"\{\d+(?:[^}]*)\}").Cast<Match>().Select(match => match.Value).OrderBy(value => value).ToArray();
            Check(Slots(pair.Value).SequenceEqual(Slots(turkish[pair.Key])), "format placeholders: " + pair.Key);
        }
        var metadata = new BepInPlugin("com.kernelpanic.ragnarok");
        string token = UiLocalization.Token("config.close");
        var table = Localization.instance.m_translations;
        table[token] = "existing translation";
        for (int round = 0; round < 3; round++)
        {
            PlayerPrefs.Language = "English";
            using (var localization = new UiLocalization(metadata))
            {
                Check(LocalizationManager.Instance.Localizations.Count == 1, "one localization registration");
                Check(UiLocalization.Text("config.close", "missing") == english[token], "English lookup");
                PlayerPrefs.Language = "Turkish";
                Check(UiLocalization.Text("config.close", "missing") == turkish[token], "Turkish lookup");
                Check(UiLocalization.Format("config.range", "Range {0:0} m", 50) == "Menzil 50 m", "formatted Turkish text");
                PlayerPrefs.Language = "French";
                Check(UiLocalization.Text("config.close", "missing") == english[token], "unsupported language uses English");
                Check(UiLocalization.Text("future.option", "Readable fallback") == "Readable fallback", "unknown option preserves fallback");
                table[UiLocalization.Token("config.sections")] = "another mod's replacement";
            }
            Check(LocalizationManager.Instance.Localizations.Count == 0, "registry removed on unload");
            Check(table[token] == "existing translation", "previous translation restored");
            Check(table[UiLocalization.Token("config.sections")] == "another mod's replacement", "foreign replacement preserved");
            Check(!table.ContainsKey(UiLocalization.Token("config.range")), "owned native words removed");
            Check(UiLocalization.Text("config.close", "fallback") == "fallback", "no static owner after unload");
        }
        table.Clear(); PlayerPrefs.Language = "English";
        Console.WriteLine("PASS: " + checks + " localization checks across " + english.Count + " English/Turkish tokens and three reload rounds.");
    }
}
