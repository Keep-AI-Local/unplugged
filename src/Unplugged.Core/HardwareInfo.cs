using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Unplugged.Core;

public enum HardwareTier { CpuOnly, SmallGpu, Gpu, BigGpu }

/// <summary>Static facts about this computer, read locally.</summary>
public static class HardwareInfo
{
    private static string? _cpuName;

    public static int CpuThreads => Environment.ProcessorCount;
    public static long TotalRamBytes => GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public static string OsName =>
        OperatingSystem.IsWindows() ? "Windows" :
        OperatingSystem.IsMacOS() ? "macOS" :
        OperatingSystem.IsLinux() ? "Linux" : RuntimeInformation.OSDescription;

    public static string CpuName => _cpuName ??= ReadCpuName();

    private static string ReadCpuName()
    {
        try
        {
            if (OperatingSystem.IsLinux())
            {
                var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name"));
                if (line != null) return line[(line.IndexOf(':') + 1)..].Trim();
            }
            else if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string s) return s.Trim();
            }
            else if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo("sysctl", "-n machdep.cpu.brand_string") { RedirectStandardOutput = true, UseShellExecute = false };
                using var p = Process.Start(psi)!;
                var s = p.StandardOutput.ReadToEnd().Trim();
                p.WaitForExit(2000);
                if (s.Length > 0) return s;
            }
        }
        catch { /* fall through */ }
        return $"{CpuThreads}-thread processor";
    }

    /// <summary>
    /// Rough tier for the "what could your computer do" screen.
    /// Image generation needs roughly 6–8 GB of graphics memory today; video roughly 12 GB+.
    /// </summary>
    public static HardwareTier Tier(BackendInfo? b)
    {
        if (b is null || !b.UsingGpu) return HardwareTier.CpuOnly;
        // Apple Silicon shares system memory with the GPU.
        long mib = OperatingSystem.IsMacOS() ? TotalRamBytes / (1024 * 1024) * 2 / 3 : b.GpuFreeMiB ?? 0;
        // Free VRAM is reported *after* other apps take their share; allow for that.
        if (mib >= 11_000) return HardwareTier.BigGpu;
        if (mib >= 5_500) return HardwareTier.Gpu;
        return HardwareTier.SmallGpu;
    }
}
