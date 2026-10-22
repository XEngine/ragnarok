using System;

namespace Ironbound.UI;

// Pure display rules shared with offline checks; no inventory or game writes.
internal static class BuildMenuRules
{
    internal static (int First, int Count) Window(int total, int columns, int rows, float offset, float rowStride)
    {
        if (total <= 0) return (0, 0);
        columns = Math.Max(1, columns); rows = Math.Max(1, rows);
        int totalRows = (total + columns - 1) / columns;
        int firstRow = rowStride > 0 ? (int)Math.Floor(Math.Max(0, offset) / rowStride) : 0;
        firstRow = Math.Min(firstRow, Math.Max(0, totalRows - rows));
        int first = firstRow * columns;
        return (first, Math.Min(total - first, (rows + 1) * columns));
    }

}
