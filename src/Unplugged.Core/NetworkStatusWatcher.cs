using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Unplugged.Core;

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

    public static bool Probe()
    {
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
}
