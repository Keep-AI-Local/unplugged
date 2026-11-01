using System.Diagnostics;
using System.Globalization;

namespace Unplugged.Core;

public sealed record HardwareSample(double AppCpuPercent, double? GpuPercent, long AppMemoryBytes);

/// <summary>
/// Samples, about 4 times a second, how hard *this app* is making the processor work, and
/// (where the OS makes it available locally) how busy the graphics card is.
/// Everything is read from local OS counters. Nothing leaves the machine.
/// </summary>
public sealed class HardwareMonitor : IDisposable
{
    private readonly Process _self = Process.GetCurrentProcess();
    private readonly Timer _timer;
    private readonly IGpuReader? _gpu;
    private TimeSpan _lastCpu;
    private DateTime _lastWall;

    public event Action<HardwareSample>? Sampled;

    /// <summary>Plain-English description of where GPU numbers come from, or why they're unavailable.</summary>
    public string GpuSource => _gpu?.Description ?? "GPU activity isn't readable here. Open your system monitor to watch it.";
    public bool HasGpuReading => _gpu != null;

    public HardwareMonitor(BackendInfo? backend)
    {
        _lastCpu = _self.TotalProcessorTime;
        _lastWall = DateTime.UtcNow;
        if (backend?.UsingGpu == true) _gpu = GpuReaders.Create(backend.DeviceName);
        _timer = new Timer(_ => Tick(), null, 250, 250);
    }

    private void Tick()
    {
        try
        {
            _self.Refresh();
            var cpu = _self.TotalProcessorTime;
            var now = DateTime.UtcNow;
            var pct = (cpu - _lastCpu).TotalMilliseconds / Math.Max(1, (now - _lastWall).TotalMilliseconds) / Environment.ProcessorCount * 100.0;
            _lastCpu = cpu; _lastWall = now;
            Sampled?.Invoke(new HardwareSample(Math.Clamp(pct, 0, 100), _gpu?.Read(), _self.WorkingSet64));
        }
        catch { /* sampling is best-effort */ }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _gpu?.Dispose();
        _self.Dispose();
    }
}

internal interface IGpuReader : IDisposable
{
    string Description { get; }
    double? Read();
}

internal static class GpuReaders
{
    public static IGpuReader? Create(string deviceName)
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                if (deviceName.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) && NvidiaSmiReader.TryCreate() is { } nv) return nv;
                if (SysfsBusyReader.TryCreate() is { } sys) return sys;
            }
            if (OperatingSystem.IsWindows())
            {
                if (WindowsGpuReader.TryCreate() is { } win) return win;
            }
        }
        catch { /* no reading */ }
        return null;
    }
}

/// <summary>Linux + NVIDIA: runs the local nvidia-smi tool in "loop" mode and reads its output.</summary>
internal sealed class NvidiaSmiReader : IGpuReader
{
    private readonly Process _p;
    private volatile int _last = -1;
    public string Description => "Graphics card activity read from your local NVIDIA driver (nvidia-smi).";

    private NvidiaSmiReader(Process p)
    {
        _p = p;
        _p.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null && int.TryParse(e.Data.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) _last = v;
        };
        _p.BeginOutputReadLine();
    }

    public static NvidiaSmiReader? TryCreate()
    {
        var psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=utilization.gpu --format=csv,noheader,nounits -lms 250")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        try { var p = Process.Start(psi); return p == null ? null : new NvidiaSmiReader(p); }
        catch { return null; }
    }

    public double? Read() => _last < 0 ? null : _last;
    public void Dispose() { try { if (!_p.HasExited) _p.Kill(); } catch { } _p.Dispose(); }
}

/// <summary>Linux + AMD (and some Intel): /sys/class/drm/card*/device/gpu_busy_percent.</summary>
internal sealed class SysfsBusyReader : IGpuReader
{
    private readonly string _path;
    public string Description => "Graphics card activity read from your local graphics driver.";
    private SysfsBusyReader(string path) => _path = path;

    public static SysfsBusyReader? TryCreate()
    {
        var f = Directory.Exists("/sys/class/drm")
            ? Directory.EnumerateDirectories("/sys/class/drm", "card*")
                .Select(d => Path.Combine(d, "device", "gpu_busy_percent")).FirstOrDefault(File.Exists)
            : null;
        return f == null ? null : new SysfsBusyReader(f);
    }

    public double? Read() => double.TryParse(File.ReadAllText(_path).Trim(), CultureInfo.InvariantCulture, out var v) ? v : null;
    public void Dispose() { }
}

/// <summary>Windows: the same "GPU Engine" counters Task Manager shows (3D engine utilization).</summary>
internal sealed class WindowsGpuReader : IGpuReader
{
    private readonly List<PerformanceCounter> _counters = [];
    private DateTime _lastRefresh = DateTime.MinValue;
    public string Description => "Graphics card activity read from Windows' own counters (the same ones Task Manager uses).";

    public static WindowsGpuReader? TryCreate()
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (!PerformanceCounterCategory.Exists("GPU Engine")) return null;
        var r = new WindowsGpuReader();
        r.Refresh();
        return r;
    }

    private void Refresh()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var c in _counters) c.Dispose();
        _counters.Clear();
        var cat = new PerformanceCounterCategory("GPU Engine");
        foreach (var inst in cat.GetInstanceNames().Where(n => n.Contains("engtype_3D") || n.Contains("engtype_Compute")))
        {
            var c = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst, readOnly: true);
            c.NextValue();
            _counters.Add(c);
        }
        _lastRefresh = DateTime.UtcNow;
    }

    public double? Read()
    {
        if (!OperatingSystem.IsWindows()) return null;
        if (DateTime.UtcNow - _lastRefresh > TimeSpan.FromSeconds(10)) Refresh(); // engine instances come and go
        double sum = 0;
        foreach (var c in _counters) { try { sum += c.NextValue(); } catch { } }
        return Math.Clamp(sum, 0, 100);
    }

    public void Dispose() { foreach (var c in _counters) c.Dispose(); }
}
