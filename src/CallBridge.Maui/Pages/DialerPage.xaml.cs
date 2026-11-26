using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

public partial class DialerPage : ContentPage
{
    private static readonly (string Digit, string Letters)[] KeyLabels =
    {
        ("1", ""), ("2", "ABC"), ("3", "DEF"), ("4", "GHI"), ("5", "JKL"), ("6", "MNO"),
        ("7", "PQRS"), ("8", "TUV"), ("9", "WXYZ"), ("*", ""), ("0", "+"), ("#", ""),
    };

    public DialerPage(DialerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CallRequested += async (_, number) =>
        {
            await Shell.Current.GoToAsync("..");
            await CallNavigator.CallAsync(number);
        };

        // The 12 keys are built in code: one loop instead of 12 near-identical XAML blocks.
        for (var i = 0; i < KeyLabels.Length; i++)
        {
            var (digit, letters) = KeyLabels[i];
            var key = new Button
            {
                Text = letters.Length > 0 ? $"{digit}\n{letters}" : digit,
                FontSize = 22,
                CornerRadius = 38,
                Padding = 0,
                Command = viewModel.PressCommand,
                CommandParameter = digit,
            };
            key.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#E5E8EF"), Color.FromArgb("#272A31"));
            key.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#111827"), Color.FromArgb("#F3F4F6"));
            Grid.SetRow(key, i / 3);
            Grid.SetColumn(key, i % 3);
            Keys.Children.Add(key);
        }
    }
}
