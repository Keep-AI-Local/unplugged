# Unplugged

Part of **Keep AI Local**: [keepailocal.org](https://keepailocal.org) · #KeepAILocal

A tiny desktop app (Windows · macOS · Linux) that proves AI can live entirely on your own computer.
It ships with Google's **Gemma 3 1B** model (~720 MB, the smallest Gemma made for general chat) and walks you through an 8-step tour: chat with it,
turn off your internet yourself, prove it in your browser, and watch the AI keep working.

- No networking code. No telemetry. No accounts. Nothing you type leaves the computer.
- The model file is opened read-only; the app shows its SHA-256 fingerprint before and after chatting.
- Runs on the GPU when available (Vulkan on Windows/Linux, Metal on Apple Silicon), otherwise the CPU.

## Build & run
```bash
build/get-model.sh                      # downloads + verifies the ~720 MB model into models/
dotnet run --project src/Unplugged.App
UNPLUGGED_FORCE_CPU=1 dotnet run --project src/Unplugged.App   # simulate a CPU-only computer
```

## Layout
- `src/Unplugged.Core` – inference (LLamaSharp/llama.cpp), passive network watcher, hardware meters, fingerprint
- `src/Unplugged.App` – Avalonia UI; tour copy lives in `Views/MainWindow.axaml`, edit-me constants in `AppInfo.cs`
- `tools/Unplugged.Probe` – console check: load model, report backend, generate two answers
- `site/` – the keepailocal.org website (static, no trackers)
- `campaign/` – how the videos are made: real footage, recorded by the app itself while the computer is offline

Model: Google Gemma 3 1B (QAT Q4_0) via [ggml-org/gemma-3-1b-it-qat-GGUF](https://huggingface.co/ggml-org/gemma-3-1b-it-qat-GGUF).
Gemma is provided under and subject to the Gemma Terms of Use found at ai.google.dev/gemma/terms (see `models/NOTICE.txt`,
`models/GEMMA_TERMS_OF_USE.txt`, `models/GEMMA_PROHIBITED_USE_POLICY.txt`, which must ship with every build).

## License
Unplugged's source code is MIT-licensed (see `LICENSE`). The AI model is not: Gemma is provided under and subject to the
Gemma Terms of Use found at ai.google.dev/gemma/terms.
