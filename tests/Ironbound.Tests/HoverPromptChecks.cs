using Ironbound.HUD;

internal static class HoverPromptChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var rows = HoverPromptLines.Parse("Moose (Tame, Hungry)\n[<color=yellow><b>E</b></color>] <b>Pet</b>\n[Shift + E] Rename\n[Hold H + LeftShift] <size=20>Release mount bond</size>");
        check(rows.Count == 4 && rows[0].Heading && rows[0].Text == "Moose (Tame, Hungry)", "Hover title/status changed");
        check(!rows[1].Heading && rows[1].Keys.SequenceEqual(new[] { "E" }) && rows[1].Text == "Pet", "Pet key/regular label not separated");
        check(rows[2].Keys.SequenceEqual(new[] { "Shift", "+", "E" }) && rows[2].Text == "Rename", "Native rename chord changed");
        check(rows[3].Hold && rows[3].Keys.SequenceEqual(new[] { "Shift", "+", "H" }) && rows[3].Text == "Release mount bond", "Hold or modifier normalization failed");
        var glyph = HoverPromptLines.Parse("[<sprite=\"gamepad\" name=\"button_a\">] <b>Use</b>")[0];
        check(glyph.Keys.Single() == "<sprite=\"gamepad\" name=\"button_a\">" && glyph.Text == "Use", "Gamepad sprite lost or wrapped in a bold label");
        var plusGlyph = HoverPromptLines.Parse("[<sprite name=\"plus+glyph\"> + E] Do something")[0];
        check(plusGlyph.Keys.Length == 3 && plusGlyph.Keys[0] == "<sprite name=\"plus+glyph\">", "Plus inside sprite metadata split the glyph");
        var alternatives = HoverPromptLines.Parse("[E / F] Use")[0];
        check(alternatives.Keys.SequenceEqual(new[] { "E", "/", "F" }), "Alternative keys were changed into a chord");
        rows = HoverPromptLines.Parse("\r\nTorch\r\n(Fuel 0/6)\r\n<color=red>Needs resin</color>\r\nModded description [not a leading control]");
        check(rows.Count == 4 && rows[0].Heading && rows[1].Quiet && !rows[2].Quiet, "Blank/CRLF lines or quiet condition classified incorrectly");
        check(rows[2].Text == "<color=red>Needs resin</color>" && rows[3].Keys.Length == 0, "Mod text/color changed or inline brackets treated as keys");
        var first = HoverPromptLines.Parse("[E] Pet")[0];
        check(!first.Heading && first.Keys.Length == 1, "Action-only hover became a heading");
        check(HoverPromptLines.Parse("").Count == 0 && HoverPromptLines.Parse("\n\n").Count == 0, "Empty hover created a row");
        var ctrl = HoverPromptLines.Parse("[Mouse-1 + RightControl + LeftAlt] <font-weight=900>Action</font-weight>")[0];
        check(ctrl.Keys.SequenceEqual(new[] { "Ctrl", "+", "Alt", "+", "Mouse-1" }) && ctrl.Text == "Action", "Mouse/modifier chord or font-weight cleanup failed");
        var timerA = HoverPromptLines.Parse("Plant\nGrowing — ready in ~2m")[1];
        var timerB = HoverPromptLines.Parse("Plant\nGrowing — ready in ~1m")[1];
        check(timerA.Signature == timerB.Signature, "Changing timer would rebuild the row's key controls");
        Console.WriteLine("PASS: hover action/key parsing, hold and modifier chords, regular labels, gamepad sprites, condition/mod text and reusable timer rows.");
    }
}
