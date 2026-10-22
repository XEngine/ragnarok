using Ironbound.UI;

internal static class CraftFrameChecks
{
    internal static void Run(Action<bool, string> check)
    {
        bool Bounds(float y, bool extension, float inset, out float left, out float right) =>
            CraftFrameShape.Bounds(570, 650, 104, 138, 40, extension, y, inset, out left, out right);
        check(Bounds(158, true, 0, out float left, out float right) && left == -104 && right == 570, "Repair extension is not flush with the panel");
        check(Bounds(158, false, 0, out left, out right) && left == 0 && right == 570, "Hidden repair leaves an extension");
        check(Bounds(100, true, 0, out left, out _) && left == 0, "Invisible extension area blocks neighboring inventory");
        check(Bounds(200, true, 0, out left, out _) && left == 0, "Extension extends beyond its vertical bounds");
        check(Bounds(158, true, 0.8f, out left, out right) && left < 0 && right > 0, "Inner outline creates a seam at the join");
        check(!Bounds(-1, true, 0, out _, out _), "Frame exists above its bounds");
        check(!Bounds(651, true, 0, out _, out _), "Frame exists below its bounds");
        for (float y = 0.125f; y < 650; y += 0.25f)
        {
            check(Bounds(y, true, 0, out float outerLeft, out float outerRight), "Outer scanline disappeared");
            if (!Bounds(y, true, 0.8f, out float innerLeft, out float innerRight)) continue;
            check(innerLeft >= outerLeft && innerRight <= outerRight, "Outline inset escaped the silhouette");
            if (y > 139 && y < 177) check(innerLeft < 0, "Outline closes off the repair join");
        }
        Console.WriteLine("PASS: continuous crafting/repair silhouette, border inset, hidden-extension bounds and join coverage.");
    }
}
