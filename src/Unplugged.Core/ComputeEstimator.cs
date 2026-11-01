namespace Unplugged.Core;

/// <summary>
/// Honest, approximate count of the arithmetic the processor does.
/// Rule of thumb for transformer models: about 2 calculations (one multiply + one add)
/// per active parameter for every piece of text generated.
/// </summary>
public static class ComputeEstimator
{
    /// <summary>
    /// Gemma "E" models (E2B, E4B) use per-layer embeddings: the file holds more parameters
    /// than are actively computed per token. Use the published "effective" size for them.
    /// </summary>
    public static double EffectiveParameters(string modelName, long fileParameterCount)
    {
        var n = modelName.ToUpperInvariant();
        if (n.Contains("E2B")) return 2.0e9;
        if (n.Contains("E4B")) return 4.0e9;
        return fileParameterCount;
    }

    public static double Calculations(double effectiveParameters, int tokens) => 2.0 * effectiveParameters * tokens;

    /// <summary>"about 230 billion" style wording.</summary>
    public static string Describe(double calculations)
    {
        if (calculations >= 1e12) return $"about {calculations / 1e12:0.#} trillion";
        if (calculations >= 1e9) return $"about {Math.Round(calculations / 1e9):N0} billion";
        return $"about {Math.Round(calculations / 1e6):N0} million";
    }
}
