namespace CallBridge.Core;

public enum BillingPeriod { Monthly, Annual }

public sealed record Plan(
    string ProductId,
    string Name,
    int Tier,
    BillingPeriod Period,
    int NumbersIncluded,
    IReadOnlyList<string> Features,
    string FallbackPrice);

public static class PlanCatalog
{
    public static readonly IReadOnlyList<Plan> Plans = new[]
    {
        new Plan("callbridge_basic_monthly", "Basic", 1, BillingPeriod.Monthly, 1,
            new[] { "1 phone number", "Unlimited US & Canada calls", "SMS & MMS" }, "$9.99"),
        new Plan("callbridge_pro_monthly", "Pro", 2, BillingPeriod.Monthly, 2,
            new[] { "2 phone numbers", "Voicemail transcription", "Call recording", "Spam screening" }, "$15.99"),
        new Plan("callbridge_pro_annual", "Pro annual", 2, BillingPeriod.Annual, 2,
            new[] { "Everything in Pro", "2 months free" }, "$159.99"),
        new Plan("callbridge_business_monthly", "Business", 3, BillingPeriod.Monthly, 5,
            new[] { "5 phone numbers", "Call forwarding rules", "Group texts up to 50" }, "$29.99"),
    };

    public static IReadOnlyList<string> ProductIds => Plans.Select(p => p.ProductId).ToList();

    public static Plan? Find(string productId) => Plans.FirstOrDefault(p => p.ProductId == productId);
}

public enum PurchaseState { Purchased, Pending, Unspecified }

/// <summary>Store-agnostic purchase (Google Play, App Store or Microsoft Store all map onto this).</summary>
public sealed record PurchaseSnapshot(
    string ProductId,
    string PurchaseToken,
    PurchaseState State,
    bool Acknowledged,
    bool AutoRenewing,
    DateTimeOffset PurchasedAt);

public abstract record Entitlement
{
    private Entitlement() { }

    public sealed record None() : Entitlement;
    public sealed record Pending(Plan Plan) : Entitlement;
    public sealed record Active(Plan Plan, string PurchaseToken, bool AutoRenewing, bool NeedsAcknowledgement) : Entitlement;
}

public enum PlanChange { NewPurchase, UpgradeChargeProrated, DowngradeDeferred, SameTierSwitch, NoChange }

public static class EntitlementResolver
{
    public static Entitlement Resolve(IEnumerable<PurchaseSnapshot> purchases)
    {
        var known = purchases
            .Select(p => (Purchase: p, Plan: PlanCatalog.Find(p.ProductId)))
            .Where(x => x.Plan is not null)
            .ToList();
        // During an upgrade the store can briefly report both purchases: the highest tier wins.
        var active = known
            .Where(x => x.Purchase.State == PurchaseState.Purchased)
            .OrderByDescending(x => x.Plan!.Tier)
            .ThenByDescending(x => x.Purchase.PurchasedAt)
            .FirstOrDefault();
        if (active.Plan is not null)
        {
            return new Entitlement.Active(active.Plan, active.Purchase.PurchaseToken, active.Purchase.AutoRenewing, !active.Purchase.Acknowledged);
        }
        var pending = known.FirstOrDefault(x => x.Purchase.State == PurchaseState.Pending);
        return pending.Plan is not null ? new Entitlement.Pending(pending.Plan) : new Entitlement.None();
    }

    /// <summary>Purchased but unacknowledged tokens. Pending purchases must never be acknowledged.</summary>
    public static IReadOnlyList<string> TokensToAcknowledge(IEnumerable<PurchaseSnapshot> purchases) =>
        purchases.Where(p => p.State == PurchaseState.Purchased && !p.Acknowledged).Select(p => p.PurchaseToken).ToList();

    public static PlanChange ChangeFor(Entitlement current, Plan target)
    {
        if (current is not Entitlement.Active active) return PlanChange.NewPurchase;
        if (active.Plan.ProductId == target.ProductId) return PlanChange.NoChange;
        if (target.Tier > active.Plan.Tier) return PlanChange.UpgradeChargeProrated;
        if (target.Tier < active.Plan.Tier) return PlanChange.DowngradeDeferred;
        return PlanChange.SameTierSwitch;
    }
}

public sealed record PlanCard(
    string ProductId,
    string Name,
    string Price,
    string PeriodLabel,
    IReadOnlyList<string> Features,
    string ActionLabel,
    bool ActionEnabled,
    bool IsCurrent,
    bool Highlighted,
    PlanChange Change)
{
    public string FeaturesText => string.Join("\n", Features.Select(f => "✓  " + f));
    public bool Emphasised => Highlighted || IsCurrent;
}

public static class PlansPresenter
{
    public static IReadOnlyList<PlanCard> Present(Entitlement entitlement, IReadOnlyDictionary<string, string> prices, string? highlight) =>
        PlanCatalog.Plans.Select(plan =>
        {
            var change = EntitlementResolver.ChangeFor(entitlement, plan);
            var (label, enabled) = entitlement is Entitlement.Pending
                ? ("Purchase pending", false)
                : change switch
                {
                    PlanChange.NewPurchase => ("Subscribe", true),
                    PlanChange.NoChange => ("Current plan", false),
                    PlanChange.UpgradeChargeProrated => ("Upgrade", true),
                    PlanChange.DowngradeDeferred => ("Switch at renewal", true),
                    _ => ("Switch", true),
                };
            return new PlanCard(
                plan.ProductId, plan.Name,
                prices.TryGetValue(plan.ProductId, out var price) ? price : plan.FallbackPrice,
                plan.Period == BillingPeriod.Monthly ? "/ month" : "/ year",
                plan.Features, label, enabled, change == PlanChange.NoChange, plan.ProductId == highlight, change);
        }).ToList();

    public static string EntitlementLabel(Entitlement entitlement) => entitlement switch
    {
        Entitlement.Pending p => $"Payment pending for {p.Plan.Name}",
        Entitlement.Active { AutoRenewing: true } a => $"{a.Plan.Name} · renews automatically",
        Entitlement.Active a => $"{a.Plan.Name} · cancelled, active until the period ends",
        _ => "No active plan",
    };
}
