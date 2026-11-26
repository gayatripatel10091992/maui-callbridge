using CallBridge.Maui.Pages;

namespace CallBridge.Maui;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("dialer", typeof(DialerPage));
        Routing.RegisterRoute("incall", typeof(InCallPage));
        Routing.RegisterRoute("plans", typeof(PlansPage));
        Loaded += (_, _) => DeepLinks.TryApply();
    }
}
