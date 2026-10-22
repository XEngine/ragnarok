using System;
using System.IO;

namespace Ironbound.Configuration;

internal static class BrandingMigration
{
    internal const string LegacyGuid = "com.kernelpanic.ragnarok";
    internal const string CurrentGuid = "com.kernelpanic.ironbound";

    // Copy once before any Bind. Keep the source and never replace an existing
    // Ironbound config. Atomic rename avoids leaving a partial destination.
    internal static bool ImportConfig(string path)
    {
        if (!string.Equals(Path.GetFileName(path), CurrentGuid + ".cfg", StringComparison.OrdinalIgnoreCase) || File.Exists(path)) return false;
        string legacy = Path.Combine(Path.GetDirectoryName(path)!, LegacyGuid + ".cfg");
        if (!File.Exists(legacy)) return false;
        string temporary = path + ".import-" + Guid.NewGuid().ToString("N");
        try { File.Copy(legacy, temporary); File.Move(temporary, path); return true; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
