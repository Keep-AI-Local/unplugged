using Unplugged.Core;

// Model check: loads a model through the app's own InferenceService and runs the tour's prompts.
//   dotnet run -- <path-to.gguf>            (GPU if available)
//   UNPLUGGED_FORCE_CPU=1 dotnet run -- …   (CPU only)
var modelPath = args.Length > 0 ? args[0] : ModelLocator.Find() ?? throw new Exception("no model");
var prompts = args.Length > 1 ? args[1..] : new[]
{
    "Write a birthday poem for my mom",
    "Explain how vaccines work like I'm 10",
    "Help me write a polite complaint email",
    "Is my internet off?",
    "What can I cook with eggs, rice and spinach?",
};

using var ai = new InferenceService();
await ai.LoadAsync(modelPath, null);
Console.WriteLine($"MODEL {Path.GetFileName(modelPath)}  ({new FileInfo(modelPath).Length / 1e6:0} MB)  {ai.ModelDisplayName}");
Console.WriteLine($"BACKEND {(ai.Backend!.UsingGpu ? "GPU " : "CPU ")}{ai.Backend.DeviceName}  load {ai.LoadDuration.TotalSeconds:0.0}s");
foreach (var q in prompts)
{
    var sb = new System.Text.StringBuilder();
    var st = await ai.AskAsync(q, p => sb.Append(p), CancellationToken.None);
    Console.WriteLine($"\n### {q}\n{sb.ToString().Trim()}\n[{st.Tokens} tokens, {st.TokensPerSecond:0} tok/s]");
}
