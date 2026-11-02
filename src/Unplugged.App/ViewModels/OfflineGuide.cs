using System;
using System.Collections.Generic;
using System.Linq;
using Unplugged.Core;

namespace Unplugged.App.ViewModels;

public enum GuidePicture { None, Wifi, Plane, Cable, WifiAndWired }

/// <summary>What the "connected by cable" symbol looks like on each system.</summary>
public enum WiredLook { None, Monitor, Lan, Jack }

/// <summary>One numbered instruction. Short, one action, plain words.</summary>
public sealed record GuideStep(int Number, string Text, GuidePicture Picture = GuidePicture.None, string? Tip = null, WiredLook Wired = WiredLook.None)
{
    public bool ShowWifi => Picture == GuidePicture.Wifi;
    public bool ShowPlane => Picture == GuidePicture.Plane;
    public bool ShowCable => Picture == GuidePicture.Cable;
    public bool ShowBoth => Picture == GuidePicture.WifiAndWired;
    public bool WiredMonitor => Wired == WiredLook.Monitor;
    public bool WiredLan => Wired == WiredLook.Lan;
    public bool WiredJack => Wired == WiredLook.Jack;
    public bool WiredNone => Wired == WiredLook.None;
    public bool HasTip => !string.IsNullOrEmpty(Tip);
}

/// <summary>
/// "How to turn off (and back on) your internet", written for someone who has never done it.
/// Picks wording for the exact system this is running on.
/// </summary>
public sealed class OfflineGuide
{
    public string SystemName { get; init; } = "";
    /// <summary>True when these steps are for a computer connected with a network cable.</summary>
    public bool IsWired { get; init; }
    public IReadOnlyList<GuideStep> OffSteps { get; init; } = [];
    public IReadOnlyList<GuideStep> OnSteps { get; init; } = [];

    public static OfflineGuide ForThisComputer() => ForThisComputer(NetworkStatusWatcher.ActiveKind());

    public static OfflineGuide ForThisComputer(ConnectionKind kind)
    {
        // Plugged in with a cable and no Wi-Fi: there's no Wi-Fi / Airplane-mode switch to use.
        if (kind == ConnectionKind.Wired) return Wired();
        var g = WiFiGuide();
        // Both Wi-Fi and a cable: both must go.
        if (kind == (ConnectionKind.WiFi | ConnectionKind.Wired))
        {
            var steps = g.OffSteps.ToList();
            var last = steps[^1];
            steps.Insert(steps.Count - 1, new GuideStep(steps.Count, "This computer is ALSO plugged in with a network cable. Unplug that too: press the little clip on the plug and pull it out gently.", GuidePicture.Cable));
            steps[^1] = last with { Number = steps.Count };
            return new OfflineGuide { SystemName = g.SystemName + ", Wi-Fi and cable", OffSteps = steps, OnSteps = g.OnSteps.Append(new GuideStep(g.OnSteps.Count + 1, "Plug the network cable back in.", GuidePicture.Cable)).ToList() };
        }
        return g;
    }

    private static OfflineGuide WiFiGuide()
    {
        if (OperatingSystem.IsWindows())
            return Environment.OSVersion.Version.Build >= 22000 ? Windows11() : Windows10();
        if (OperatingSystem.IsMacOS()) return Mac();
        var desktop = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").ToUpperInvariant();
        if (desktop.Contains("KDE")) return LinuxKde();
        if (desktop.Contains("GNOME") || desktop.Contains("UBUNTU")) return LinuxGnome();
        return LinuxOther();
    }

    private const string FindTheCable =
        "Look at the back or side of your computer for a cable with a plug a bit wider than a phone charger and a little clip on it.";

