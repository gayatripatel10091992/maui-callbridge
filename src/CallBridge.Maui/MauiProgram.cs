using CallBridge.Core;
using CallBridge.Core.ViewModels;
using CallBridge.Maui.Pages;

namespace CallBridge.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        Func<DateTimeOffset> now = () => DateTimeOffset.Now;
        builder.Services.AddSingleton(new CallBridgeBackend(now));
        // Demo store on every platform; swap for Google Play Billing, StoreKit 2 or Microsoft Store per platform.
        builder.Services.AddSingleton<ISubscriptionStore>(new DemoSubscriptionStore(now));

        builder.Services.AddSingleton<RecentsViewModel>();
        builder.Services.AddSingleton<MessagesViewModel>();
        builder.Services.AddSingleton<VoicemailViewModel>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddTransient<DialerViewModel>();
        builder.Services.AddTransient<InCallViewModel>();
        builder.Services.AddTransient<PlansViewModel>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<RecentsPage>();
        builder.Services.AddTransient<MessagesPage>();
        builder.Services.AddTransient<VoicemailPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<DialerPage>();
        builder.Services.AddTransient<InCallPage>();
        builder.Services.AddTransient<PlansPage>();

        return builder.Build();
    }
}
