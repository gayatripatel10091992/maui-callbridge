using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CallBridge.Core.ViewModels;

public sealed record InCallUi(string Title, string Subtitle, string Status, string NetworkLabel, bool Muted, bool Speaker,
    bool OnHold, bool Reconnecting, bool Ended, bool CanControl);

public static class InCallPresenter
{
    public static InCallUi Present(CallState state, string? contactName, string number, DateTimeOffset now)
    {
        var title = contactName ?? PhoneNumbers.Display(number);
        var subtitle = contactName is not null ? PhoneNumbers.Display(number) : "Unknown caller";
        static string Net(CallNetwork? n) => n switch
        {
            CallNetwork.Wifi => "Wi-Fi calling · HD voice",
            CallNetwork.MobileData => "Mobile data · HD voice",
            _ => "Looking for a network",
        };
        return state switch
        {
            CallState.Dialing d => new(title, subtitle, "Calling…", Net(d.Network), false, false, false, false, false, false),
            CallState.Ringing => new(title, subtitle, "Incoming call", "CallBridge", false, false, false, false, false, false),
            CallState.Active a => new(title, subtitle, a.OnHold ? "On hold" : Durations.Format(now - a.StartedAt), Net(a.Network),
                a.Muted, a.Speaker, a.OnHold, false, false, true),
            CallState.Reconnecting r => new(title, subtitle, "Reconnecting…", Net(null), r.Call.Muted, r.Call.Speaker, false, true, false, false),
            CallState.Ended e => new(title, subtitle, e.Reason switch
            {
                EndReason.Completed => $"Call ended · {Durations.Format(e.Duration)}",
                EndReason.Declined => "Call declined",
                EndReason.Missed => "Missed call",
                EndReason.Dropped => "Call dropped: no network",
                _ => "Call failed",
            }, "CallBridge", false, false, false, false, true, false),
            _ => new(title, subtitle, "Preparing…", "CallBridge", false, false, false, false, false, false),
        };
    }
}

/// <summary>
/// Drives one outgoing call through <see cref="CallStateMachine"/>. The page feeds it a one-second tick and the
/// platform's network changes; on hang-up the call is written to the call log.
/// </summary>
public partial class InCallViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;
    private CallState _state = new CallState.Idle();
    private string _number = "";
    private string? _contact;

    public InCallViewModel(CallBridgeBackend backend) => _backend = backend;

    [ObservableProperty]
    private InCallUi _ui = new("", "", "", "", false, false, false, false, false, false);

    public CallState State => _state;

    public void Start(string number, CallNetwork network)
    {
        // Accept anything dialable ("4155550134", "1 415 555 0134", "+14155550134") and work with E.164 from here on.
        _number = PhoneNumbers.ToE164(number) ?? number.Trim();
        _contact = _backend.ContactFor(number)?.Name;
        _state = new CallState.Idle();
        Dispatch(new CallEvent.Dial(number, network));
    }

    public void Dispatch(CallEvent e)
    {
        var before = _state;
        _state = CallStateMachine.Reduce(_state, e);
        if (before is not CallState.Ended && _state is CallState.Ended ended) LogCall(before, ended);
        Ui = InCallPresenter.Present(_state, _contact, _number, _backend.Now);
    }

    public void Tick() => Dispatch(new CallEvent.Tick(_backend.Now));

    [RelayCommand] private void ToggleMute() => Dispatch(new CallEvent.ToggleMute());
    [RelayCommand] private void ToggleSpeaker() => Dispatch(new CallEvent.ToggleSpeaker());
    [RelayCommand] private void ToggleHold() => Dispatch(new CallEvent.ToggleHold());
    [RelayCommand] private void HangUp() => Dispatch(new CallEvent.Hangup(_backend.Now));

    private void LogCall(CallState before, CallState.Ended ended)
    {
        var network = before switch
        {
            CallState.Active a => a.Network,
            CallState.Dialing d => d.Network,
            CallState.Reconnecting r => r.Call.Network,
            _ => CallNetwork.Wifi,
        };
        _backend.AddCall(new CallRecord(
            _backend.Calls.Count == 0 ? 1 : _backend.Calls.Max(c => c.Id) + 1,
            _number, _contact, CallDirection.Outgoing, _backend.Now - ended.Duration, (int)ended.Duration.TotalSeconds,
            _backend.SelectedLineId, network == CallNetwork.Wifi));
    }
}
