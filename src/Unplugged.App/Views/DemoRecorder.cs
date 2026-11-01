using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Unplugged.App.Views;

/// <summary>
/// Filming aid (only active when UNPLUGGED_RECORD_DIR is set): captures this window as PNG frames,
/// each named by its time in milliseconds, plus an events.txt timeline. A separate script turns the
/// frames into video. This is real footage of the real app; nothing is staged.
/// </summary>
public sealed class DemoRecorder
{
    private readonly Window _window;
    private readonly string _dir;
    private readonly double _scale;
    private readonly Stopwatch _clock = new();
    private DispatcherTimer? _timer;
    private int _pending;

    public DemoRecorder(Window window, string dir, double scale = 1.5)
    {
        _window = window; _dir = dir; _scale = scale;
        Directory.CreateDirectory(Path.Combine(dir, "frames"));
    }

    public void Start(int fps = 20)
    {
        _clock.Start();
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1000.0 / fps), DispatcherPriority.Background, (_, _) => Capture());
        _timer.Start();
    }

    private void Capture()
    {
        if (_pending > 6) return;   // encoder is behind; skip a frame rather than pile up
        var size = new PixelSize((int)(_window.Bounds.Width * _scale), (int)(_window.Bounds.Height * _scale));
        if (size.Width <= 0) return;
        var rtb = new RenderTargetBitmap(size, new Vector(96 * _scale, 96 * _scale));
        rtb.Render(_window);
        var path = Path.Combine(_dir, "frames", $"f_{_clock.ElapsedMilliseconds:D8}.png");
        System.Threading.Interlocked.Increment(ref _pending);
        Task.Run(() =>
        {
            try { using var fs = File.Create(path); rtb.Save(fs); } finally { rtb.Dispose(); System.Threading.Interlocked.Decrement(ref _pending); }
        });
    }

    public void Mark(string evt) =>
        File.AppendAllText(Path.Combine(_dir, "events.txt"), $"{_clock.ElapsedMilliseconds} {evt}\n");

    public async Task StopAsync()
    {
        _timer?.Stop();
        while (_pending > 0) await Task.Delay(50);
    }
}
