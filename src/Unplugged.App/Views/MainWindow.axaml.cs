using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Unplugged.App.ViewModels;

namespace Unplugged.App.Views;

public partial class MainWindow : Window, IPlatformActions
{
    public MainWindow()
    {
        InitializeComponent();

        // Keep the newest words in view while the AI writes.
        ChatScroll.PropertyChanged += (_, e) =>
        {
            if (e.Property == ScrollViewer.ExtentProperty) ChatScroll.ScrollToEnd();
        };

        // Enter sends, Shift+Enter makes a new line.
        ChatInput.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && DataContext is MainViewModel vm)
            {
                e.Handled = true;
                if (vm.SendCommand.CanExecute(null)) vm.SendCommand.Execute(null);
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        // "There's more below" hints on every scrolling area (not while filming).
        if (!MainViewModel.DemoMode)
        {
            ScrollHint.Attach(TourScroll, Color.Parse("#0E1014"));
            ScrollHint.Attach(Step4Scroll, Color.Parse("#0E1014"));
            ScrollHint.Attach(PanelScroll, Color.Parse("#161A21"), "More below  ↓");
        }

        // Filming: record this window and run the hands-free demo scene.
        var recordDir = Environment.GetEnvironmentVariable("UNPLUGGED_RECORD_DIR");
        if (MainViewModel.DemoMode && !string.IsNullOrEmpty(recordDir))
        {
            Opened += async (_, _) =>
            {
                var rec = new DemoRecorder(this, recordDir);
                while (DataContext is not MainViewModel { IsLoading: false }) await System.Threading.Tasks.Task.Delay(100);
                await System.Threading.Tasks.Task.Delay(600);
                // Show the live processor / graphics-card meters in the side panel.
                var p = Avalonia.VisualExtensions.TranslatePoint(CpuMeterLabel, new Avalonia.Point(0, 0), (Avalonia.Visual)PanelScroll.Content!);
                if (p is { } pt) PanelScroll.Offset = new Avalonia.Vector(0, Math.Max(0, pt.Y - 8));
                rec.Start();
                var vm = (MainViewModel)DataContext!;
                var prompt = Environment.GetEnvironmentVariable("UNPLUGGED_DEMO_PROMPT") ?? "Write a funny 8-line poem about turning off my Wi-Fi.";
                await vm.RunDemoAsync(rec.Mark, prompt);
                await rec.StopAsync();
                rec.Mark("stopped");
            };
        }

        // Developer aid for screenshots: UNPLUGGED_DEV_SCROLL_END=1 keeps the tour scrolled to the bottom.
        if (Environment.GetEnvironmentVariable("UNPLUGGED_DEV_SCROLL_END") == "1")
            TourScroll.PropertyChanged += (_, e) => { if (e.Property == ScrollViewer.ExtentProperty) TourScroll.ScrollToEnd(); };

        // Each new screen starts at the top.
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainViewModel vm) return;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(MainViewModel.Step) or nameof(MainViewModel.JustChatMode))
                {
                    TourScroll.Offset = default;
                    Step4Scroll.Offset = default;
                }
            };
        };
    }

    public async Task OpenUriAsync(string uri)
    {
        try { await Launcher.LaunchUriAsync(new Uri(uri)); } catch { /* no browser available */ }
    }

    public async Task OpenFolderAsync(string path)
    {
        try { await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path)); } catch { }
    }

    public async Task CopyTextAsync(string text)
    {
        if (Clipboard is { } cb) await cb.SetTextAsync(text);
    }
}
