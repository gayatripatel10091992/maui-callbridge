using System.Globalization;

namespace CallBridge.Core;

/// <summary>Timestamp labels for the call log, messages and voicemail, in the offset of <c>now</c>.</summary>
public static class TimeLabels
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Relative(DateTimeOffset then, DateTimeOffset now)
    {
        var diff = now - then;
        if (diff >= TimeSpan.Zero && diff < TimeSpan.FromMinutes(1)) return "Just now";
        if (diff >= TimeSpan.Zero && diff < TimeSpan.FromHours(1)) return $"{(int)diff.TotalMinutes} min ago";
        var local = then.ToOffset(now.Offset);
        var days = now.Date.Subtract(local.Date).Days;
        return days switch
        {
            0 => local.ToString("h:mm tt", Us),
            1 => "Yesterday",
            >= 2 and <= 6 => local.ToString("ddd", Us),
            _ => local.ToString("MMM d", Us),
        };
    }

    public static string Clock(DateTimeOffset at, DateTimeOffset now) => at.ToOffset(now.Offset).ToString("h:mm tt", Us);
}

public static class Durations
{
    /// <summary>65 → <c>1:05</c>, 3725 → <c>1:02:05</c>.</summary>
    public static string Format(TimeSpan span)
    {
        var s = span < TimeSpan.Zero ? TimeSpan.Zero : span;
        return s.TotalHours >= 1 ? $"{(int)s.TotalHours}:{s.Minutes:00}:{s.Seconds:00}" : $"{(int)s.TotalMinutes}:{s.Seconds:00}";
    }
}

public enum RecentsFilter { All, Missed }

/// <summary>One call-log row; consecutive calls from the same number on the same day collapse into one.</summary>
public sealed record CallRowUi(
    long Key,
    string Title,
    string Subtitle,
    string Number,
    CallDirection Direction,
    int Count,
    string TimeLabel,
    string LineLabel,
    string LineColor,
    bool ViaWifi,
    bool LikelySpam)
{
    public string DisplayTitle => Count > 1 ? $"{Title} ({Count})" : Title;
    public bool IsMissed => Direction == CallDirection.Missed;
    public string DirectionGlyph => Direction switch
    {
        CallDirection.Incoming => "↙",
        CallDirection.Outgoing => "↗",
        CallDirection.Missed => "↙",
        _ => "⊘",
    };
    public string NetworkLabel => ViaWifi ? "Wi-Fi" : "Mobile";
    public string Initials => Avatars.Initials(LikelySpam ? null : Title);
}

public static class Avatars
{
    public static readonly string[] LineColors = { "#1F5EAD", "#00897B", "#8E24AA", "#EF6C00", "#D81B60" };

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0])) return "#";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2);
        return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0])));
    }
}

public static class RecentsPresenter
{
    public static IReadOnlyList<CallRowUi> Present(
        IEnumerable<CallRecord> records, IReadOnlyList<PhoneLine> lines, RecentsFilter filter, DateTimeOffset now)
    {
        var visible = records
            .OrderByDescending(r => r.StartedAt)
            .Where(r => filter == RecentsFilter.All || r.Direction == CallDirection.Missed)
            .ToList();
        var byId = lines.ToDictionary(l => l.Id);
        return Group(visible, now.Offset).Select(group =>
        {
            var first = group[0];
            byId.TryGetValue(first.LineId, out var line);
            return new CallRowUi(
                first.Id,
                first.ContactName ?? PhoneNumbers.Display(first.Number),
                first.LikelySpam ? $"Likely spam · {PhoneNumbers.Display(first.Number)}"
                    : first.ContactName is not null ? PhoneNumbers.Display(first.Number) : "Unknown",
                first.Number,
                first.Direction,
                group.Count,
                TimeLabels.Relative(first.StartedAt, now),
                line?.Label ?? "",
                Avatars.LineColors[(line?.ColorIndex ?? 0) % Avatars.LineColors.Length],
                first.ViaWifi,
                first.LikelySpam);
        }).ToList();
    }

    public static List<List<CallRecord>> Group(IReadOnlyList<CallRecord> sorted, TimeSpan offset)
    {
        var result = new List<List<CallRecord>>();
        foreach (var record in sorted)
        {
            var last = result.Count > 0 ? result[^1][^1] : null;
            if (last is not null &&
                last.Number == record.Number &&
                (last.Direction == CallDirection.Missed) == (record.Direction == CallDirection.Missed) &&
                last.StartedAt.ToOffset(offset).Date == record.StartedAt.ToOffset(offset).Date)
            {
                result[^1].Add(record);
            }
            else
            {
                result.Add(new List<CallRecord> { record });
            }
        }
        return result;
    }
}
