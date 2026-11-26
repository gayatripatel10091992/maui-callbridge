using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

public partial class MessagesPage : ContentPage
{
    public MessagesPage(MessagesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
