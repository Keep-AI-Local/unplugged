using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Unplugged.Core;

namespace Unplugged.App.ViewModels;

/// <summary>Things only the window can do (open a link, open a folder, copy text).</summary>
public interface IPlatformActions
{
    Task OpenUriAsync(string uri);
    Task OpenFolderAsync(string path);
    Task CopyTextAsync(string text);
}

public partial class ChatMessage : ObservableObject
{
    public ChatMessage(bool fromUser, string text) { FromUser = fromUser; Text = text; }
    public bool FromUser { get; }
    public bool FromAi => !FromUser;
    public string Who => FromUser ? "You" : "The AI on your computer";
    [ObservableProperty] public partial string Text { get; set; }
    [ObservableProperty] public partial string Footnote { get; set; } = "";
}

public sealed record CommunityLink(string Name, string Url, string Blurb);
public sealed record OfflineIdea(string Task, string Hardware, bool Heavy);

public partial class MainViewModel : ViewModelBase, IDisposable
{
    public const int StepCount = 8;

    private readonly InferenceService _ai = new();
    private NetworkStatusWatcher? _net;
    private HardwareMonitor? _hw;
    private CancellationTokenSource? _answerCts;
    private Fingerprint? _startFingerprint;
    private readonly Queue<double> _cpuHistory = new(), _gpuHistory = new();
    private const int HistoryLength = 80;

    public IPlatformActions? Platform { get; set; }

    // ---------- loading ----------
    [ObservableProperty] public partial bool IsLoading { get; set; } = true;
    [ObservableProperty] public partial double LoadProgress { get; set; }
    [ObservableProperty] public partial string LoadStatus { get; set; } = "Looking for the AI file on this computer…";
    [ObservableProperty] public partial string? FatalError { get; set; }

