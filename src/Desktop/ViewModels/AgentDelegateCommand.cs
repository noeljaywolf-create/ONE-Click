using System.Windows.Input;

namespace OneClick.Desktop.ViewModels;

/// <summary>
/// Minimal bindable command for the agent panel. Supports sync and async delegates;
/// async exceptions are routed to an optional error handler.
/// </summary>
public sealed class AgentDelegateCommand : ICommand
{
    private readonly Func<object?, Task>? _executeAsync;
    private readonly Action<object?>? _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly Action<Exception>? _onError;
    private bool _isRunning;

    public AgentDelegateCommand(Action execute, Func<bool>? canExecute = null, Action<Exception>? onError = null)
        : this(_ => execute(), canExecute is null ? null : _ => canExecute(), onError)
    {
    }

    public AgentDelegateCommand(Action<object?> execute, Func<object?, bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
        _onError = onError;
    }

    public AgentDelegateCommand(Func<Task> executeAsync, Func<bool>? canExecute = null, Action<Exception>? onError = null)
        : this(_ => executeAsync(), canExecute is null ? null : _ => canExecute(), onError)
    {
    }

    public AgentDelegateCommand(Func<object?, Task> executeAsync, Func<object?, bool>? canExecute = null, Action<Exception>? onError = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
        _onError = onError;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke(parameter) ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;

        try
        {
            _isRunning = true;
            RaiseCanExecuteChanged();
            if (_executeAsync is not null)
                await _executeAsync(parameter).ConfigureAwait(true);
            else
                _execute!(parameter);
        }
        catch (Exception ex)
        {
            if (_onError is not null) _onError(ex);
            else throw;
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
