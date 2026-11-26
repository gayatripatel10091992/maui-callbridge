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
    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (LaunchOptions.Dark) UserAppTheme = AppTheme.Dark;
        var (width, height) = LaunchOptions.Size ?? (440, 880);
        // Width/Height only apply on desktop; phones always fill the screen.
        return new Window(_services.GetRequiredService<AppShell>()) { Title = "CallBridge", Width = width, Height = height, X = 0, Y = 0 };
    }

    /// <summary>iOS and Windows hand over app links here (Android uses the activity's intent filter).</summary>
    protected override void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);
        DeepLinks.Handle(uri.ToString());
    }
}
