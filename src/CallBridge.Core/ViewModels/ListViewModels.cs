using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CallBridge.Core.ViewModels;

public sealed record ThreadRowUi(string Id, string Title, string Initials, string Preview, string TimeLabel, int Unread,
    bool IsGroup, bool HasAttachment, string LineLabel, string LineColor)
{
    public bool HasUnread => Unread > 0;
    public string PreviewText => HasAttachment ? "📷 " + Preview : Preview;
}

public partial class MessagesViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;

    public MessagesViewModel(CallBridgeBackend backend)
    {
        _backend = backend;
        _backend.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<ThreadRowUi> Threads { get; } = new();

    public void Refresh()
    {
        Threads.Clear();
        var lines = _backend.Lines.ToDictionary(l => l.Id);
        foreach (var t in _backend.Threads.OrderByDescending(t => t.LastAt))
        {
            lines.TryGetValue(t.LineId, out var line);
            Threads.Add(new ThreadRowUi(t.Id, t.Title, t.IsGroup ? "👥" : Avatars.Initials(t.Title), t.LastMessage,
                TimeLabels.Relative(t.LastAt, _backend.Now), t.UnreadCount, t.IsGroup, t.LastHasAttachment,
                line?.Label ?? "", Avatars.LineColors[(line?.ColorIndex ?? 0) % Avatars.LineColors.Length]));
        }
    }
}

public sealed record VoicemailRowUi(long Id, string Title, string Number, string Initials, string Meta, string Transcript,
    bool HasTranscript, bool IsNew, string LineLabel);

public partial class VoicemailViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;

    public VoicemailViewModel(CallBridgeBackend backend)
    {
        _backend = backend;
        _backend.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<VoicemailRowUi> Voicemails { get; } = new();

    public event EventHandler<string>? CallRequested;

    [RelayCommand]
    private void Play(long id) => _backend.MarkVoicemailHeard(id);

    [RelayCommand]
    private void CallBack(string number) => CallRequested?.Invoke(this, number);

    public void Refresh()
    {
        Voicemails.Clear();
        var lines = _backend.Lines.ToDictionary(l => l.Id);
        foreach (var v in _backend.Voicemails.OrderByDescending(v => v.ReceivedAt))
        {
            var title = v.ContactName ?? PhoneNumbers.Display(v.Number);
            Voicemails.Add(new VoicemailRowUi(v.Id, title, v.Number, Avatars.Initials(v.ContactName),
                $"{TimeLabels.Relative(v.ReceivedAt, _backend.Now)} · {Durations.Format(TimeSpan.FromSeconds(v.DurationSeconds))}",
                v.Transcript is { } t ? $"“{t}”" : "Transcription isn't available for this message.",
                v.Transcript is not null, v.IsNew, lines.TryGetValue(v.LineId, out var l) ? l.Label : ""));
        }
    }
}

public partial class PlansViewModel : ObservableObject
{
    private readonly ISubscriptionStore _store;

    public PlansViewModel(ISubscriptionStore store)
    {
        _store = store;
        _store.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<PlanCard> Cards { get; } = new();

    [ObservableProperty]
    private string _entitlementLabel = "";

    [ObservableProperty]
    private string? _message;

    [ObservableProperty]
    private string? _highlight;

    partial void OnHighlightChanged(string? value) => Refresh();

    [RelayCommand]
    private async Task Select(PlanCard card)
    {
        if (!card.ActionEnabled) return;
        var current = EntitlementResolver.Resolve(_store.Purchases);
        var ok = await _store.PurchaseAsync(card.ProductId, card.Change, (current as Entitlement.Active)?.PurchaseToken);
        Message = !ok ? "The store couldn't open checkout on this device."
            : card.Change == PlanChange.DowngradeDeferred ? $"{card.Name} starts at your next renewal."
            : null;
        Refresh();
    }

    public void Refresh()
    {
        var entitlement = EntitlementResolver.Resolve(_store.Purchases);
        EntitlementLabel = PlansPresenter.EntitlementLabel(entitlement);
        Cards.Clear();
        foreach (var card in PlansPresenter.Present(entitlement, _store.Prices, Highlight)) Cards.Add(card);
    }
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;
    private readonly ISubscriptionStore _store;

    public SettingsViewModel(CallBridgeBackend backend, ISubscriptionStore store)
    {
        _backend = backend;
        _store = store;
        _store.Changed += (_, _) => OnPropertyChanged(nameof(PlanLabel));
    }

    public string PlanLabel => PlansPresenter.EntitlementLabel(EntitlementResolver.Resolve(_store.Purchases));

    public IReadOnlyList<string> NumberRows =>
        _backend.Lines.Select(l => $"{l.Label}  ·  {PhoneNumbers.Display(l.Number)}").ToList();

    public bool WifiCallingPreferred
    {
        get => _backend.Settings.WifiCallingPreferred;
        set => Update(s => s with { WifiCallingPreferred = value });
    }

    public bool RecordCalls
    {
        get => _backend.Settings.RecordCalls;
        set => Update(s => s with { RecordCalls = value });
    }

    public bool ForwardingEnabled
    {
        get => _backend.Settings.ForwardingEnabled;
        set => Update(s => s with { ForwardingEnabled = value, ForwardingNumber = s.ForwardingNumber.Length > 0 ? s.ForwardingNumber : "+12125550147" });
    }

    public string ForwardingText => ForwardingEnabled && _backend.Settings.ForwardingNumber.Length > 0
        ? $"To {PhoneNumbers.Display(_backend.Settings.ForwardingNumber)}"
        : "Ring another phone when you're away";

    public bool ScreenUnknownCallers
    {
        get => _backend.Settings.ScreenUnknownCallers;
        set => Update(s => s with { ScreenUnknownCallers = value });
    }

    public string SpamText => $"Calls scoring {_backend.Settings.SpamThreshold}+ go to voicemail, 95+ are blocked";

    public string BlockedText => $"{_backend.Settings.BlockedNumbers.Count} numbers and patterns";

    private void Update(Func<CallSettings, CallSettings> change)
    {
        _backend.UpdateSettings(change);
        OnPropertyChanged(string.Empty);
    }
}
