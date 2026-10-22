#if DEBUG
using BepInEx.Logging;
using UnityEngine;

namespace Ironbound.Development;

// Opt-in startup smoke check: destroy only synthetic UI, then release managed owners.
// This exercises real Unity fake-null behavior without loading/modifying a world.
internal static class UiLifecycleRuntimeChecks
{
    internal static void Run(Theme theme, InterfaceSettings settings, ManualLogSource log)
    {
        for (int round = 0; round < 3; round++)
        {
            GameObject parent = new("Ironbound UI lifecycle check", typeof(RectTransform));
            ItemNotifications? notifications = null;
            CharacterHud? character = null;
            try
            {
                notifications = new ItemNotifications(theme, parent.transform);
                character = new CharacterHud(settings, theme, parent.transform);
                // Scene unload destroys children before the surviving C# owners release them.
                UnityEngine.Object.DestroyImmediate(parent);
                notifications.Clear();
                notifications.Dispose();
                notifications.Dispose();
                character.Dispose();
                character.Dispose();
            }
            finally
            {
                notifications?.Dispose();
                character?.Dispose();
                if (parent) UnityEngine.Object.DestroyImmediate(parent);
            }
        }
        log.LogInfo("Ironbound UI lifecycle checks PASS: destroyed notification fades/roots and portrait images, repeated cleanup.");
    }
}
#endif
