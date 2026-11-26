using CallBridge.Core;

namespace CallBridge.Maui;

/// <summary>
/// Routes <c>callbridge://</c> and App Links into Shell navigation. Links that arrive before the shell exists
/// (cold start from a link) are kept and applied once it has loaded.
/// </summary>
public static class DeepLinks
{
    private static string? _pending;

    public static void Handle(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        _pending = url;
        TryApply();
    }

    public static void TryApply()
    {
        if (_pending is null || Shell.Current is null) return;
        var destination = DeepLinkParser.Parse(_pending);
        _pending = null;
        if (destination is null) return;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (destination)
            {
                case Destination.Tab tab:
                    await Shell.Current.GoToAsync("//" + tab.Name);
                    break;
                case Destination.Dial dial:
                    await CallNavigator.CallAsync(dial.E164);
                    break;
                case Destination.Plans plans:
                    await Shell.Current.GoToAsync("plans", new Dictionary<string, object> { ["highlight"] = plans.HighlightProductId ?? "" });
                    break;
                case Destination.Conversation:
                    await Shell.Current.GoToAsync("//messages");
                    break;
            }
        });
    }
}

public static class CallNavigator
{
    public static Task CallAsync(string e164) =>
        Shell.Current.GoToAsync("incall", new Dictionary<string, object> { ["number"] = e164 });
}
