using CallBridge.Core;
using CallBridge.Core.ViewModels;

namespace CallBridge.Maui.Pages;

/// <summary>
/// Hosts one call. Feeds the view model a one-second tick and real connectivity changes (Wi-Fi ↔ cellular)
/// from MAUI Essentials, so the shared state machine performs the handover logic on every platform.
/// </summary>
[QueryProperty(nameof(Number), "number")]
public partial class InCallPage : ContentPage
{
    private readonly InCallViewModel _viewModel;
    private IDispatcherTimer? _timer;
    private int _ticks;

    public InCallPage(InCallViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public string Number { get; set; } = "";

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Start(Number, CurrentNetwork() ?? CallNetwork.Wifi);
        Initials.Text = Avatars.Initials(_viewModel.Ui.Title);
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;

        _ticks = 0;
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += async (_, _) =>
        {
            _ticks++;
            // Demo signalling: the far end answers after ~2 s. The SIP stack reports this in the real app.
            if (_ticks == 2) _viewModel.Dispatch(new CallEvent.RemoteAnswered(DateTimeOffset.Now));
            _viewModel.Tick();
            if (_viewModel.Ui.Ended)
            {
                _timer?.Stop();
                await Task.Delay(1200);
                await Shell.Current.GoToAsync("..");
            }
        };
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _timer?.Stop();
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() => _viewModel.Dispatch(new CallEvent.NetworkChanged(CurrentNetwork(), DateTimeOffset.Now)));

    private static CallNetwork? CurrentNetwork()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet) return null;
        return Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.Cellular) &&
               !Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi)
            ? CallNetwork.MobileData
            : CallNetwork.Wifi;
    }
}