    // ---------- tour ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep0), nameof(IsStep1), nameof(IsStep2), nameof(IsStep3), nameof(IsStep4),
        nameof(IsStep5), nameof(IsStep6), nameof(IsStep7), nameof(ShowChat), nameof(StepLabel), nameof(CanGoBack),
        nameof(ShowTourNav), nameof(NextLabel), nameof(ShowSkipOffline), nameof(ChatSuggestions), nameof(NextEnabled))]
    public partial int Step { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowChat), nameof(ShowTourNav), nameof(ShowTour), nameof(ChatSuggestions))]
    public partial bool JustChatMode { get; set; }

    public bool ShowTour => !JustChatMode;
    public bool IsStep0 => Step == 0; public bool IsStep1 => Step == 1; public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3; public bool IsStep4 => Step == 4; public bool IsStep5 => Step == 5;
    public bool IsStep6 => Step == 6; public bool IsStep7 => Step == 7;
    public bool ShowChat => JustChatMode || Step is 1 or 3;
    public bool ShowTourNav => !JustChatMode && Step < StepCount - 1;
    public bool CanGoBack => Step > 0;
    public string StepLabel => $"Step {Step + 1} of {StepCount}";
    public string NextLabel => Step switch
    {
        2 => IsOnline ? "Waiting for you to go offline…" : "I'm offline. Continue",
        _ => "Next",
    };
    public bool ShowSkipOffline => Step == 2 && IsOnline;
    public bool NextEnabled => !(Step == 2 && IsOnline);

    // ---------- network ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline), nameof(NetworkBadge), nameof(NextLabel), nameof(ShowSkipOffline), nameof(NextEnabled))]
    public partial bool IsOnline { get; set; } = true;
    public bool IsOffline => !IsOnline;
    public string NetworkBadge => IsOnline ? "CONNECTED TO THE INTERNET" : "OFFLINE  ·  NO INTERNET";

    // ---------- model / hardware facts ----------
    [ObservableProperty] public partial string ModelName { get; set; } = "";
    [ObservableProperty] public partial string ModelFileName { get; set; } = "";
    [ObservableProperty] public partial string ModelSizeText { get; set; } = "";
    [ObservableProperty] public partial string RunningOnText { get; set; } = "";
    [ObservableProperty] public partial string RunningOnShort { get; set; } = "";
    [ObservableProperty] public partial bool UsingGpu { get; set; }
    [ObservableProperty] public partial string LoadSummary { get; set; } = "";
    [ObservableProperty] public partial string MemoryKind { get; set; } = "memory";
    [ObservableProperty] public partial string TierMessage { get; set; } = "";
    [ObservableProperty] public partial string MediaTierMessage { get; set; } = "";
    [ObservableProperty] public partial string EngineDetails { get; set; } = "";

    // ---------- live meters ----------
    [ObservableProperty] public partial double CpuPercent { get; set; }
    [ObservableProperty] public partial double GpuPercent { get; set; }
    [ObservableProperty] public partial bool HasGpuMeter { get; set; }
    [ObservableProperty] public partial string GpuSource { get; set; } = "";
    [ObservableProperty] public partial IList<Point> CpuGraph { get; set; } = new List<Point>();
    [ObservableProperty] public partial IList<Point> GpuGraph { get; set; } = new List<Point>();
    [ObservableProperty] public partial string SpeedText { get; set; } = "Waiting for your first message";
    [ObservableProperty] public partial string CalculationsText { get; set; } = "";
    [ObservableProperty] public partial string MemoryUseText { get; set; } = "";

    // ---------- fingerprint ----------
    [ObservableProperty] public partial string FingerprintStart { get; set; } = "calculating…";
    [ObservableProperty] public partial string FingerprintPublished { get; set; } = "";
    [ObservableProperty] public partial string FingerprintCheckResult { get; set; } = "";
    [ObservableProperty] public partial bool FingerprintChecked { get; set; }
    [ObservableProperty] public partial bool FingerprintMatched { get; set; }
    [ObservableProperty] public partial double FingerprintProgress { get; set; }
    [ObservableProperty] public partial bool IsFingerprinting { get; set; }

    // ---------- chat ----------
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string InputText { get; set; } = "";
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand), nameof(StopCommand), nameof(UseSuggestionCommand))]
    public partial bool IsGenerating { get; set; }
    [ObservableProperty] public partial string LastPiece { get; set; } = "";

    public IReadOnlyList<string> ChatSuggestions => (JustChatMode, Step) switch
    {
        (false, 1) => ["Write a birthday poem for my mom", "Explain how vaccines work like I'm 10", "Help me write a polite complaint email"],
        (false, 3) => ["Is my internet off?", "Write a short poem about privacy", "What can I cook with eggs, rice and spinach?"],
        _ => ["Help me plan a budget for next month", "Explain the stock market in simple words", "Write a bedtime story about a brave turtle", "Translate 'thank you, friend' into Spanish and French"],
    };

    // ---------- the closing note ----------
    [ObservableProperty] public partial double NoteLine1Opacity { get; set; }
    [ObservableProperty] public partial double NoteLine2Opacity { get; set; }
    [ObservableProperty] public partial double NoteSignatureOpacity { get; set; }
    [ObservableProperty] public partial bool NoteButtonsVisible { get; set; }

    // ---------- OS-specific wording ----------
    public bool IsWindows => OperatingSystem.IsWindows();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GuideFor), nameof(ShowAirplaneButton), nameof(ShowNetworkSettingsButton), nameof(ShowCableBox))]
    public partial OfflineGuide Guide { get; set; } = OfflineGuide.ForThisComputer();
    public bool ShowAirplaneButton => IsWindows && !Guide.IsWired;
    public bool ShowNetworkSettingsButton => IsWindows && Guide.IsWired;
    public bool ShowCableBox => !Guide.IsWired;
    public string GuideTitle => "How to turn off your internet";
    public string GuideFor => $"Instructions for your computer: {Guide.SystemName}";
    public string ReconnectTitle => "First, turn your internet back on";
    public string SystemMonitorHowTo => HardwareInfo.OsName switch
    {
        "Windows" => "Press Ctrl + Shift + Esc to open Task Manager, then click \"Performance\".",
        "macOS" => "Open Activity Monitor (press Cmd + Space, type \"Activity Monitor\"), then look at the CPU tab. For the graphics chip: Window › GPU History.",
        _ => "Open your System Monitor app (or run htop / nvtop in a terminal).",
    };

    public IReadOnlyList<CommunityLink> CommunityLinks { get; } =
    [
        new("Hugging Face", "https://huggingface.co", "The world's library of open AI models. Hundreds of thousands, shared freely by the community. (The AI in this app, Google's Gemma, came from here.)"),
        new("Ollama", "https://ollama.com", "The easiest way to download and run AI models on your own computer."),
        new("LM Studio", "https://lmstudio.ai", "A friendly desktop app: browse models, download one, and chat. Fully offline after download."),
        new("Jan", "https://jan.ai", "An open-source, offline-first chat app that runs on your own computer."),
        new("GPT4All", "https://www.nomic.ai/gpt4all", "A beginner-friendly local chat app that can also read your own documents privately."),
        new("llama.cpp", "https://github.com/ggml-org/llama.cpp", "The open engine that powers most local AI, including this app."),
        new("r/LocalLLaMA", "https://www.reddit.com/r/LocalLLaMA", "The biggest community of people running AI at home. Friendly to beginners."),
        new("ComfyUI", "https://www.comfy.org", "Make images and video on your own graphics card (needs a capable GPU)."),
    ];

    public IReadOnlyList<OfflineIdea> OfflineIdeas { get; } =
    [
        new("Write, rewrite and proofread emails, letters and résumés", "Any computer", false),
        new("Summarize your own PDFs and documents, and ask questions about them", "Any computer · faster with a GPU", false),
        new("Translate text between languages", "Any computer", false),
        new("Ask private questions about health, legal matters, money or relationships", "Any computer", false),
        new("Turn recordings and meetings into text", "Any computer · faster with a GPU", false),
        new("Get help with homework, without a company collecting your kids' data", "Any computer", false),
        new("Keep a private journal or thinking partner", "Any computer", false),
        new("Get coding help", "Graphics card recommended", false),
        new("Read text in photos, and describe images", "Graphics card recommended", false),
        new("Run a voice assistant or smart home that never phones home", "Small home server or GPU", false),
        new("Make images and art", "Graphics card, roughly 8 GB+ of video memory", true),
        new("Edit photos: remove objects, upscale, restore old pictures", "Graphics card recommended", true),
        new("Make short video clips", "Powerful graphics card, roughly 12–24 GB+", true),
        new("All of the above on a plane, in a cabin, on a ship, or during an outage", "No internet, by definition", false),
    ];

    public string ShareUrl => AppInfo.SiteUrl.Replace("https://", "");
    public string MovementLine => $"{AppInfo.MovementName}.  {AppInfo.Hashtag}";
    public string MakerSignature => AppInfo.MakerSignature;

    public MainViewModel()
    {
        if (Avalonia.Controls.Design.IsDesignMode) { IsLoading = false; return; }
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var path = ModelLocator.Find();
            if (path == null)
            {
                FatalError = "The AI model file wasn't found. It should be in a folder called \"models\" next to this app.";
                return;
            }
            ModelFileName = Path.GetFileName(path);
            ModelSizeText = FormatBytes(new FileInfo(path).Length);
            LoadStatus = $"Reading {ModelFileName} ({ModelSizeText}) from your drive into your computer's memory…";

            var progress = new Progress<float>(p => LoadProgress = p * 100);
            await Task.Run(() => _ai.LoadAsync(path, progress));

            var b = _ai.Backend!;
            UsingGpu = b.UsingGpu;
            ModelName = _ai.ModelDisplayName;
            MemoryKind = b.UsingGpu ? (OperatingSystem.IsMacOS() ? "your Mac's unified memory" : "your graphics card's memory") : "your computer's memory (RAM)";
            RunningOnText = b.UsingGpu ? $"Running on your graphics card: {b.DeviceName}" : $"Running on your processor: {b.DeviceName} ({HardwareInfo.CpuThreads} threads)";
            RunningOnShort = b.UsingGpu ? $"GPU · {b.DeviceName}" : $"CPU · {HardwareInfo.CpuThreads} threads";
            LoadSummary = $"Read {ModelFileName} ({ModelSizeText}) from your drive and loaded it into {MemoryKind} in {_ai.LoadDuration.TotalSeconds:0.0} seconds.";
            BuildTierMessages(b);
            EngineDetails = string.Join("\n", InferenceService.EngineLog.Where(l => !l.StartsWith("- ")).TakeLast(120));

            _net = new NetworkStatusWatcher();
            IsOnline = _net.IsOnline;
            _net.StatusChanged += online => Dispatcher.UIThread.Post(() => IsOnline = online);

            _hw = new HardwareMonitor(b);
            HasGpuMeter = _hw.HasGpuReading;
            GpuSource = _hw.GpuSource;
            _hw.Sampled += s => Dispatcher.UIThread.Post(() => OnSample(s));

            IsLoading = false;
            // Developer/filming aid: UNPLUGGED_START_STEP=3 opens directly on step 3 (1-based).
            if (int.TryParse(Environment.GetEnvironmentVariable("UNPLUGGED_START_STEP"), out var start) && start is >= 1 and <= StepCount)
                GoTo(start - 1);
            _ = ComputeStartFingerprintAsync(path);
        }
        catch (Exception ex)
        {
            FatalError = "Something went wrong starting the AI: " + ex.Message;
        }
    }

    private void BuildTierMessages(BackendInfo b)
    {
        var tier = HardwareInfo.Tier(b);
        TierMessage = tier switch
        {
            HardwareTier.CpuOnly => "This is running on your processor alone, with no graphics card helping. It works, and that's the point. A computer with a good graphics card would make it many times faster and could run much bigger, smarter models. Still with no internet at all.",
            HardwareTier.SmallGpu => $"Your graphics card ({b.DeviceName}) is helping. A card with more memory could run much bigger, smarter models. Still with no internet at all.",
            _ => $"Your graphics card ({b.DeviceName}) is doing the work. It could run models several times bigger and smarter than this one. Still with no internet at all.",
        };
        MediaTierMessage = tier switch
        {
            HardwareTier.BigGpu => "Good news: your graphics card can likely make images on its own today, and possibly short video clips. No internet needed.",
            HardwareTier.Gpu => "Good news: your graphics card can likely make images on its own today. Video is harder and may be out of reach for now.",
            _ => "Your computer probably can't make images or video yet. That's the hardest job there is for a computer, and today it needs a strong graphics card. But give it a few years: it's heading for ordinary computers, just like the text AI you just used.",
        };
    }

    private async Task ComputeStartFingerprintAsync(string path)
    {
        IsFingerprinting = true;
        try
        {
            var fp = await Task.Run(() => ModelFingerprint.ComputeAsync(path, new Progress<double>(p => FingerprintProgress = p * 100)));
            _startFingerprint = fp;
            FingerprintStart = fp.Short;
            var match = ModelFingerprint.MatchesPublished(path, fp);
            FingerprintPublished = match switch
            {
                true => "Matches the fingerprint published for this file on Hugging Face. It's the untouched original.",
                false => "Doesn't match the published fingerprint. This copy may have been modified.",
                null => "",
            };
        }
        finally { IsFingerprinting = false; }
    }

    private void OnSample(HardwareSample s)
    {
        CpuPercent = s.AppCpuPercent;
        if (s.GpuPercent is { } g) GpuPercent = g;
        MemoryUseText = $"This app is using {FormatBytes(s.AppMemoryBytes)} of your computer's memory right now.";
        CpuGraph = Push(_cpuHistory, s.AppCpuPercent);
        if (HasGpuMeter) GpuGraph = Push(_gpuHistory, s.GpuPercent ?? 0);
    }

    private static IList<Point> Push(Queue<double> q, double v)
    {
        q.Enqueue(v);
        while (q.Count > HistoryLength) q.Dequeue();
        const double w = 280, h = 54;
        var pts = new List<Point>(q.Count);
        int i = HistoryLength - q.Count;
        foreach (var x in q) pts.Add(new Point(i++ * w / (HistoryLength - 1), h - Math.Clamp(x, 0, 100) / 100 * h));
        return pts;
    }

    // ---------- filming demo (UNPLUGGED_DEMO=1) ----------
    public static bool DemoMode => Environment.GetEnvironmentVariable("UNPLUGGED_DEMO") == "1";

    /// <summary>
    /// Hands-free scene for the videos: waits until the computer is really offline, then types a
    /// prompt and lets the AI answer. <paramref name="mark"/> records timeline events.
    /// </summary>
    public async Task RunDemoAsync(Action<string> mark, string prompt)
    {
        while (IsLoading) await Task.Delay(100);
        JustChatMode = true;
        mark("ready");
        while (IsOnline) await Task.Delay(100);
        mark("offline");
        await Task.Delay(3500);
        mark("typing");
        foreach (var ch in prompt) { InputText += ch; await Task.Delay(45); }
        await Task.Delay(500);
        mark("answer-start");
        await SendCommand.ExecuteAsync(null);
        mark("answer-end");
        await Task.Delay(4500);
        mark("done");
    }

    // ---------- commands: tour ----------
    [RelayCommand]
    private void Next()
    {
        if (Step == 2 && IsOnline) return;
        GoTo(Math.Min(Step + 1, StepCount - 1));
    }

    [RelayCommand] private void SkipOffline() => GoTo(3);
    [RelayCommand] private void Back() => GoTo(Math.Max(Step - 1, 0));

    private void GoTo(int step)
    {
        // Re-check how this computer is connected (Wi-Fi or cable) right before showing the "unplug it" steps.
        if (step == 2 && IsOnline) Guide = OfflineGuide.ForThisComputer();
        Step = step;
        if (step == 7) _ = PlayNoteAsync();
    }

    private async Task PlayNoteAsync()
    {
        NoteLine1Opacity = NoteLine2Opacity = NoteSignatureOpacity = 0;
        NoteButtonsVisible = false;
        await Task.Delay(600); NoteLine1Opacity = 1;
        await Task.Delay(3200); NoteLine2Opacity = 1;
        await Task.Delay(2000); NoteSignatureOpacity = 1;
        await Task.Delay(1500); NoteButtonsVisible = true;
    }

    [RelayCommand] private void StartJustChat() => JustChatMode = true;
    [RelayCommand] private void RestartTour() { JustChatMode = false; GoTo(0); }

    // ---------- commands: chat ----------
    private bool CanSend() => !IsGenerating && !string.IsNullOrWhiteSpace(InputText);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        var text = InputText.Trim();
        InputText = "";
        await AskAsync(text);
    }

    private bool CanUseSuggestion(string? _) => !IsGenerating;

    [RelayCommand(CanExecute = nameof(CanUseSuggestion))]
    private Task UseSuggestion(string? text) => string.IsNullOrWhiteSpace(text) ? Task.CompletedTask : AskAsync(text);

    private async Task AskAsync(string text)
    {
        if (!_ai.IsLoaded || IsGenerating) return;
        Messages.Add(new ChatMessage(true, text));
        var reply = new ChatMessage(false, "");
        Messages.Add(reply);
        IsGenerating = true;
        _answerCts = new CancellationTokenSource();
        var buffer = new System.Text.StringBuilder();
        var stats = await _ai.AskAsync(text, piece =>
        {
            lock (buffer) buffer.Append(piece);
            Dispatcher.UIThread.Post(() =>
            {
                lock (buffer) reply.Text = PlainText(buffer.ToString()).TrimStart();
                LastPiece = piece;
            });
        }, _answerCts.Token);
        IsGenerating = false;
        lock (buffer) reply.Text = PlainText(buffer.ToString()).Trim();
        LastPiece = "";
        var who = UsingGpu ? "your graphics card" : "your processor";
        SpeedText = $"{stats.TokensPerSecond:0} pieces of text per second";
        CalculationsText = $"To write that answer, {who} just did {ComputeEstimator.Describe(stats.ApproxCalculations)} calculations (approximate). Right here, on this computer.";
        reply.Footnote = $"{stats.Tokens} pieces · {stats.Seconds:0.0} s · made by {who}{(IsOnline ? "" : " while OFFLINE")} · never left this computer";
    }

    [RelayCommand(CanExecute = nameof(IsGenerating))]
    private void Stop() => _answerCts?.Cancel();

    [RelayCommand]
    private void ForgetEverything()
    {
        if (IsGenerating) return;
        Messages.Clear();
        _ai.NewConversation();
        SpeedText = "Conversation erased. It only ever existed in this computer's temporary memory.";
        CalculationsText = "";
    }

    // ---------- commands: proofs ----------
    [RelayCommand] private Task OpenProofWebsite() => Platform?.OpenUriAsync("https://www.wikipedia.org") ?? Task.CompletedTask;
    [RelayCommand] private Task OpenAirplaneSettings() => Platform?.OpenUriAsync("ms-settings:network-airplanemode") ?? Task.CompletedTask;
    [RelayCommand] private Task OpenNetworkSettings() => Platform?.OpenUriAsync("ms-settings:network-advancedsettings") ?? Task.CompletedTask;
    [RelayCommand] private Task ShowModelFile() => Platform?.OpenFolderAsync(Path.GetDirectoryName(_ai.ModelPath)!) ?? Task.CompletedTask;
    [RelayCommand] private Task OpenLink(string? url) => url == null || Platform == null ? Task.CompletedTask : Platform.OpenUriAsync(url);
    [RelayCommand] private Task CopyShareLink() => Platform?.CopyTextAsync(AppInfo.SiteUrl) ?? Task.CompletedTask;

    [RelayCommand]
    private async Task CheckFingerprint()
    {
        if (_startFingerprint == null || IsFingerprinting) return;
        IsFingerprinting = true;
        FingerprintCheckResult = "Re-reading the whole AI file and fingerprinting it again…";
        try
        {
            var now = await Task.Run(() => ModelFingerprint.ComputeAsync(_ai.ModelPath, new Progress<double>(p => FingerprintProgress = p * 100)));
            FingerprintMatched = now.Sha256Hex == _startFingerprint.Sha256Hex && now.SizeBytes == _startFingerprint.SizeBytes;
            FingerprintCheckResult = FingerprintMatched
                ? $"Identical: {now.Short}. You've been talking to it, and the file didn't change by a single byte. It didn't write down what you said. It can't: it's only ever read, never written."
                : "The file changed since startup. That should never happen.";
            FingerprintChecked = true;
        }
        finally { IsFingerprinting = false; }
    }

    /// <summary>Small models love Markdown (**bold**, "*   " bullets). Show it as clean plain text.</summary>
    private static string PlainText(string s)
    {
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?m)^(\s*)[\*\-]\s+", "$1•  ");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"(?m)^#{1,6}\s*", "");
        return s.Replace("**", "").Replace("__", "");
    }

    // Decimal units, matching what download pages (e.g. Hugging Face) show.
    private static string FormatBytes(long b) => b >= 1_000_000_000 ? $"{b / 1e9:0.00} GB" : $"{b / 1e6:0} MB";

    public void Dispose()
    {
        _answerCts?.Cancel();
        _net?.Dispose();
        _hw?.Dispose();
        _ai.Dispose();
    }
}
