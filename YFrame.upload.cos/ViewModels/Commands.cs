using System.Windows.Input;

namespace YFrame.upload.cos.ViewModels;

/// <summary>
/// 同步命令。
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>创建同步命令。</summary>
    /// <param name="execute">执行方法</param>
    /// <param name="canExecute">是否可执行</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>可执行状态变化。</summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>判断是否可执行。</summary>
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <summary>执行命令。</summary>
    public void Execute(object? parameter) => _execute();

    /// <summary>手动刷新命令状态。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// 异步命令，执行期间自动禁用并防止重复点击。
/// </summary>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    /// <summary>创建异步命令。</summary>
    /// <param name="execute">异步执行方法</param>
    /// <param name="canExecute">是否可执行</param>
    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    /// <summary>可执行状态变化。</summary>
    public event EventHandler? CanExecuteChanged;

    /// <summary>判断是否可执行。</summary>
    public bool CanExecute(object? parameter)
        => !_isRunning && (_canExecute?.Invoke() ?? true);

    /// <summary>执行命令。</summary>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _isRunning = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>手动刷新命令状态。</summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
