using CallBridge.Core;
using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

public partial class RecentsPage : ContentPage
{
    public RecentsPage(RecentsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CallRequested += async (_, number) => await CallNavigator.CallAsync(number);
    }

    private async void OnCallClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: CallRowUi row }) await CallNavigator.CallAsync(row.Number);
    }

    private async void OnKeypadClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("dialer");
}
