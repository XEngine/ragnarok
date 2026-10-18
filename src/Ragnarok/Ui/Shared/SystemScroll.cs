using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Ragnarok.UI;

// The Windows "lines to scroll per notch" mouse setting (SPI_GETWHEELSCROLLLINES), re-read every few
// seconds so a change in Windows applies without restarting. Absent off Windows.
internal static class SystemScroll
{
    private const uint GetWheelScrollLines = 0x0068;
    private const uint PageScroll = 0xFFFFFFFF; // "One screen at a time".

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref uint value, uint winIni);

    private static float cached = -1, readAt = -100;
    private static bool unavailable;

    // Lines per notch, a large step for "one screen at a time", or null when there is no such setting.
    internal static float? Lines()
    {
        if (unavailable || Application.platform != RuntimePlatform.WindowsPlayer) return null;
        if (Time.unscaledTime - readAt < 3f) return cached > 0 ? cached : null;
        readAt = Time.unscaledTime;
        try
        {
            uint lines = 3;
            if (!SystemParametersInfo(GetWheelScrollLines, 0, ref lines, 0)) return null;
            cached = lines == PageScroll ? 12f : lines;
        }
        catch (Exception)
        {
            unavailable = true;
            return null;
        }
        return cached > 0 ? cached : null;
    }
}
