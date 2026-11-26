namespace CallBridge.Core;

/// <summary>North American Numbering Plan helpers for the dialer and contact rows.</summary>
public static class PhoneNumbers
{
    /// <summary>Formats digits as the user types: <c>4155550</c> → <c>(415) 555-0</c>.</summary>
    public static string FormatAsYouType(string raw)
    {
        var plus = raw.TrimStart().StartsWith('+');
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0) return plus ? "+" : "";
        // International numbers outside +1 stay ungrouped: grouping rules differ per country.
        if (plus && !digits.StartsWith('1')) return "+" + digits;

        var hasTrunk = digits.StartsWith('1');
        var national = hasTrunk ? digits[1..] : digits;
        if (national.Length > 10) return (plus ? "+" : "") + digits;
        var prefix = !hasTrunk ? "" : plus ? "+1 " : "1 ";
        return (prefix + GroupNational(national)).Trim();
    }

    /// <summary>Normalises input to E.164, or null when it can't be dialled.</summary>
    public static string? ToE164(string raw)
    {
        var plus = raw.TrimStart().StartsWith('+');
        var d = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (plus) return d.Length is >= 8 and <= 15 ? "+" + d : null;
        if (d.Length == 10 && d[0] is >= '2' and <= '9') return "+1" + d;
        if (d.Length == 11 && d[0] == '1' && d[1] is >= '2' and <= '9') return "+" + d;
        return null;
    }

    /// <summary><c>+14155550134</c> → <c>(415) 555-0134</c>; other numbers are returned unchanged.</summary>
    public static string Display(string e164) =>
        e164.StartsWith("+1") && e164.Length == 12 ? GroupNational(e164[2..]) : e164;

    private static string GroupNational(string n) => n.Length switch
    {
        <= 3 => n,
        <= 6 => $"({n[..3]}) {n[3..]}",
        _ => $"({n[..3]}) {n[3..6]}-{n[6..]}",
    };
}
