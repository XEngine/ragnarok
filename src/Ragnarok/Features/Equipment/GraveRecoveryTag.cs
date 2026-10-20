using System;

namespace Ragnarok.Features.Equipment;

internal static class GraveRecoveryTag
{
    internal static bool TryRead(string? value, long owner, out int slot)
    {
        slot = -1;
        if (string.IsNullOrEmpty(value)) return false;
        string[] fields = value!.Split(':');
        return fields.Length == 3 && long.TryParse(fields[0], out long markedOwner) && markedOwner == owner &&
            int.TryParse(fields[1], out slot) && Guid.TryParseExact(fields[2], "N", out _);
    }
}
