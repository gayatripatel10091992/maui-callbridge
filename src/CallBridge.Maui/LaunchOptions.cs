namespace CallBridge.Maui;

/// <summary>
/// Desktop command line: <c>CallBridge.Maui.exe [callbridge://link] [--dark] [--size 440x720]</c>.
/// Windows has no intent or URL hand-off for an unpackaged app, so the same links the phones use arrive as arguments.
/// CI uses this to open each screen for its Windows screenshots.
/// </summary>
public static class LaunchOptions
{
    public static string? Link { get; private set; }
    public static bool Dark { get; private set; }
    public static (double Width, double Height)? Size { get; private set; }

    public static void Parse(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("callbridge://", StringComparison.OrdinalIgnoreCase)) Link = arg;
            else if (arg == "--dark") Dark = true;
            else if (arg == "--size" && i + 1 < args.Count && TryParseSize(args[++i], out var size)) Size = size;
        }
    }

    private static bool TryParseSize(string value, out (double, double) size)
    {
        size = default;
        var parts = value.Split('x');
        if (parts.Length != 2 || !double.TryParse(parts[0], out var w) || !double.TryParse(parts[1], out var h)) return false;
        size = (w, h);
        return true;
    }
}
