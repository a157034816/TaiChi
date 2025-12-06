using System;
using System.Windows.Input;

namespace TaiChi.Wpf.NodeEditor.Controls.Helpers;

/// <summary>
/// 命令辅助类
/// </summary>
public static class CommandHelper
{
    /// <summary>
    /// 创建一个简单的命令
    /// </summary>
    /// <param name="execute">执行方法</param>
    /// <param name="canExecute">可执行判断方法</param>
    /// <returns>命令实例</returns>
    public static ICommand CreateCommand(Action execute, Func<bool>? canExecute = null)
    {
        return new RelayCommand(execute, canExecute);
    }

    /// <summary>
    /// 创建一个带参数的命令
    /// </summary>
    /// <param name="execute">执行方法</param>
    /// <param name="canExecute">可执行判断方法</param>
    /// <returns>命令实例</returns>
    public static ICommand CreateCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        return new RelayCommand<T>(execute, canExecute);
    }
}

/// <summary>
/// 简单的命令实现
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="execute">执行方法</param>
    /// <param name="canExecute">可执行判断方法</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 可执行状态变化事件
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// 判断命令是否可执行
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>如果可执行返回true</returns>
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke() ?? true;
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="parameter">参数</param>
    public void Execute(object? parameter)
    {
        _execute();
    }

    /// <summary>
    /// 触发可执行状态重新评估
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// 带参数的命令实现
/// </summary>
/// <typeparam name="T">参数类型</typeparam>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="execute">执行方法</param>
    /// <param name="canExecute">可执行判断方法</param>
    public RelayCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 可执行状态变化事件
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// 判断命令是否可执行
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>如果可执行返回true</returns>
    public bool CanExecute(object? parameter)
    {
        if (parameter is T typedParameter)
        {
            return _canExecute?.Invoke(typedParameter) ?? true;
        }
        
        if (parameter == null && !typeof(T).IsValueType)
        {
            return _canExecute?.Invoke(default(T)!) ?? true;
        }
        
        return false;
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="parameter">参数</param>
    public void Execute(object? parameter)
    {
        if (parameter is T typedParameter)
        {
            _execute(typedParameter);
        }
        else if (parameter == null && !typeof(T).IsValueType)
        {
            _execute(default(T)!);
        }
    }

    /// <summary>
    /// 触发可执行状态重新评估
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

/// <summary>
/// 异步命令接口
/// </summary>
public interface IAsyncCommand : ICommand
{
    /// <summary>
    /// 异步执行命令
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>任务</returns>
    System.Threading.Tasks.Task ExecuteAsync(object? parameter);

    /// <summary>
    /// 是否正在执行
    /// </summary>
    bool IsExecuting { get; }
}

/// <summary>
/// 异步命令实现
/// </summary>
public class AsyncRelayCommand : IAsyncCommand
{
    private readonly Func<System.Threading.Tasks.Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="execute">异步执行方法</param>
    /// <param name="canExecute">可执行判断方法</param>
    public AsyncRelayCommand(Func<System.Threading.Tasks.Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <summary>
    /// 是否正在执行
    /// </summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// 可执行状态变化事件
    /// </summary>
    public event EventHandler? CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }

    /// <summary>
    /// 判断命令是否可执行
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>如果可执行返回true</returns>
    public bool CanExecute(object? parameter)
    {
        return !IsExecuting && (_canExecute?.Invoke() ?? true);
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    /// <param name="parameter">参数</param>
    public async void Execute(object? parameter)
    {
        await ExecuteAsync(parameter);
    }

    /// <summary>
    /// 异步执行命令
    /// </summary>
    /// <param name="parameter">参数</param>
    /// <returns>任务</returns>
    public async System.Threading.Tasks.Task ExecuteAsync(object? parameter)
    {
        if (IsExecuting) return;

        try
        {
            IsExecuting = true;
            await _execute();
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// 触发可执行状态重新评估
    /// </summary>
    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}
