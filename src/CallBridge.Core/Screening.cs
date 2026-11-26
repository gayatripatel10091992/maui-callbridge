using System.Text.RegularExpressions;

namespace CallBridge.Core;

public enum Verdict { Ring, SendToVoicemail, Block }

public sealed record ScreeningDecision(Verdict Verdict, string? Label);

/// <summary>Decides what happens to an incoming call before it rings. The user's block list and contacts beat the spam score.</summary>
public static class CallScreener
{
    public static ScreeningDecision Decide(string e164, bool isContact, int? spamScore, CallSettings settings)
    {
        if (settings.BlockedNumbers.Any(p => Matches(p, e164))) return new(Verdict.Block, "Blocked");
        if (isContact) return new(Verdict.Ring, null);
        if (spamScore >= 95) return new(Verdict.Block, "Spam");
        if (spamScore >= settings.SpamThreshold) return new(Verdict.SendToVoicemail, "Likely spam");
        if (settings.ScreenUnknownCallers) return new(Verdict.SendToVoicemail, "Unknown caller");
        return new(Verdict.Ring, null);
    }

    /// <summary>Exact E.164 numbers, or a prefix ending in <c>*</c> such as <c>+1900*</c>.</summary>
    public static bool Matches(string pattern, string e164) =>
        pattern.EndsWith('*') ? e164.StartsWith(pattern[..^1], StringComparison.Ordinal) : pattern == e164;
}

/// <summary>Reads the one-time code from an SMS such as <c>&lt;#&gt; Your CallBridge code is 482913</c>.</summary>
public static partial class OtpParser
{
    public static string? Parse(string message, int length = 6)
    {
        var code = new Regex($@"(?<!\d)\d{{{length}}}(?!\d)");
        var keyword = Keyword().Match(message);
        if (keyword.Success)
        {
            var after = code.Match(message, keyword.Index + keyword.Length);
            if (after.Success) return after.Value;
        }
        var any = code.Match(message);
        return any.Success ? any.Value : null;
    }

    [GeneratedRegex(@"\b(code|otp|pin)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Keyword();
}
