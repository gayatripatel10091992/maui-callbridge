using CallBridge.Core;
using Xunit;

namespace CallBridge.Core.Tests;

public class PhoneNumbersTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("415", "415")]
    [InlineData("4155", "(415) 5")]
    [InlineData("4155550134", "(415) 555-0134")]
    [InlineData("1", "1")]
    [InlineData("14155550134", "1 (415) 555-0134")]
    [InlineData("+14155550134", "+1 (415) 555-0134")]
    [InlineData("+44 7911 123456", "+447911123456")]
    public void FormatsAsYouType(string input, string expected) => Assert.Equal(expected, PhoneNumbers.FormatAsYouType(input));

    [Fact]
    public void NormalisesToE164OrRejects()
    {
        Assert.Equal("+14155550134", PhoneNumbers.ToE164("(415) 555-0134"));
        Assert.Equal("+14155550134", PhoneNumbers.ToE164("1 415 555 0134"));
        Assert.Equal("+447911123456", PhoneNumbers.ToE164("+44 7911 123456"));
        Assert.Null(PhoneNumbers.ToE164("0155550134"));
        Assert.Null(PhoneNumbers.ToE164("555"));
        Assert.Equal("(415) 555-0134", PhoneNumbers.Display("+14155550134"));
    }
}

public class CallStateMachineTests
{
    private const string N = "+14155550134";
    private static readonly DateTimeOffset T0 = SampleData.FixedNow;

    [Fact]
    public void OutgoingCallConnectsAndEndsWithDuration()
    {
        CallState s = new CallState.Idle();
        s = CallStateMachine.Reduce(s, new CallEvent.Dial(N, CallNetwork.Wifi));
        s = CallStateMachine.Reduce(s, new CallEvent.RemoteAnswered(T0));
        s = CallStateMachine.Reduce(s, new CallEvent.ToggleMute());
        Assert.True(((CallState.Active)s).Muted);
        s = CallStateMachine.Reduce(s, new CallEvent.Hangup(T0.AddSeconds(60)));
        Assert.Equal(new CallState.Ended(N, EndReason.Completed, TimeSpan.FromSeconds(60)), s);
    }

    [Fact]
    public void WifiToMobileDataIsASeamlessHandover()
    {
        var active = new CallState.Active(N, T0, CallNetwork.Wifi);
        var s = CallStateMachine.Reduce(active, new CallEvent.NetworkChanged(CallNetwork.MobileData, T0.AddSeconds(5)));
        Assert.Equal(active with { Network = CallNetwork.MobileData, Handovers = 1 }, s);
    }

    [Fact]
    public void LostNetworkReconnectsOrDropsAfterTimeout()
    {
        var active = new CallState.Active(N, T0, CallNetwork.Wifi);
        var lost = CallStateMachine.Reduce(active, new CallEvent.NetworkChanged(null, T0.AddSeconds(20)));
        Assert.IsType<CallState.Reconnecting>(lost);

        var back = CallStateMachine.Reduce(lost, new CallEvent.NetworkChanged(CallNetwork.MobileData, T0.AddSeconds(23)));
        Assert.Equal(CallNetwork.MobileData, Assert.IsType<CallState.Active>(back).Network);

        Assert.Equal(lost, CallStateMachine.Reduce(lost, new CallEvent.Tick(T0.AddSeconds(29.9))));
        var dropped = CallStateMachine.Reduce(lost, new CallEvent.Tick(T0.AddSeconds(30)));
        Assert.Equal(new CallState.Ended(N, EndReason.Dropped, TimeSpan.FromSeconds(30)), dropped);
    }

    [Fact]
    public void UnansweredIncomingCallIsMissedAndStrayEventsAreIgnored()
    {
        var ringing = CallStateMachine.Reduce(new CallState.Idle(), new CallEvent.Incoming(N));
        Assert.Equal(ringing, CallStateMachine.Reduce(ringing, new CallEvent.ToggleMute()));
        Assert.Equal(EndReason.Missed, Assert.IsType<CallState.Ended>(CallStateMachine.Reduce(ringing, new CallEvent.RemoteHangup(T0))).Reason);
    }
}

public class BillingTests
{
    private static PurchaseSnapshot P(string id, PurchaseState state = PurchaseState.Purchased, bool ack = true, bool renew = true, int t = 0) =>
        new(id, "tok-" + id, state, ack, renew, SampleData.FixedNow.AddMinutes(t));

    [Fact]
    public void NoPurchasesMeansNoAccess() => Assert.IsType<Entitlement.None>(EntitlementResolver.Resolve(Array.Empty<PurchaseSnapshot>()));

    [Fact]
    public void HighestTierWinsDuringAnUpgradeOverlap()
    {
        var e = Assert.IsType<Entitlement.Active>(EntitlementResolver.Resolve(new[]
        {
            P("callbridge_basic_monthly", t: 1), P("callbridge_business_monthly", ack: false, t: 2),
        }));
        Assert.Equal("Business", e.Plan.Name);
        Assert.True(e.NeedsAcknowledgement);
    }

