namespace CallBridge.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    // Resolve the shell after App.xaml resources are loaded, so StaticResource lookups in pages succeed.
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(_services.GetRequiredService<AppShell>()) { Title = "CallBridge", Width = 440, Height = 880 };

    /// <summary>iOS and Windows hand over app links here (Android uses the activity's intent filter).</summary>
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        DeepLinks.Handle(uri.ToString());
    }
}
