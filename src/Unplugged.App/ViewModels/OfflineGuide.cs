using System;
using System.Collections.Generic;

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
    public IReadOnlyList<GuideStep> OffSteps { get; init; } = [];
    public IReadOnlyList<GuideStep> OnSteps { get; init; } = [];

    public static OfflineGuide ForThisComputer()
    {
        if (OperatingSystem.IsWindows())
            return Environment.OSVersion.Version.Build >= 22000 ? Windows11() : Windows10();
        if (OperatingSystem.IsMacOS()) return Mac();
        var desktop = (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "").ToUpperInvariant();
        if (desktop.Contains("KDE")) return LinuxKde();
        if (desktop.Contains("GNOME") || desktop.Contains("UBUNTU")) return LinuxGnome();
        return LinuxOther();
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