    [Fact]
    public void PendingPurchasesGrantNothingAndAreNeverAcknowledged()
    {
        var purchases = new[] { P("callbridge_pro_monthly", PurchaseState.Pending, ack: false) };
        Assert.IsType<Entitlement.Pending>(EntitlementResolver.Resolve(purchases));
        Assert.Empty(EntitlementResolver.TokensToAcknowledge(purchases));
    }

    [Fact]
    public void PlanCardsFollowTheCurrentEntitlement()
    {
        var pro = EntitlementResolver.Resolve(new[] { P("callbridge_pro_monthly") });
        var cards = PlansPresenter.Present(pro, new Dictionary<string, string> { ["callbridge_pro_monthly"] = "₹1,299" }, null);
        Assert.Equal(new[] { "Switch at renewal", "Current plan", "Switch", "Upgrade" }, cards.Select(c => c.ActionLabel));
        Assert.Equal("₹1,299", cards[1].Price);
        Assert.Equal("Pro · renews automatically", PlansPresenter.EntitlementLabel(pro));
    }

    [Fact]
    public async Task DemoStoreUpgradesNowAndDefersDowngrades()
    {
        var store = new DemoSubscriptionStore(() => SampleData.FixedNow, new[] { P("callbridge_pro_monthly") });
        var vm = new ViewModels.PlansViewModel(store);
        await vm.SelectCommand.ExecuteAsync(vm.Cards.Single(c => c.Name == "Basic"));
        Assert.Equal("Basic starts at your next renewal.", vm.Message);
        await vm.SelectCommand.ExecuteAsync(vm.Cards.Single(c => c.Name == "Business"));
        Assert.Equal("Business · renews automatically", vm.EntitlementLabel);
        Assert.Single(store.Purchases);
    }
}

public class LinksTests
{
    [Fact]
    public void ParsesAppLinksAndCustomScheme()
    {
        Assert.Equal(new Destination.Dial("+14155550134"), DeepLinkParser.Parse("callbridge://dial?number=%28415%29%20555-0134"));
        Assert.Equal(new Destination.Dial("+14155550134"), DeepLinkParser.Parse("https://callbridge.example.com/call/4155550134"));
        Assert.Equal(new Destination.Conversation("t-maya"), DeepLinkParser.Parse("https://callbridge.example.com/messages/t-maya"));
        Assert.Equal(new Destination.Plans("callbridge_pro_monthly"), DeepLinkParser.Parse("https://callbridge.example.com/plans?plan=callbridge_pro_monthly"));
        Assert.Equal(new Destination.Tab("voicemail"), DeepLinkParser.Parse("callbridge://voicemail"));
    }

    [Fact]
    public void RejectsForeignHostsAndUnsafeIds()
    {
        Assert.Null(DeepLinkParser.Parse("https://evil.example.com/messages/t-maya"));
        Assert.Null(DeepLinkParser.Parse("https://callbridge.example.com/messages/..%2Fsecret"));
        Assert.Null(DeepLinkParser.Parse("callbridge://dial?number=12"));
        Assert.Null(DeepLinkParser.Parse("not a url"));
    }

    [Fact]
    public void WebFunnelHandoffIsValidated()
    {
        const string token = "s3ss10n_ABCDEFGHIJKLmnop";
        var ids = PlanCatalog.ProductIds;
        Assert.Equal(new HandoffResult.Valid(token, "callbridge_pro_monthly", "maya@example.com"),
            HandoffParser.Parse($"https://callbridge.example.com/app/handoff?session={token}&plan=callbridge_pro_monthly&email=maya%40example.com", ids));
        Assert.IsType<HandoffResult.NotHandoff>(HandoffParser.Parse("https://callbridge.example.com/signup/step2", ids));
        Assert.IsType<HandoffResult.Invalid>(HandoffParser.Parse($"http://callbridge.example.com/app/handoff?session={token}&plan=callbridge_pro_monthly", ids));
        Assert.IsType<HandoffResult.Invalid>(HandoffParser.Parse("https://callbridge.example.com/app/handoff?session=short&plan=callbridge_pro_monthly", ids));
        Assert.IsType<HandoffResult.Invalid>(HandoffParser.Parse($"https://callbridge.example.com/app/handoff?session={token}&plan=free_forever", ids));
    }
}

public class ScreeningAndOtpTests
{
    private const string N = "+18885550123";

    [Fact]
    public void BlockListBeatsEverythingAndContactsBeatSpamScore()
    {
        Assert.Equal(Verdict.Block, CallScreener.Decide(N, true, 0, new CallSettings { BlockedNumbers = new[] { "+1888*" } }).Verdict);
        Assert.Equal(Verdict.Ring, CallScreener.Decide(N, true, 99, new CallSettings()).Verdict);
    }

