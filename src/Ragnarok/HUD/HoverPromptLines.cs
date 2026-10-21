using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ragnarok.HUD;

// Separate native hover content from bracketed controls, preserving rich sprite
// glyphs and action wording supplied by the game or other mods.
internal static class HoverPromptLines
{
    internal sealed class Line
    {
        internal string Text = "";
        internal string[] Keys = Array.Empty<string>();
        internal bool Hold, Heading, Quiet;
        internal string Signature => Heading + "|" + Hold + "|" + string.Join("|", Keys);
    }
    private static readonly Regex Action = new(@"^\s*\[(?<keys>[^\]\r\n]+)\]\s*(?<label>.+)$", RegexOptions.Compiled);
    private static readonly Regex Weight = new(@"</?(?:b|size|font|font-weight)(?:=[^>]*)?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Tags = new(@"<(?!sprite\b)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Parts = new(@"<sprite\b[^>]*>|[^+/<]+|[+/]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static List<Line> Parse(string source)
    {
        var result = new List<Line>();
        string[] lines = source.Replace("\r", "").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string text = lines[i].Trim();
            if (text.Length == 0) continue;
            Match action = Action.Match(text);
            var line = new Line { Heading = result.Count == 0 && !action.Success };
            if (action.Success)
            {
                string keys = Tags.Replace(action.Groups["keys"].Value, "").Trim();
                if (keys.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase))
                { line.Hold = true; keys = keys.Substring(5).Trim(); }
                line.Keys = Parts.Matches(keys).Cast<Match>().Select(m => Normalize(m.Value.Trim())).Where(k => k.Length > 0).ToArray();
                // KeyboardShortcut.ToString places the main key before modifiers.
                // Put modifiers first for the same presentation as native chords.
                if (!line.Keys.Contains("/"))
                {
                    var chord = line.Keys.Where(k => k != "+").OrderBy(k => Modifier(k) ? 0 : 1).ToArray();
                    line.Keys = chord.SelectMany((key, index) => index == 0 ? new[] { key } : new[] { "+", key }).ToArray();
                }
                line.Text = Weight.Replace(action.Groups["label"].Value, "").Trim();
            }
            else line.Text = line.Heading ? text : Weight.Replace(text, "");
            string plain = Tags.Replace(line.Text, "").Trim();
            line.Quiet = !line.Heading && line.Keys.Length == 0 && plain.StartsWith("(", StringComparison.Ordinal) && plain.EndsWith(")", StringComparison.Ordinal);
            result.Add(line);
        }
        return result;
    }

    private static bool Modifier(string key) => key is "Shift" or "Ctrl" or "Alt" or "Cmd";
    private static string Normalize(string key) => key switch
    {
        "LeftShift" or "RightShift" => "Shift",
        "LeftControl" or "RightControl" or "Control" => "Ctrl",
        "LeftAlt" or "RightAlt" => "Alt",
        "LeftCommand" or "RightCommand" => "Cmd",
        _ => key
    };
}
