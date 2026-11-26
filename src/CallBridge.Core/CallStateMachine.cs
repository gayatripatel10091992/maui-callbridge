namespace CallBridge.Core;

public enum CallNetwork { Wifi, MobileData }

public enum EndReason { Completed, Declined, Missed, Dropped, Failed }

public abstract record CallState
{
    private CallState() { }

    public sealed record Idle() : CallState;
    public sealed record Dialing(string Number, CallNetwork Network) : CallState;
    public sealed record Ringing(string Number) : CallState;

    public sealed record Active(
        string Number,
        DateTimeOffset StartedAt,
        CallNetwork Network,
        bool Muted = false,
        bool OnHold = false,
        bool Speaker = false,
        int Handovers = 0) : CallState;

    /// <summary>Network lost mid-call; audio pauses while the app re-registers on another network.</summary>
    public sealed record Reconnecting(Active Call, DateTimeOffset Since) : CallState;
    public sealed record Ended(string Number, EndReason Reason, TimeSpan Duration) : CallState;
}

public abstract record CallEvent
{
    private CallEvent() { }

    public sealed record Dial(string Number, CallNetwork Network) : CallEvent;
    public sealed record Incoming(string Number) : CallEvent;
    public sealed record Answer(DateTimeOffset Now, CallNetwork Network) : CallEvent;
    public sealed record RemoteAnswered(DateTimeOffset Now) : CallEvent;
    public sealed record Hangup(DateTimeOffset Now) : CallEvent;
    public sealed record RemoteHangup(DateTimeOffset Now) : CallEvent;
    public sealed record Decline() : CallEvent;
    public sealed record ToggleMute() : CallEvent;
    public sealed record ToggleHold() : CallEvent;
    public sealed record ToggleSpeaker() : CallEvent;

    /// <summary><c>Network == null</c> means connectivity was lost.</summary>
    public sealed record NetworkChanged(CallNetwork? Network, DateTimeOffset Now) : CallEvent;
    public sealed record Tick(DateTimeOffset Now) : CallEvent;
}

/// <summary>
/// Pure reducer for one VoIP call: Wi-Fi ↔ mobile-data handover, reconnect with a 10-second drop timeout, and
/// events that don't apply to the current state are ignored instead of throwing. Same rules as the Kotlin app.
/// </summary>
public static class CallStateMachine
{
    public static readonly TimeSpan ReconnectTimeout = TimeSpan.FromSeconds(10);

    public static CallState Reduce(CallState state, CallEvent e) => state switch
    {
        CallState.Idle => e switch
        {
            CallEvent.Dial d => new CallState.Dialing(d.Number, d.Network),
            CallEvent.Incoming i => new CallState.Ringing(i.Number),
            _ => state,
        },
        CallState.Dialing s => e switch
        {
            CallEvent.RemoteAnswered a => new CallState.Active(s.Number, a.Now, s.Network),
            CallEvent.Hangup => new CallState.Ended(s.Number, EndReason.Completed, TimeSpan.Zero),
            CallEvent.RemoteHangup => new CallState.Ended(s.Number, EndReason.Declined, TimeSpan.Zero),
            CallEvent.NetworkChanged { Network: { } net } => s with { Network = net },
            CallEvent.NetworkChanged => new CallState.Ended(s.Number, EndReason.Failed, TimeSpan.Zero),
            _ => state,
        },
        CallState.Ringing s => e switch
        {
            CallEvent.Answer a => new CallState.Active(s.Number, a.Now, a.Network),
            CallEvent.Decline => new CallState.Ended(s.Number, EndReason.Declined, TimeSpan.Zero),
            CallEvent.RemoteHangup => new CallState.Ended(s.Number, EndReason.Missed, TimeSpan.Zero),
            _ => state,
        },
        CallState.Active s => e switch
        {
            CallEvent.ToggleMute => s with { Muted = !s.Muted },
            CallEvent.ToggleHold => s with { OnHold = !s.OnHold },
            CallEvent.ToggleSpeaker => s with { Speaker = !s.Speaker },
            CallEvent.Hangup h => new CallState.Ended(s.Number, EndReason.Completed, h.Now - s.StartedAt),
            CallEvent.RemoteHangup h => new CallState.Ended(s.Number, EndReason.Completed, h.Now - s.StartedAt),
            CallEvent.NetworkChanged { Network: null } n => new CallState.Reconnecting(s, n.Now),
            CallEvent.NetworkChanged n when n.Network == s.Network => s,
            CallEvent.NetworkChanged n => s with { Network = n.Network!.Value, Handovers = s.Handovers + 1 },
            _ => state,
        },
        CallState.Reconnecting s => e switch
        {
            CallEvent.NetworkChanged { Network: { } net } => s.Call with { Network = net, Handovers = s.Call.Handovers + 1 },
            CallEvent.Tick t when t.Now - s.Since >= ReconnectTimeout =>
                new CallState.Ended(s.Call.Number, EndReason.Dropped, t.Now - s.Call.StartedAt),
            CallEvent.Hangup h => new CallState.Ended(s.Call.Number, EndReason.Completed, h.Now - s.Call.StartedAt),
            CallEvent.RemoteHangup h => new CallState.Ended(s.Call.Number, EndReason.Completed, h.Now - s.Call.StartedAt),
            _ => state,
        },
        CallState.Ended s => e switch
        {
            CallEvent.Dial d => new CallState.Dialing(d.Number, d.Network),
            CallEvent.Incoming i => new CallState.Ringing(i.Number),
            _ => state,
        },
        _ => state,
    };
}
