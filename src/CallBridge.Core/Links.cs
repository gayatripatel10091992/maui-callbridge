namespace CallBridge.Core;

public abstract record Destination
{
    private Destination() { }

    public sealed record Dial(string E164) : Destination;
    public sealed record Conversation(string ThreadId) : Destination;
    public sealed record Plans(string? HighlightProductId) : Destination;

    /// <summary>A top-level tab: calls, messages, voicemail or settings.</summary>
    public sealed record Tab(string Name) : Destination;
}

/// <summary>App links (<c>https://callbridge.example.com/…</c>) and the <c>callbridge://</c> scheme.</summary>
public static class DeepLinkParser
{
    public const string Host = "callbridge.example.com";
    private static readonly string[] Tabs = { "calls", "messages", "voicemail", "settings" };

    public static Destination? Parse(string? url)
    {
        if (url is null || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;
        List<string> segments;
        if (uri.Scheme == "callbridge") segments = new[] { uri.Host }.Concat(PathSegments(uri)).Where(s => s.Length > 0).ToList();
        else if (uri.Scheme == "https" && uri.Host == Host) segments = PathSegments(uri);
        else return null;

        var query = Query(uri);
        var first = segments.FirstOrDefault();
        switch (first)
        {
            case "dial" or "call":
                var raw = segments.ElementAtOrDefault(1) ?? query.GetValueOrDefault("number");
                return raw is not null && PhoneNumbers.ToE164(raw) is { } e164 ? new Destination.Dial(e164) : null;
            case "messages" when segments.Count > 1:
                return IsSafeId(segments[1]) ? new Destination.Conversation(segments[1]) : null;
            case "plans":
                return new Destination.Plans(query.GetValueOrDefault("plan"));
            case { } tab when Tabs.Contains(tab):
                return new Destination.Tab(tab);
            default:
                return null;
        }
    }

    private static bool IsSafeId(string id) =>
        id.Length is > 0 and <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    internal static List<string> PathSegments(Uri uri) =>
        Uri.UnescapeDataString(uri.AbsolutePath).Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

    internal static Dictionary<string, string> Query(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .GroupBy(kv => Uri.UnescapeDataString(kv[0]))
            .ToDictionary(g => g.Key, g => Uri.UnescapeDataString(g.First()[1]));
}

public abstract record HandoffResult
{
    private HandoffResult() { }

    public sealed record NotHandoff() : HandoffResult;
    public sealed record Invalid(string Reason) : HandoffResult;
    public sealed record Valid(string SessionToken, string ProductId, string? Email) : HandoffResult;
}

/// <summary>
/// The web signup funnel ends by navigating to <c>https://callbridge.example.com/app/handoff?session=…&amp;plan=…</c>;
/// the app intercepts it and continues with native store checkout. Only HTTPS on our host is trusted.
/// </summary>
public static class HandoffParser
{
    public static HandoffResult Parse(string url, IReadOnlyCollection<string> knownProducts)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return new HandoffResult.NotHandoff();
        if (uri.Host != DeepLinkParser.Host || !DeepLinkParser.PathSegments(uri).SequenceEqual(new[] { "app", "handoff" }))
            return new HandoffResult.NotHandoff();
        if (uri.Scheme != "https") return new HandoffResult.Invalid("insecure scheme");
        var q = DeepLinkParser.Query(uri);
        if (!q.TryGetValue("session", out var session)) return new HandoffResult.Invalid("missing session");
        if (session.Length is < 16 or > 128 || !session.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            return new HandoffResult.Invalid("malformed session");
        if (!q.TryGetValue("plan", out var plan)) return new HandoffResult.Invalid("missing plan");
        if (!knownProducts.Contains(plan)) return new HandoffResult.Invalid("unknown plan");
        var email = q.GetValueOrDefault("email");
        return new HandoffResult.Valid(session, plan, email is not null && email.Contains('@') ? email : null);
    }
}
