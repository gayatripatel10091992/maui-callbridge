using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        PlatformLabel.Text = $".NET MAUI · {DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}";
    }

    private async void OnPlansClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("plans");
}
