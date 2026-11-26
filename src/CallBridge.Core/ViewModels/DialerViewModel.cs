using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CallBridge.Core.ViewModels;

public partial class DialerViewModel : ObservableObject
{
    private readonly CallBridgeBackend _backend;

    public DialerViewModel(CallBridgeBackend backend) => _backend = backend;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Formatted), nameof(SuggestionText), nameof(HasSuggestion), nameof(CanCall), nameof(Dialable))]
    [NotifyCanExecuteChangedFor(nameof(CallCommand))]
    private string _input = "";

    public string Formatted => PhoneNumbers.FormatAsYouType(Input);
    public string? Dialable => PhoneNumbers.ToE164(Input);
    public bool CanCall => Dialable is not null;

    private Contact? Suggestion => _backend.Suggest(new string(Input.Where(char.IsAsciiDigit).ToArray()));
    public bool HasSuggestion => Suggestion is not null;
    public string SuggestionText => Suggestion is { } c ? $"{c.Name} · {PhoneNumbers.Display(c.Number)}" : "";

    public string LineLabel => _backend.Lines.First(l => l.Id == _backend.SelectedLineId).Label;

    public event EventHandler<string>? CallRequested;

    [RelayCommand]
    private void Press(string key)
    {
        if (Input.Length < 16) Input += key;
    }

    [RelayCommand]
    private void Backspace()
    {
        if (Input.Length > 0) Input = Input[..^1];
    }

    [RelayCommand]
    private void Clear() => Input = "";

    [RelayCommand]
    private void SwitchLine()
    {
        var lines = _backend.Lines;
        var i = lines.ToList().FindIndex(l => l.Id == _backend.SelectedLineId);
        _backend.SelectLine(lines[(i + 1) % lines.Count].Id);
        OnPropertyChanged(nameof(LineLabel));
    }

    [RelayCommand(CanExecute = nameof(CanCall))]
    private void Call()
    {
        if (Dialable is { } number) CallRequested?.Invoke(this, number);
    }

    [RelayCommand]
    private void CallSuggestion()
    {
        if (Suggestion is { } c) CallRequested?.Invoke(this, c.Number);
    }
}