    [Fact]
    public void SpamScoreAndUnknownCallerScreening()
    {
        Assert.Equal(Verdict.Block, CallScreener.Decide(N, false, 97, new CallSettings()).Verdict);
        Assert.Equal(new ScreeningDecision(Verdict.SendToVoicemail, "Likely spam"), CallScreener.Decide(N, false, 80, new CallSettings()));
        Assert.Equal(Verdict.Ring, CallScreener.Decide(N, false, null, new CallSettings()).Verdict);
        Assert.Equal(Verdict.SendToVoicemail, CallScreener.Decide(N, false, null, new CallSettings { ScreenUnknownCallers = true }).Verdict);
    }

    [Theory]
    [InlineData("<#> Your CallBridge code is 482913\nFA+9qCX9VSu", "482913")]
    [InlineData("Order 551234 shipped. Your verification code: 120934", "120934")]
    public void ExtractsTheCode(string sms, string code) => Assert.Equal(code, OtpParser.Parse(sms));

    [Fact]
    public void NoCode() => Assert.Null(OtpParser.Parse("No code here"));
}

public class PresentationTests
{
    private static readonly DateTimeOffset Now = SampleData.FixedNow;

    [Fact]
    public void RelativeTimeLabels()
    {
        Assert.Equal("Just now", TimeLabels.Relative(Now.AddSeconds(-10), Now));
        Assert.Equal("12 min ago", TimeLabels.Relative(Now.AddMinutes(-12), Now));
        Assert.Equal("12:30 PM", TimeLabels.Relative(Now.AddHours(-3), Now));
        Assert.Equal("Yesterday", TimeLabels.Relative(Now.AddDays(-1), Now));
        Assert.Equal("Wed", TimeLabels.Relative(Now.AddDays(-2), Now));
        Assert.Equal("Nov 11", TimeLabels.Relative(Now.AddDays(-9), Now));
    }

    [Fact]
    public void Durations_() 
    {
        Assert.Equal("0:05", Durations.Format(TimeSpan.FromSeconds(5)));
        Assert.Equal("12:34", Durations.Format(TimeSpan.FromSeconds(754)));
        Assert.Equal("1:02:05", Durations.Format(TimeSpan.FromSeconds(3725)));
    }

    [Fact]
    public void ConsecutiveMissedCallsFromTheSameCallerCollapse()
    {
        var calls = SampleData.Calls(Now);
        var rows = RecentsPresenter.Present(calls, SampleData.Lines, RecentsFilter.All, Now);
        Assert.Equal(2, rows.Single(r => r.Title == "Priya Raman").Count);
        Assert.Equal(calls.Count - 1, rows.Count);
        var missed = RecentsPresenter.Present(calls, SampleData.Lines, RecentsFilter.Missed, Now);
        Assert.Equal(new[] { "(888) 555-0123", "Priya Raman" }, missed.Select(r => r.Title));
    }
}

public class ViewModelTests
{
    [Fact]
    public void DialerFormatsSuggestsAndRaisesCall()
    {
        var backend = new CallBridgeBackend(() => SampleData.FixedNow);
        var vm = new ViewModels.DialerViewModel(backend);
        string? called = null;
        vm.CallRequested += (_, n) => called = n;
        foreach (var key in "4155550134") vm.PressCommand.Execute(key.ToString());
        Assert.Equal("(415) 555-0134", vm.Formatted);
        Assert.Equal("Maya Chen · (415) 555-0134", vm.SuggestionText);
        vm.CallCommand.Execute(null);
        Assert.Equal("+14155550134", called);
    }

    [Fact]
    public void HangingUpLogsTheCall()
    {
        var now = SampleData.FixedNow;
        var backend = new CallBridgeBackend(() => now);
        var vm = new ViewModels.InCallViewModel(backend);
        vm.Start("+12125550147", CallNetwork.Wifi);
        vm.Dispatch(new CallEvent.RemoteAnswered(now));
        now = now.AddSeconds(95);
        vm.Tick();
        Assert.Equal("1:35", vm.Ui.Status);
        vm.HangUpCommand.Execute(null);
        var logged = backend.Calls[0];
        Assert.Equal("Daniel Brooks", logged.ContactName);
        Assert.Equal(95, logged.DurationSeconds);
    }

    [Fact]
    public void RecentsFilterSwitches()
    {
        var vm = new ViewModels.RecentsViewModel(new CallBridgeBackend(() => SampleData.FixedNow));
        vm.SetFilterCommand.Execute("Missed");
        Assert.True(vm.IsMissedSelected);
        Assert.Equal(2, vm.Rows.Count);
        Assert.Equal("Work · (415) 555-0100", vm.LineLabel);
    }
}
