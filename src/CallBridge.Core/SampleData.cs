namespace CallBridge.Core;

/// <summary>Deterministic demo data relative to <c>now</c>. All numbers are in the fictional 555-01xx range.</summary>
public static class SampleData
{
    /// <summary>Friday 20 November 2026, 3:30 PM in New York (UTC−5).</summary>
    public static readonly DateTimeOffset FixedNow = new(2026, 11, 20, 15, 30, 0, TimeSpan.FromHours(-5));

    public static readonly IReadOnlyList<PhoneLine> Lines = new[]
    {
        new PhoneLine("work", "Work", "+14155550100", 0),
        new PhoneLine("personal", "Personal", "+16465550111", 1),
    };

    public static readonly IReadOnlyList<Contact> Contacts = new[]
    {
        new Contact("Maya Chen", "+14155550134"),
        new Contact("Daniel Brooks", "+12125550147"),
        new Contact("Priya Raman", "+13125550162"),
        new Contact("Lucas Moreau", "+16175550178"),
        new Contact("Sofia Alvarez", "+13055550189"),
        new Contact("Ethan Walker", "+17185550193"),
    };

    private static TimeSpan Min(double m) => TimeSpan.FromMinutes(m);
    private static TimeSpan Hours(double h) => TimeSpan.FromHours(h);
    private static TimeSpan Days(double d) => TimeSpan.FromDays(d);

    public static List<CallRecord> Calls(DateTimeOffset now)
    {
        var c = Contacts;
        return new List<CallRecord>
        {
            new(1, c[0].Number, c[0].Name, CallDirection.Incoming, now - Min(12), 754, "work", true, HasRecording: true),
            new(2, "+18885550123", null, CallDirection.Missed, now - Min(48), 0, "work", true, LikelySpam: true),
            new(3, c[1].Number, c[1].Name, CallDirection.Outgoing, now - Hours(2), 312, "work", false),
            new(4, c[2].Number, c[2].Name, CallDirection.Missed, now - Hours(3), 0, "personal", true),
            new(5, c[2].Number, c[2].Name, CallDirection.Missed, now - Hours(3) - Min(6), 0, "personal", true),
            new(6, c[3].Number, c[3].Name, CallDirection.Outgoing, now - Days(1) - Hours(2), 1890, "work", true),
            new(7, "+15125550175", null, CallDirection.Incoming, now - Days(1) - Hours(5), 95, "personal", false),
            new(8, c[4].Number, c[4].Name, CallDirection.Incoming, now - Days(2), 421, "personal", true),
            new(9, "+19005550110", null, CallDirection.Blocked, now - Days(3), 0, "work", true, LikelySpam: true),
            new(10, c[5].Number, c[5].Name, CallDirection.Outgoing, now - Days(4), 66, "work", false),
            new(11, c[0].Number, c[0].Name, CallDirection.Outgoing, now - Days(9), 1204, "work", true),
        };
    }

    public static List<MessageThread> Threads(DateTimeOffset now) => new()
    {
        new("t-maya", new[] { Contacts[0] }, "Sounds good, see you at 4!", now - Min(5), 2, "work"),
        new("t-launch", new[] { Contacts[1], Contacts[2], Contacts[3] }, "Priya: Shared the final deck", now - Min(40), 5, "work", LastHasAttachment: true),
        new("t-sofia", new[] { Contacts[4] }, "Thanks for the quick call", now - Hours(6), 0, "personal"),
        new("t-ethan", new[] { Contacts[5] }, "Can you send me the address?", now - Days(1) - Hours(3), 0, "work"),
        new("t-pharmacy", new[] { new Contact("+1 (312) 555-0199", "+13125550199") }, "Your order is ready for pickup", now - Days(3), 0, "personal"),
    };

    public static List<Message> Messages(string threadId, DateTimeOffset now) => threadId == "t-launch"
        ? new List<Message>
        {
            new(1, threadId, false, "Daniel", "Are we still on for the launch review?", now - Hours(2)),
            new(2, threadId, true, null, "Yes, 4pm. I'll dial everyone in from my work line.", now - Hours(2) + Min(3)),
            new(3, threadId, false, "Lucas", "Works for me 👍", now - Hours(1) - Min(20)),
            new(4, threadId, false, "Priya", "Shared the final deck", now - Min(40), AttachmentLabel: "launch-deck.png"),
            new(5, threadId, true, null, "Got it, looks great", now - Min(38), DeliveryStatus.Sent),
        }
        : new List<Message>
        {
            new(1, threadId, false, "Maya", "Hey! Did the new number port over?", now - Hours(3)),
            new(2, threadId, true, null, "Yes, it finished this morning. Calls and texts both work.", now - Hours(3) + Min(2)),
            new(3, threadId, false, "Maya", "Perfect. Can we talk through the contract later?", now - Min(20)),
            new(4, threadId, true, null, "Sure, 4pm?", now - Min(8)),
            new(5, threadId, false, "Maya", "Sounds good, see you at 4!", now - Min(5)),
        };

    public static List<Voicemail> Voicemails(DateTimeOffset now) => new()
    {
        new(1, Contacts[2].Number, Contacts[2].Name, now - Hours(3), 34,
            "Hi, it's Priya. I tried you twice, call me back when you get this. It's about Friday's demo.", true, "personal"),
        new(2, "+15125550175", null, now - Days(1) - Hours(4), 22,
            "Hello, this is a reminder about your appointment tomorrow at 10 AM. Please call to confirm.", true, "personal"),
        new(3, Contacts[5].Number, Contacts[5].Name, now - Days(5), 12, null, false, "work"),
    };
}
