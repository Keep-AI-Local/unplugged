using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Unplugged.Core;

/// <summary>How this computer currently reaches the internet (read from the OS, nothing is sent).</summary>
[Flags]
public enum ConnectionKind { None = 0, WiFi = 1, Wired = 2 }

/// <summary>
/// Tells whether this computer currently has a route to the outside world, by *reading* the
/// operating system's own network-adapter list. It never sends a single packet: no pings,
/// no DNS lookups, no "connectivity checks". (A real connection test would contradict the
/// whole point of this app; the user proves it in their browser instead.)
///
/// "Online" = at least one adapter that is up, isn't loopback/tunnel, and has a default
/// gateway. Virtual bridges (Docker, VMs) have no gateway, so they don't count.
/// </summary>
public sealed class NetworkStatusWatcher : IDisposable
{
    private readonly Timer _timer;
    private bool? _last;

    public event Action<bool>? StatusChanged;
    public bool IsOnline { get; private set; }

    public NetworkStatusWatcher()
    {
        IsOnline = Probe();
        _last = IsOnline;
        NetworkChange.NetworkAvailabilityChanged += OnChange;
        NetworkChange.NetworkAddressChanged += OnChange;
        _timer = new Timer(_ => Check(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void OnChange(object? sender, EventArgs e) => Check();

    private void Check()
    {
        bool now;
        try { now = Probe(); } catch { return; }
        IsOnline = now;
        if (_last != now)
        {
            _last = now;
            StatusChanged?.Invoke(now);
        }
    }

    /// <summary>Which kinds of adapters currently carry a route to the internet (Wi-Fi, cable, or both).</summary>
    public static ConnectionKind ActiveKind()
    {
        // macOS: .NET reports the Wi-Fi adapter (en0) as "Ethernet", so ask macOS itself which device is Wi-Fi.
        if (OperatingSystem.IsMacOS() && MacDefaultRoute(out var iface) is { } online)
            return !online ? ConnectionKind.None
                 : MacWifiDevices().Contains(iface!) ? ConnectionKind.WiFi : ConnectionKind.Wired;

        var kind = ConnectionKind.None;
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            bool hasGateway;
            try { hasGateway = nic.GetIPProperties().GatewayAddresses.Any(g => !g.Address.Equals(System.Net.IPAddress.Any) && !g.Address.Equals(System.Net.IPAddress.IPv6Any)); }
            catch { continue; }
            if (!hasGateway) continue;
            kind |= nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? ConnectionKind.WiFi : ConnectionKind.Wired;
        }
        return kind;
    }

    public static bool Probe()
    {
        // macOS: read the routing table directly (most reliable there); falls back below if that fails.
        if (OperatingSystem.IsMacOS() && MacDefaultRoute(out _) is { } macOnline) return macOnline;

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up) continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
            IPInterfaceProperties props;
            try { props = nic.GetIPProperties(); } catch { continue; }
            foreach (var gw in props.GatewayAddresses)
            {
                var a = gw.Address;
                if (a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(System.Net.IPAddress.Any)) return true;
                if (a.AddressFamily == AddressFamily.InterNetworkV6 && !a.Equals(System.Net.IPAddress.IPv6Any)) return true;
            }
        }
        return false;
    }

    public void Dispose()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnChange;
        NetworkChange.NetworkAddressChanged -= OnChange;
        _timer.Dispose();
    }

    // ---------- macOS helpers: local system tools only, nothing is sent over the network ----------

    /// <summary>
    /// Asks macOS for the default route ("route -n get default"). Returns true/false for online/offline and the
    /// interface carrying it, or null if the check itself couldn't run.
    /// </summary>
    private static bool? MacDefaultRoute(out string? iface)
    {
        iface = null;
        var (code, output) = RunLocal("/sbin/route", "-n get default");
        if (code is null) return null;
        if (code != 0) return false;   // "not in table": no default route, i.e. offline
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("interface:", StringComparison.Ordinal)) iface = t["interface:".Length..].Trim();
        }
        return iface != null;
    }

    private static HashSet<string>? _macWifi;
    /// <summary>Device names macOS lists as Wi-Fi (e.g. "en0"), from "networksetup -listallhardwareports".</summary>
    private static HashSet<string> MacWifiDevices()
    {
        if (_macWifi != null) return _macWifi;
        var set = new HashSet<string>(StringComparer.Ordinal);
        var (code, output) = RunLocal("/usr/sbin/networksetup", "-listallhardwareports");
        if (code == 0)
        {
            bool wifiPort = false;
            foreach (var line in output.Split('\n'))
            {
                var t = line.Trim();
                if (t.StartsWith("Hardware Port:", StringComparison.Ordinal))
                    wifiPort = t.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) || t.Contains("AirPort", StringComparison.OrdinalIgnoreCase);
                else if (wifiPort && t.StartsWith("Device:", StringComparison.Ordinal))
                    set.Add(t["Device:".Length..].Trim());
            }
        }
        else set.Add("en0");   // the Wi-Fi adapter on virtually every Mac
        return _macWifi = set;
    }

    private static (int? Code, string Output) RunLocal(string file, string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(file, args)
            { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return (null, "");
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return (null, ""); }
            return (p.ExitCode, output);
        }
        catch { return (null, ""); }
    }
}
