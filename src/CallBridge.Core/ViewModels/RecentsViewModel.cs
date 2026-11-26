using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CallBridge.Core.ViewModels;

/// <summary>Call log. UI-framework free, so it runs under xUnit without a device.</summary>
public partial class RecentsViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;

    public RecentsViewModel(CallBridgeBackend backend)
    {
        _backend = backend;
        _backend.Changed += (_, _) => Refresh();
        Refresh();
    }

    public ObservableCollection<CallRowUi> Rows { get; } = new();

    [ObservableProperty]
    private RecentsFilter _filter = RecentsFilter.All;

    [ObservableProperty]
    private string _lineLabel = "";

    public bool IsAllSelected => Filter == RecentsFilter.All;
    public bool IsMissedSelected => Filter == RecentsFilter.Missed;

    /// <summary>Raised with an E.164 number when the user taps a row's call button.</summary>
    public event EventHandler<string>? CallRequested;

    partial void OnFilterChanged(RecentsFilter value)
    {
        OnPropertyChanged(nameof(IsAllSelected));
        OnPropertyChanged(nameof(IsMissedSelected));
        Refresh();
    }

    [RelayCommand]
    private void SetFilter(string name) => Filter = Enum.Parse<RecentsFilter>(name);

    [RelayCommand]
    private void Call(string number) => CallRequested?.Invoke(this, number);

    public void Refresh()
    {
        Rows.Clear();
        foreach (var row in RecentsPresenter.Present(_backend.Calls, _backend.Lines, Filter, _backend.Now)) Rows.Add(row);
        var line = _backend.Lines.First(l => l.Id == _backend.SelectedLineId);
        LineLabel = $"{line.Label} · {PhoneNumbers.Display(line.Number)}";
    }
}
