using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Ironbound.HUD;

// Local presentation previews, available without cheats or server authority.
internal sealed class PopupCommands : IDisposable
{
    private const string Name = "ironbound_popup";
    private static readonly string[] Kinds = { "pickup", "unlock", "rested", "wet", "warning", "skill", "milestone",
        "system", "blocked", "failure", "death", "discovery", "power", "day", "all", "clear" };
    private static readonly FieldInfo Commands = AccessTools.Field(typeof(Terminal), "commands");
    private static readonly MethodInfo Refresh = AccessTools.Method(typeof(Terminal), "updateCommandList");
    private readonly NotificationFeed feed;
    private readonly Terminal.ConsoleCommand command;
    private readonly Terminal.ConsoleCommand? previous;
    private readonly Queue<string> pending = new();
    private Player? previewPlayer;
    private int total;
    private float next;

    internal PopupCommands(NotificationFeed feed)
    {
        this.feed = feed;
        Registry.TryGetValue(Name, out previous);
        command = new Terminal.ConsoleCommand(Name, "[type] Preview Ironbound popups; close F5 to play. No gameplay changes.",
            (Terminal.ConsoleEventArgs args) => Run(args), optionsFetcher: () => new List<string>(Kinds));
        RefreshConsole();
    }

    private static Dictionary<string, Terminal.ConsoleCommand> Registry =>
        (Dictionary<string, Terminal.ConsoleCommand>)Commands.GetValue(null);

    private static void RefreshConsole()
    {
        if (global::Console.instance) Refresh.Invoke(global::Console.instance, null);
    }

    private void Run(Terminal.ConsoleEventArgs args)
    {
        string kind = args.ArgsAll.Trim().ToLowerInvariant();
        if (Array.IndexOf(Kinds, kind) < 0)
        {
            args.Context.AddString("Usage: " + Name + " <" + string.Join("|", Kinds) + ">");
            args.Context.AddString("UI previews only. 'all' plays each sample in sequence; 'clear' cancels them.");
            return;
        }
        if (!Player.m_localPlayer || !feed.Active)
        {
            args.Context.AddString("Load a world and select Ironbound notifications in settings first.");
            return;
        }
        pending.Clear();
        feed.Preview("clear");
        if (kind == "clear") { args.Context.AddString("Popup previews cleared."); return; }
        if (kind == "all")
        {
            foreach (string sample in Kinds)
                if (sample != "all" && sample != "clear") pending.Enqueue(sample);
        }
        else pending.Enqueue(kind);
        previewPlayer = Player.m_localPlayer;
        total = pending.Count;
        next = Time.unscaledTime + 0.25f;
        args.Context.AddString("Close F5 to preview " + kind + ": " + total + " sample(s), one every 4.6 seconds. Reopening F5 pauses playback.");
    }

    internal void Tick()
    {
        if (pending.Count == 0) return;
        if (!Player.m_localPlayer || Player.m_localPlayer != previewPlayer)
        {
            pending.Clear();
            Report("Preview cancelled: the player/world changed.");
            return;
        }
        // A hidden or temporarily rebuilding HUD must not discard the remaining samples.
        if (global::Console.IsVisible() || global::Hud.IsUserHidden() || !feed.Active)
        { next = Time.unscaledTime + 0.25f; return; }
        if (Time.unscaledTime < next) return;
        string sample = pending.Peek();
        try
        {
            feed.Preview(sample);
            pending.Dequeue();
            Report("Preview " + (total - pending.Count) + "/" + total + ": " + sample +
                (pending.Count == 0 ? " (sequence complete)." : "; next: " + pending.Peek() + " in 4.6s."));
        }
        catch (Exception error)
        {
            pending.Clear();
            Report("Preview failed at " + sample + "; see BepInEx/LogOutput.log.");
            Debug.LogException(error);
        }
        next = Time.unscaledTime + 4.6f;
    }

    private static void Report(string text)
    {
        if (global::Console.instance) global::Console.instance.AddString("[Ironbound] " + text);
        Debug.Log("[Ironbound] " + text);
    }

    public void Dispose()
    {
        pending.Clear();
        if (Registry.TryGetValue(Name, out Terminal.ConsoleCommand installed) && ReferenceEquals(installed, command))
        {
            if (previous != null) Registry[Name] = previous;
            else Registry.Remove(Name);
            RefreshConsole();
        }
    }
}
