namespace CallBridge.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        // Read the command line before the window is created: it can set the theme and window size.
        LaunchOptions.Parse(Environment.GetCommandLineArgs().Skip(1).ToList());
        base.OnLaunched(args);
        DeepLinks.Handle(LaunchOptions.Link);
    }
}
