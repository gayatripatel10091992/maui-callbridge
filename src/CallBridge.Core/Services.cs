namespace CallBridge.Core;

/// <summary>
/// In-memory stand-in for the VoIP/SMS backend. View models depend only on this API, so the real HTTP and SIP
/// clients can replace it without touching the UI.
/// </summary>
public sealed class CallBridgeBackend
{
    private readonly Func<DateTimeOffset> _now;
    private readonly Dictionary<string, List<Message>> _messages;

    public CallBridgeBackend(Func<DateTimeOffset> now)
    {
        _now = now;
        var t = now();
        Calls = SampleData.Calls(t);
        Threads = SampleData.Threads(t);
        Voicemails = SampleData.Voicemails(t);
        _messages = Threads.ToDictionary(th => th.Id, th => SampleData.Messages(th.Id, t));
    }

    public event EventHandler? Changed;

    public DateTimeOffset Now => _now();
    public IReadOnlyList<PhoneLine> Lines => SampleData.Lines;
    public IReadOnlyList<Contact> Contacts => SampleData.Contacts;
    public List<CallRecord> Calls { get; }
    public List<MessageThread> Threads { get; }
    public List<Voicemail> Voicemails { get; }
    public CallSettings Settings { get; private set; } = new() { BlockedNumbers = new[] { "+19005550110", "+1900*" } };
    public string SelectedLineId { get; private set; } = "work";

    public Contact? ContactFor(string e164) => Contacts.FirstOrDefault(c => c.Number == e164);

    /// <summary>Best contact match for the digits typed on the keypad.</summary>
    public Contact? Suggest(string digits) =>
        digits.Length < 3 ? null : Contacts.FirstOrDefault(c => new string(c.Number.Where(char.IsAsciiDigit).ToArray()).Contains(digits));

    public IReadOnlyList<Message> MessagesFor(string threadId) => _messages.TryGetValue(threadId, out var list) ? list : Array.Empty<Message>();

    public void SelectLine(string id)
    {
        SelectedLineId = id;
        Raise();
    }

    public void AddCall(CallRecord record)
    {
        Calls.Insert(0, record);
        Raise();
    }

    public void UpdateSettings(Func<CallSettings, CallSettings> change)
    {
        Settings = change(Settings);
        Raise();
    }

    public void MarkVoicemailHeard(long id)
    {
        var i = Voicemails.FindIndex(v => v.Id == id);
        if (i >= 0) Voicemails[i] = Voicemails[i] with { IsNew = false };
        Raise();
    }

    public Message Send(string threadId, string body)
    {
        var now = _now();
        var message = new Message(now.ToUnixTimeMilliseconds(), threadId, true, null, body, now, DeliveryStatus.Sending);
        if (!_messages.TryGetValue(threadId, out var list)) _messages[threadId] = list = new List<Message>();
        list.Add(message);
        var i = Threads.FindIndex(t => t.Id == threadId);
        if (i >= 0) Threads[i] = Threads[i] with { LastMessage = body, LastAt = now, UnreadCount = 0 };
        Raise();
        return message;
    }

    public void UpdateStatus(string threadId, long messageId, DeliveryStatus status)
    {
        if (!_messages.TryGetValue(threadId, out var list)) return;
        var i = list.FindIndex(m => m.Id == messageId);
        if (i >= 0) list[i] = list[i] with { Status = status };
        Raise();
    }

    private void Raise() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>What the app needs from a store (Google Play Billing, StoreKit or the Microsoft Store).</summary>
public interface ISubscriptionStore
{
    IReadOnlyList<PurchaseSnapshot> Purchases { get; }
    IReadOnlyDictionary<string, string> Prices { get; }
    bool Available { get; }
    event EventHandler? Changed;
    Task<bool> PurchaseAsync(string productId, PlanChange change, string? oldPurchaseToken);
}

/// <summary>
/// Sandbox store used by the demo on every platform. It applies the same plan-change rules a real store does:
/// upgrades replace the old subscription now, downgrades are deferred to renewal.
/// </summary>
public sealed class DemoSubscriptionStore : ISubscriptionStore
{
    private readonly Func<DateTimeOffset> _now;
    private readonly List<PurchaseSnapshot> _purchases = new();

    public DemoSubscriptionStore(Func<DateTimeOffset> now, IEnumerable<PurchaseSnapshot>? initial = null)
    {
        _now = now;
        if (initial is not null) _purchases.AddRange(initial);
    }

    public IReadOnlyList<PurchaseSnapshot> Purchases => _purchases;
    public IReadOnlyDictionary<string, string> Prices { get; } = new Dictionary<string, string>();
    public bool Available => true;
    public event EventHandler? Changed;

    public Task<bool> PurchaseAsync(string productId, PlanChange change, string? oldPurchaseToken)
    {
        if (change is PlanChange.NoChange) return Task.FromResult(false);
        if (change is PlanChange.DowngradeDeferred) return Task.FromResult(true); // takes effect at renewal
        _purchases.RemoveAll(p => p.PurchaseToken == oldPurchaseToken);
        _purchases.Add(new PurchaseSnapshot(productId, $"demo-{productId}-{_now().ToUnixTimeSeconds()}",
            PurchaseState.Purchased, Acknowledged: true, AutoRenewing: true, _now()));
        Changed?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(true);
    }
}
