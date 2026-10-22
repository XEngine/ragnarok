using Ironbound.UI;

internal static class BuildMenuChecks
{
    internal static void Run(Action<bool, string> check)
    {
        check(BuildMenuRules.Window(0, 5, 4, 0, 160) == (0, 0), "Empty build grid must allocate no cards");
        check(BuildMenuRules.Window(10, 5, 4, 0, 160) == (0, 10), "Small build list was truncated");
        check(BuildMenuRules.Window(300, 5, 4, 0, 160) == (0, 25), "Large list did not bound the card pool");
        check(BuildMenuRules.Window(13, 5, 2, 9000, 160) == (5, 8), "Partial last row/overscroll lost pieces");
        check(BuildMenuRules.Window(100, 5, 4, -60, 160).First == 0, "Negative overscroll lost the first row");
        Random random = new(8301);
        for (int trial = 0; trial < 3000; trial++)
        {
            int total = random.Next(1, 2000), columns = random.Next(4, 17), rows = random.Next(3, 11);
            float stride = random.Next(40, 200);
            int totalRows = (total + columns - 1) / columns;
            float maxOffset = Math.Max(0, (totalRows - rows) * stride);
            float offset = (float)random.NextDouble() * maxOffset;
            var window = BuildMenuRules.Window(total, columns, rows, offset, stride);
            check(window.First >= 0 && window.First % columns == 0, "Build window is not aligned to a logical row");
            check(window.Count <= (rows + 1) * columns && window.First + window.Count <= total, "Build window exceeds its pool/data bounds");
            // Every cell intersecting the viewport must be represented, including the
            // extra partial row at fractional scroll offsets and incomplete last rows.
            int intersectingFirst = (int)Math.Floor(offset / stride) * columns;
            int intersectingLast = Math.Min(total, (int)Math.Ceiling(offset / stride + rows) * columns);
            check(window.First <= intersectingFirst && window.First + window.Count >= intersectingLast,
                "Scrolling exposes a piece outside the rendered window");
        }
        Console.WriteLine("PASS: build card pooling/scroll coverage (3,000 randomized viewports).");
    }
}