    private static OfflineGuide Wired()
    {
        if (OperatingSystem.IsWindows()) return new()
        {
            SystemName = "Windows, connected with a cable", IsWired = true,
            OffSteps =
            [
                new(1, "This computer is connected with a network cable, so there's no Airplane-mode switch. The simplest way: unplug the cable.", GuidePicture.Cable, FindTheCable),
                new(2, "Press the little clip down and gently pull the plug out."),
                new(3, "Can't reach the cable? Click \"Open Windows' network settings\" below, then click \"Disable\" next to Ethernet. If Windows asks for permission, click \"Yes\"."),
                new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE."),
            ],
            OnSteps =
            [
                new(1, "Plug the network cable back in.", GuidePicture.Cable),
                new(2, "If you used \"Disable\" instead: open the same settings page and click \"Enable\" next to Ethernet. Your internet comes back in a few seconds."),
            ],
        };
        if (OperatingSystem.IsMacOS()) return new()
        {
            SystemName = "Mac, connected with a cable", IsWired = true,
            OffSteps =
            [
                new(1, "This Mac is connected with a network cable. The simplest way: unplug the cable (on many Macs it goes into a small adapter).", GuidePicture.Cable, FindTheCable),
                new(2, "Press the little clip down and gently pull the plug out.",
                    Tip: "Can't reach it? Open System Settings -> Network, Control-click \"Ethernet\" and choose \"Make Service Inactive\"."),
                new(3, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE."),
            ],
            OnSteps =
            [
                new(1, "Plug the network cable back in.", GuidePicture.Cable),
                new(2, "If you used \"Make Service Inactive\": Control-click \"Ethernet\" in System Settings -> Network again and choose \"Make Service Active\"."),
            ],
        };
        var desktop = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").ToUpperInvariant();
        string how = desktop.Contains("KDE")
            ? "Or, without unplugging: click the network symbol near the clock and click \"Disconnect\" next to your wired connection."
            : desktop.Contains("GNOME") || desktop.Contains("UBUNTU")
                ? "Or, without unplugging: click the symbols at the top-right of your screen and turn off the \"Wired\" button."
                : "Or, without unplugging: click the network symbol in your panel and disconnect the wired connection.";
        return new()
        {
            SystemName = "Linux, connected with a cable", IsWired = true,
            OffSteps =
            [
                new(1, "This computer is connected with a network cable. The simplest way: unplug the cable.", GuidePicture.Cable, FindTheCable),
                new(2, "Press the little clip down and gently pull the plug out.", Tip: how),
                new(3, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE."),
            ],
            OnSteps =
            [
                new(1, "Plug the network cable back in (or reconnect the wired connection the same way you disconnected it).", GuidePicture.Cable),
            ],
        };
    }

    private const string KeyboardTip =
        "Shortcut: many laptops have an airplane key on the top row of the keyboard. Pressing it (sometimes together with the \"Fn\" key) does the same thing.";

    private static OfflineGuide Windows11() => new()
    {
        SystemName = "Windows 11",
        OffSteps =
        [
            new(1, "Look at the bottom-right corner of your screen, next to the clock. Find your internet symbol. It looks like one of these:", GuidePicture.WifiAndWired,
                Wired: WiredLook.Monitor),
            new(2, "Click it once. A small panel pops up with some square buttons."),
            new(3, "Click the square button that says \"Airplane mode\". It has a little airplane on it:", GuidePicture.Plane,
                "When it's on, the button turns a bright color."),
            new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE.", Tip: KeyboardTip),
        ],
        OnSteps =
        [
            new(1, "Click the symbol next to the clock again (it may now look like an airplane).", GuidePicture.Plane),
            new(2, "Click \"Airplane mode\" again so it turns off. Your internet comes back in a few seconds."),
        ],
    };

    private static OfflineGuide Windows10() => new()
    {
        SystemName = "Windows 10",
        OffSteps =
        [
            new(1, "Look at the bottom-right corner of your screen, next to the clock. Find your internet symbol. It looks like one of these:", GuidePicture.WifiAndWired,
                Wired: WiredLook.Monitor),
            new(2, "Click it once. A list of networks pops up."),
            new(3, "At the bottom of that list, click the square button that says \"Airplane mode\":", GuidePicture.Plane,
                "When it's on, the button turns a bright color."),
            new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE.", Tip: KeyboardTip),
        ],
        OnSteps =
        [
            new(1, "Click the symbol next to the clock again (it may now look like an airplane).", GuidePicture.Plane),
            new(2, "Click \"Airplane mode\" again so it turns off. Your internet comes back in a few seconds."),
        ],
    };

    private static OfflineGuide Mac() => new()
    {
        SystemName = "Mac",
        OffSteps =
        [
            new(1, "Look at the top-right corner of your screen, in the thin bar along the top. Find the Wi-Fi symbol. It looks like this:", GuidePicture.WifiAndWired,
                "Don't see it? Click the icon that looks like two little switches (Control Center) in the same corner. The Wi-Fi button is in there.",
                WiredLook.None),
            new(2, "Click it once. A small menu opens."),
            new(3, "Click the switch next to the word \"Wi-Fi\" so it turns off (it goes grey)."),
            new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE."),
        ],
        OnSteps =
        [
            new(1, "Click the Wi-Fi symbol at the top-right of your screen again.", GuidePicture.Wifi),
            new(2, "Click the switch next to \"Wi-Fi\" so it turns back on (blue). Your Mac reconnects in a few seconds."),
        ],
    };

    private static OfflineGuide LinuxKde() => new()
    {
        SystemName = "Linux (KDE Plasma)",
        OffSteps =
        [
            new(1, "Look at the bottom-right corner of your screen, near the clock. Find your network symbol. It looks something like one of these:", GuidePicture.WifiAndWired,
                Wired: WiredLook.Jack),
            new(2, "Click it once. A panel opens with your networks."),
            new(3, "At the top of that panel, click the button with a little airplane on it (\"Airplane mode\").", GuidePicture.Plane),
            new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE.", Tip: KeyboardTip),
        ],
        OnSteps =
        [
            new(1, "Click the network symbol near the clock again.", GuidePicture.Plane),
            new(2, "Click the airplane button again so Airplane mode turns off. Your internet comes back in a few seconds."),
        ],
    };

    private static OfflineGuide LinuxGnome() => new()
    {
        SystemName = "Linux (GNOME / Ubuntu)",
        OffSteps =
        [
            new(1, "Look at the top-right corner of your screen. Click the group of small symbols there (your network symbol, sound and battery). The network symbol looks like one of these:", GuidePicture.WifiAndWired,
                Wired: WiredLook.Lan),
            new(2, "A panel opens with round buttons. Click the \"Wi-Fi\" button so it turns off (grey).",
                Tip: "If you see an \"Airplane Mode\" button, you can click that instead. If you use a cable, click the \"Wired\" button instead."),
            new(3, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE.", Tip: KeyboardTip),
        ],
        OnSteps =
        [
            new(1, "Click the small symbols at the top-right of your screen again."),
            new(2, "Click the \"Wi-Fi\" button so it turns back on. Your internet comes back in a few seconds."),
        ],
    };

    private static OfflineGuide LinuxOther() => new()
    {
        SystemName = "Linux",
        OffSteps =
        [
            new(1, "Find the network symbol in your screen's panel (usually in a corner, near the clock). It looks something like one of these:", GuidePicture.WifiAndWired,
                Wired: WiredLook.Lan),
            new(2, "Click it once."),
            new(3, "Turn off Wi-Fi, or turn on \"Airplane mode\" if you see it.", GuidePicture.Plane),
            new(4, "That's it! Now look at the top of this window. The badge should turn red and say OFFLINE.", Tip: KeyboardTip),
        ],
        OnSteps =
        [
            new(1, "Click the network symbol again."),
            new(2, "Turn Wi-Fi back on (or Airplane mode off). Your internet comes back in a few seconds."),
        ],
    };
}
