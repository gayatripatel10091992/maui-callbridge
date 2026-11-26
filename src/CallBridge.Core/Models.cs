namespace CallBridge.Core;

/// <summary>One of the user's virtual numbers. A device can hold several lines.</summary>
public sealed record PhoneLine(string Id, string Label, string Number, int ColorIndex);

public enum CallDirection { Incoming, Outgoing, Missed, Blocked }

public sealed record CallRecord(
    long Id,
    string Number,
    string? ContactName,
    CallDirection Direction,
    DateTimeOffset StartedAt,
    int DurationSeconds,
    string LineId,
    bool ViaWifi,
    bool HasRecording = false,
    bool LikelySpam = false);

public sealed record Contact(string Name, string Number);

public sealed record MessageThread(
    string Id,
    IReadOnlyList<Contact> Participants,
    string LastMessage,
    DateTimeOffset LastAt,
    int UnreadCount,
    string LineId,
    bool LastHasAttachment = false)
{
    public bool IsGroup => Participants.Count > 1;
    // First names for people; numbers without a saved name stay whole ("+1 (312) 555-0199", not "+1").
    public string Title => string.Join(", ", Participants.Select(p => char.IsLetter(p.Name[0]) ? p.Name.Split(' ')[0] : p.Name));
}

public enum DeliveryStatus { Sending, Sent, Delivered, Failed }

public sealed record Message(
    long Id,
    string ThreadId,
    bool FromMe,
    string? SenderName,
    string Body,
    DateTimeOffset SentAt,
    DeliveryStatus Status = DeliveryStatus.Delivered,
    string? AttachmentLabel = null);

public sealed record Voicemail(
    long Id,
    string Number,
    string? ContactName,
    DateTimeOffset ReceivedAt,
    int DurationSeconds,
    string? Transcript,
    bool IsNew,
    string LineId);

public sealed record CallSettings
{
    public bool WifiCallingPreferred { get; init; } = true;
    public bool ForwardingEnabled { get; init; }
    public string ForwardingNumber { get; init; } = "";
    public bool ScreenUnknownCallers { get; init; }
    public int SpamThreshold { get; init; } = 70;
    public bool RecordCalls { get; init; }
    public IReadOnlyList<string> BlockedNumbers { get; init; } = Array.Empty<string>();
}
