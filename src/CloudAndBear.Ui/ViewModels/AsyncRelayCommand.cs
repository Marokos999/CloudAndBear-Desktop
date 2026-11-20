using System.Windows.Input;

namespace CloudAndBear.Ui.ViewModels;

/// <summary>Komanda za radnje koje traju (nmcli, systemctl). Test može da sačeka kraj preko <see cref="ExecuteAsync"/>.</summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public async void Execute(object? parameter) => await ExecuteAsync();

    public Task ExecuteAsync() => CanExecute(null) ? _execute() : Task.CompletedTask;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
