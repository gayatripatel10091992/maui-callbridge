using CallBridge.Core;
using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

[QueryProperty(nameof(Highlight), "highlight")]
public partial class PlansPage : ContentPage
{
    private readonly PlansViewModel _viewModel;

    public PlansPage(PlansViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string Highlight
    {
        get => _viewModel.Highlight ?? "";
        set => _viewModel.Highlight = string.IsNullOrEmpty(value) ? null : value;
    }

    private async void OnSelectClicked(object? sender, EventArgs e)
    {
        if (sender is BindableObject { BindingContext: PlanCard card }) await _viewModel.SelectCommand.ExecuteAsync(card);
    }
}
