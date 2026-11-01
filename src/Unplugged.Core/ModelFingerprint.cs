using System.Security.Cryptography;

namespace Unplugged.Core;

/// <summary>A SHA-256 "fingerprint" of the model file: proof that chatting never changes it.</summary>
public sealed record Fingerprint(string Sha256Hex, long SizeBytes, DateTime LastModifiedUtc)
{
    public string Short => $"{Sha256Hex[..8]}…{Sha256Hex[^8..]}";
}

public static class ModelFingerprint
{
    /// <summary>
    /// Published SHA-256 fingerprints (from each file's page on huggingface.co). If the bundled file
    /// matches, it is byte-for-byte the file that was published there.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PublishedHashes = new Dictionary<string, string>
    {
        // Google Gemma 3 1B (QAT, Q4_0), packaged by the llama.cpp project: ggml-org/gemma-3-1b-it-qat-GGUF
        ["gemma-3-1b-it-qat-Q4_0.gguf"] = "ef60e4e91a738c99ae9976b050657dfe68a4007a0ccca121b55ec0c413dccd58",
        // Google Gemma 4 E2B (QAT, Q4_0), published by Google: google/gemma-4-E2B-it-qat-q4_0-gguf
        ["gemma-4-E2B_q4_0-it.gguf"] = "fa401b55b07ee70a54c6dae3903c783a6e65064312529ea57175cb5f8dec6634",
    };

    /// <summary>Reads the file (read-only) and hashes it. Reports progress 0..1.</summary>
    public static async Task<Fingerprint> ComputeAsync(string path, IProgress<double>? progress, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[4 << 20];
        long done = 0;
        int read;
        while ((read = await fs.ReadAsync(buffer, ct)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            done += read;
            progress?.Report((double)done / info.Length);
        }
        return new Fingerprint(Convert.ToHexStringLower(sha.GetHashAndReset()), info.Length, info.LastWriteTimeUtc);
    }

    public static bool? MatchesPublished(string path, Fingerprint fp) =>
        PublishedHashes.TryGetValue(Path.GetFileName(path), out var h) ? string.Equals(h, fp.Sha256Hex, StringComparison.OrdinalIgnoreCase) : null;
}
