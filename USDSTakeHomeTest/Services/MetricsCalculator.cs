using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace USDSTakeHomeTest.Services;

public class MetricsCalculator
{
    private static readonly Regex Ws = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex WordRegex = new(@"\b\w+\b", RegexOptions.Compiled);

    // Obligation markers (case-insensitive, whole word / phrase)
    private static readonly Regex Shall = new(@"\bshall\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Must = new(@"\bmust\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex MayNot = new(@"\bmay\s+not\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Prohibited = new(@"\bprohibited\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public sealed record Metrics(int WordCount, double ObligationIntensity, string Sha256Checksum);

    public Metrics Compute(string rawText)
    {
        var normalized = NormalizeText(rawText);

        int wordCount = WordRegex.Matches(normalized).Count;

        // Obligation markers count
        int obligations =
            Shall.Matches(normalized).Count +
            Must.Matches(normalized).Count +
            MayNot.Matches(normalized).Count +
            Prohibited.Matches(normalized).Count;

        // Per 10,000 words (avoid divide by zero)
        double intensity = wordCount <= 0 ? 0.0 : obligations / (wordCount / 10000.0);

        string checksum = Sha256Hex(normalized);

        return new Metrics(wordCount, intensity, checksum);
    }

    public string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Collapse whitespace, trim, keep original casing for readability if desired
        // For checksum stability, we lower-case.
        var collapsed = Ws.Replace(text, " ").Trim();
        return collapsed.ToLowerInvariant();
    }

    private static string Sha256Hex(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
