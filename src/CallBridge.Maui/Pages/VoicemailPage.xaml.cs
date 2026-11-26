using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

public partial class VoicemailPage : ContentPage
{
    private readonly VoicemailViewModel _viewModel;

    public VoicemailPage(VoicemailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private void OnPlayClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: VoicemailRowUi row }) _viewModel.PlayCommand.Execute(row.Id);
    }

    private async void OnCallBackClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: VoicemailRowUi row }) await CallNavigator.CallAsync(row.Number);
    }
}
