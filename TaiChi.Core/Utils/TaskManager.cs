using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TaiChi.Core.Utils
{
    /// <summary>
    /// 任务管理器，用于创建、管理和取消异步任务
    /// </summary>
    public class TaskManager : IDisposable
    {
        private static readonly Lazy<TaskManager> _instance = new Lazy<TaskManager>(() => new TaskManager());

        /// <summary>
        /// 获取任务管理器的单例实例
        /// </summary>
        public static TaskManager Instance => _instance.Value;

        /// <summary>
        /// 存储所有被管理的任务信息
        /// </summary>
        private readonly ConcurrentDictionary<Guid, ManagedTask> _tasks = new ConcurrentDictionary<Guid, ManagedTask>();
        
        /// <summary>
        /// 是否已释放资源
        /// </summary>
        private bool _disposed;
        
        /// <summary>
        /// 用于同步的对象
        /// </summary>
        private readonly object _syncLock = new object();
        
        /// <summary>
        /// 默认超时时间（毫秒），-1表示无限等待
        /// </summary>
        public int DefaultTimeout { get; set; } = -1;
        
        /// <summary>
        /// 当任务被添加时触发
        /// </summary>
        public event EventHandler<TaskEventArgs>? TaskAdded;
        
        /// <summary>
        /// 当任务被完成时触发（不管成功、失败或取消）
        /// </summary>
        public event EventHandler<TaskEventArgs>? TaskCompleted;
        
        /// <summary>
        /// 当任务执行失败时触发
        /// </summary>
        public event EventHandler<TaskExceptionEventArgs>? TaskFaulted;
        
        /// <summary>
        /// 当任务被取消时触发
        /// </summary>
        public event EventHandler<TaskEventArgs>? TaskCanceled;

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private TaskManager() { }
        
        /// <summary>
        /// 表示任务事件的参数
        /// </summary>
        public class TaskEventArgs : EventArgs
        {
            /// <summary>
            /// 任务ID
            /// </summary>
            public Guid TaskId { get; }
            
            /// <summary>
            /// 任务标签
            /// </summary>
            public string TaskTag { get; }
            
            /// <summary>
            /// 任务描述
            /// </summary>
            public string TaskDescription { get; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public TaskEventArgs(Guid taskId, string taskTag, string taskDescription)
            {
                TaskId = taskId;
                TaskTag = taskTag;
                TaskDescription = taskDescription;
            }
        }
        
        /// <summary>
        /// 表示任务异常事件的参数
        /// </summary>
        public class TaskExceptionEventArgs : TaskEventArgs
        {
            /// <summary>
            /// 异常信息
            /// </summary>
            public Exception Exception { get; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            public TaskExceptionEventArgs(Guid taskId, string taskTag, string taskDescription, Exception exception)
                : base(taskId, taskTag, taskDescription)
            {
                Exception = exception;
            }
        }

        /// <summary>
        /// 表示一个被管理的任务
        /// </summary>
        public class ManagedTask
        {
            /// <summary>
            /// 任务的唯一标识符
            /// </summary>
            public Guid Id { get; }

            /// <summary>
            /// 任务的标签，用于分组和批量操作
            /// </summary>
            public string Tag { get; }

            /// <summary>
            /// 任务的描述信息
            /// </summary>
            public string Description { get; }

            /// <summary>
            /// 任务的创建时间
            /// </summary>
            public DateTime CreationTime { get; }
            
            /// <summary>
            /// 任务的开始执行时间
            /// </summary>
            public DateTime? StartTime { get; private set; }
            
            /// <summary>
            /// 任务的完成时间
            /// </summary>
            public DateTime? CompletionTime { get; private set; }
            
            /// <summary>
            /// 任务的执行时长（毫秒）
            /// </summary>
            public long? ExecutionDuration => CompletionTime.HasValue && StartTime.HasValue
                ? (long?)(CompletionTime.Value - StartTime.Value).TotalMilliseconds
                : null;

            /// <summary>
            /// 任务的取消令牌源
            /// </summary>
            public CancellationTokenSource CancellationTokenSource { get; }
            
            /// <summary>
            /// 任务的取消令牌
            /// </summary>
            public CancellationToken CancellationToken => CancellationTokenSource.Token;

            /// <summary>
            /// 底层的Task对象
            /// </summary>
            public Task Task { get; }
            
            /// <summary>
            /// 任务的重试计数
            /// </summary>
            public int RetryCount { get; private set; }
            
            /// <summary>
            /// 任务的最大重试次数
            /// </summary>
            public int MaxRetries { get; }
            
            /// <summary>
            /// 任务的优先级
            /// </summary>
            public TaskPriority Priority { get; }
            
            /// <summary>
            /// 任务的超时时间（毫秒），-1表示无限等待
            /// </summary>
            public int Timeout { get; }
            
            /// <summary>
            /// 任务是否已超时
            /// </summary>
            public bool IsTimedOut { get; private set; }

            /// <summary>
            /// 任务的当前状态
            /// </summary>
            public TaskStatus Status => Task.Status;

            /// <summary>
            /// 任务是否已完成
            /// </summary>
            public bool IsCompleted => Task.IsCompleted;

            /// <summary>
            /// 任务是否已取消
            /// </summary>
            public bool IsCanceled => Task.IsCanceled;

            /// <summary>
            /// 任务是否出现异常
            /// </summary>
            public bool IsFaulted => Task.IsFaulted;

            /// <summary>
            /// 任务是否正在运行
            /// </summary>
            public bool IsRunning => Task.Status == TaskStatus.Running;
            
            /// <summary>
            /// 任务是否正在等待
            /// </summary>
            public bool IsWaiting => Task.Status == TaskStatus.WaitingToRun || 
                                    Task.Status == TaskStatus.WaitingForActivation || 
                                    Task.Status == TaskStatus.WaitingForChildrenToComplete;
            
            /// <summary>
            /// 任务是否成功完成
            /// </summary>
            public bool IsSuccessfullyCompleted => Task.Status == TaskStatus.RanToCompletion;

            /// <summary>
            /// 任务的异常信息（如果有）
            /// </summary>
            public AggregateException? Exception => Task.Exception;
            
            /// <summary>
            /// 任务优先级枚举
            /// </summary>
            public enum TaskPriority
            {
                Low = 0,
                Normal = 1,
                High = 2,
                Critical = 3
            }

            /// <summary>
            /// 构造一个被管理的任务
            /// </summary>
            internal ManagedTask(Guid id, string tag, string description, Task task, CancellationTokenSource cts, 
                int timeout = -1, int maxRetries = 0, TaskPriority priority = TaskPriority.Normal)
            {
                Id = id;
                Tag = tag ?? string.Empty;
                Description = description ?? string.Empty;
                CreationTime = DateTime.Now;
                Task = task ?? throw new ArgumentNullException(nameof(task));
                CancellationTokenSource = cts ?? throw new ArgumentNullException(nameof(cts));
                Timeout = timeout;
                MaxRetries = maxRetries;
                Priority = priority;
                RetryCount = 0;
                
                // 注册任务状态变化
                task.ContinueWith(t => 
                {
                    CompletionTime = DateTime.Now;
                    
                    // 处理超时
                    if (Timeout > 0 && ExecutionDuration > Timeout)
                    {
                        IsTimedOut = true;
                    }
                });
                
                // 设置开始时间
                if (task.Status == TaskStatus.Running || task.Status == TaskStatus.WaitingForChildrenToComplete)
                {
                    StartTime = DateTime.Now;
                }
                else
                {
                    task.ContinueWith(t => StartTime = DateTime.Now, 
                        CancellationToken.None, 
                        TaskContinuationOptions.NotOnCanceled | TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously, 
                        TaskScheduler.Default);
                }
            }
            
            /// <summary>
            /// 增加重试计数
            /// </summary>
            internal void IncrementRetryCount()
            {
                RetryCount++;
            }
            
            /// <summary>
            /// 判断是否可以重试任务
            /// </summary>
            internal bool CanRetry => RetryCount < MaxRetries;
            
            /// <summary>
            /// 标记任务为超时
            /// </summary>
            internal void MarkAsTimedOut()
            {
                IsTimedOut = true;
            }
        }

        /// <summary>
        /// 验证任务管理器的状态
        /// </summary>
        /// <exception cref="ObjectDisposedException">如果任务管理器已被释放</exception>
        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TaskManager));
            }
        }

        /// <summary>
        /// 触发任务添加事件
        /// </summary>
        private void OnTaskAdded(ManagedTask task)
        {
            TaskAdded?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 触发任务完成事件
        /// </summary>
        private void OnTaskCompleted(ManagedTask task)
        {
            TaskCompleted?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 触发任务失败事件
        /// </summary>
        private void OnTaskFaulted(ManagedTask task, Exception exception)
        {
            TaskFaulted?.Invoke(this, new TaskExceptionEventArgs(task.Id, task.Tag, task.Description, exception));
        }

        /// <summary>
        /// 触发任务取消事件
        /// </summary>
        private void OnTaskCanceled(ManagedTask task)
        {
            TaskCanceled?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 注册任务完成事件
        /// </summary>
        private void RegisterTaskCompletionCallbacks(ManagedTask task)
        {
            task.Task.ContinueWith(t =>
            {
                try
                {
                    // 移除任务
                    _tasks.TryRemove(task.Id, out _);
                    
                    // 触发完成事件
                    OnTaskCompleted(task);
                    
                    // 根据任务状态触发对应事件
                    if (t.IsFaulted && t.Exception != null)
                    {
                        OnTaskFaulted(task, t.Exception.GetBaseException());
                    }
                    else if (t.IsCanceled)
                    {
                        OnTaskCanceled(task);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"任务完成回调处理异常: {ex}");
                }
            }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            
            // 处理超时
            if (task.Timeout > 0)
            {
                RegisterTimeoutHandler(task);
            }
        }
        
        /// <summary>
        /// 注册任务超时处理
        /// </summary>
        private void RegisterTimeoutHandler(ManagedTask task)
        {
            if (task.Timeout <= 0)
                return;
                
            Task.Delay(task.Timeout).ContinueWith(t =>
            {
                // 检查任务是否仍在运行
                if (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted)
                {
                    try
                    {
                        // 标记为超时
                        task.MarkAsTimedOut();
                        
                        // 取消任务
                        task.CancellationTokenSource.Cancel();
                        
                        Debug.WriteLine($"任务已超时并被取消: {task.Id}, 标签: {task.Tag}, 描述: {task.Description}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"取消超时任务时发生异常: {ex}");
                    }
                }
            }, CancellationToken.None);
        }
        
        /// <summary>
        /// 创建一个新的任务实例
        /// </summary>
        private ManagedTask CreateManagedTask(Guid id, string tag, string description, Task task, CancellationTokenSource cts,
            int timeout, int maxRetries, ManagedTask.TaskPriority priority)
        {
            var managedTask = new ManagedTask(id, tag, description, task, cts, timeout, maxRetries, priority);
            
            // 添加到任务字典
            _tasks[id] = managedTask;
            
            // 触发任务添加事件
            OnTaskAdded(managedTask);
            
            // 注册完成回调
            RegisterTaskCompletionCallbacks(managedTask);
            
            return managedTask;
        }

        /// <summary>
        /// 创建并启动一个新的任务
        /// </summary>
        /// <param name="action">要执行的操作</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="priority">任务优先级</param>
        /// <returns>被管理任务的ID</returns>
        /// <exception cref="ArgumentNullException">action为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public Guid StartNew(Action<CancellationToken> action, string tag = "", string description = "",
            int timeout = -1, int maxRetries = 0, ManagedTask.TaskPriority priority = ManagedTask.TaskPriority.Normal)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            
            EnsureNotDisposed();

            var id = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(() =>
            {
                try
                {
                    action(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 任务被取消，正常退出
                }
                catch (Exception ex) when (maxRetries > 0)
                {
                    // 重试逻辑
                    HandleRetry(id, action, token, ex, 1, maxRetries);
                }
            }, token);

            var managedTask = CreateManagedTask(id, tag, description, task, cts, timeout, maxRetries, priority);
            return id;
        }
        
        /// <summary>
        /// 处理任务重试
        /// </summary>
        private void HandleRetry<T>(Guid taskId, T action, CancellationToken token, Exception exception, 
            int currentRetry, int maxRetries, [CallerMemberName] string callerName = "")
            where T : class
        {
            if (currentRetry > maxRetries || token.IsCancellationRequested || _disposed)
            {
                // 达到最大重试次数或已取消，抛出异常
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("任务已取消", exception, token);
                }
                else
                {
                    throw new AggregateException($"在{maxRetries}次重试后，任务仍然失败", exception);
                }
            }
            
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.IncrementRetryCount();
                
                // 使用指数退避策略
                int delayMs = Math.Min(1000 * (int)Math.Pow(2, currentRetry - 1), 30000);
                
                Debug.WriteLine($"任务重试: {taskId}, 重试次数: {currentRetry}/{maxRetries}, 延迟: {delayMs}ms, 方法: {callerName}");
                
                // 等待一段时间后重试
                Thread.Sleep(delayMs);
                
                try
                {
                    // 根据调用的方法类型进行不同的重试
                    if (callerName == nameof(StartNew) && action is Action<CancellationToken> actionWithToken)
                    {
                        actionWithToken(token);
                    }
                    else if (callerName.StartsWith(nameof(StartNew) + "`") && action is Func<CancellationToken, object> funcWithToken)
                    {
                        funcWithToken(token);
                    }
                    else if (callerName == "StartNewAsync" && action is Func<CancellationToken, Task> asyncFuncWithToken)
                    {
                        asyncFuncWithToken(token).GetAwaiter().GetResult();
                    }
                    else if (callerName.StartsWith("StartNewAsync" + "`") && action is Func<CancellationToken, Task<object>> asyncFuncWithTokenAndResult)
                    {
                        asyncFuncWithTokenAndResult(token).GetAwaiter().GetResult();
                    }
                    else
                    {
                        throw new InvalidOperationException($"未知的调用方法: {callerName}，动作类型: {action.GetType().Name}");
                    }
                }
                catch (Exception ex) when (currentRetry < maxRetries)
                {
                    // 继续重试
                    HandleRetry(taskId, action, token, ex, currentRetry + 1, maxRetries, callerName);
                }
            }
        }

        /// <summary>
        /// 创建并启动一个新的任务，带有返回值
        /// </summary>
        /// <typeparam name="TResult">任务返回值类型</typeparam>
        /// <param name="function">要执行的函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="priority">任务优先级</param>
        /// <returns>被管理任务的ID</returns>
        /// <exception cref="ArgumentNullException">function为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public Guid StartNew<TResult>(Func<CancellationToken, TResult> function, string tag = "", string description = "",
            int timeout = -1, int maxRetries = 0, ManagedTask.TaskPriority priority = ManagedTask.TaskPriority.Normal)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            EnsureNotDisposed();

            var id = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(() =>
            {
                try
                {
                    return function(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 任务被取消，抛出异常以便Task.IsCanceled为true
                    throw;
                }
                catch (Exception ex) when (maxRetries > 0)
                {
                    // 重试逻辑
                    return HandleRetryWithResult(id, function, token, ex, 1, maxRetries);
                }
            }, token);

            var managedTask = CreateManagedTask(id, tag, description, task, cts, timeout, maxRetries, priority);
            return id;
        }
        
        /// <summary>
        /// 处理带结果的任务重试
        /// </summary>
        private TResult HandleRetryWithResult<TResult>(Guid taskId, Func<CancellationToken, TResult> function, 
            CancellationToken token, Exception exception, int currentRetry, int maxRetries)
        {
            if (currentRetry > maxRetries || token.IsCancellationRequested || _disposed)
            {
                // 达到最大重试次数或已取消，抛出异常
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("任务已取消", exception, token);
                }
                else
                {
                    throw new AggregateException($"在{maxRetries}次重试后，任务仍然失败", exception);
                }
            }
            
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.IncrementRetryCount();
                
                // 使用指数退避策略
                int delayMs = Math.Min(1000 * (int)Math.Pow(2, currentRetry - 1), 30000);
                
                Debug.WriteLine($"任务重试: {taskId}, 重试次数: {currentRetry}/{maxRetries}, 延迟: {delayMs}ms");
                
                // 等待一段时间后重试
                Thread.Sleep(delayMs);
                
                try
                {
                    return function(token);
                }
                catch (Exception ex) when (currentRetry < maxRetries)
                {
                    // 继续重试
                    return HandleRetryWithResult(taskId, function, token, ex, currentRetry + 1, maxRetries);
                }
            }
            
            throw new InvalidOperationException("未找到要重试的任务");
        }

        /// <summary>
        /// 获取指定ID的任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <returns>任务对象，如果不存在则返回null</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public ManagedTask? GetTask(Guid id)
        {
            EnsureNotDisposed();
            _tasks.TryGetValue(id, out var task);
            return task;
        }

        /// <summary>
        /// 获取所有当前活动的任务
        /// </summary>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetAllTasks()
        {
            EnsureNotDisposed();
            return _tasks.Values.ToList();
        }

        /// <summary>
        /// 获取指定标签的所有任务
        /// </summary>
        /// <param name="tag">任务标签</param>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetTasksByTag(string tag)
        {
            EnsureNotDisposed();
            
            if (string.IsNullOrEmpty(tag))
                return new List<ManagedTask>();

            return _tasks.Values
                .Where(t => string.Equals(t.Tag, tag, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        
        /// <summary>
        /// 获取指定状态的所有任务
        /// </summary>
        /// <param name="status">任务状态</param>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetTasksByStatus(TaskStatus status)
        {
            EnsureNotDisposed();
            return _tasks.Values.Where(t => t.Status == status).ToList();
        }
        
        /// <summary>
        /// 获取所有运行中的任务
        /// </summary>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetRunningTasks()
        {
            EnsureNotDisposed();
            return _tasks.Values.Where(t => t.IsRunning).ToList();
        }
        
        /// <summary>
        /// 获取所有已完成的任务
        /// </summary>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetCompletedTasks()
        {
            EnsureNotDisposed();
            return _tasks.Values.Where(t => t.IsCompleted).ToList();
        }
        
        /// <summary>
        /// 获取所有失败的任务
        /// </summary>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetFaultedTasks()
        {
            EnsureNotDisposed();
            return _tasks.Values.Where(t => t.IsFaulted).ToList();
        }
        
        /// <summary>
        /// 获取所有取消的任务
        /// </summary>
        /// <returns>任务列表</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public IReadOnlyList<ManagedTask> GetCanceledTasks()
        {
            EnsureNotDisposed();
            return _tasks.Values.Where(t => t.IsCanceled).ToList();
        }

        /// <summary>
        /// 取消指定ID的任务
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="throwOnError">如果为true，则在取消失败时抛出异常</param>
        /// <returns>是否成功取消</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="InvalidOperationException">如果throwOnError为true且取消失败</exception>
        public bool CancelTask(Guid id, bool throwOnError = false)
        {
            EnsureNotDisposed();
            
            if (_tasks.TryGetValue(id, out var task))
            {
                try
                {
                    if (!task.CancellationTokenSource.IsCancellationRequested)
                    {
                        task.CancellationTokenSource.Cancel();
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    if (throwOnError)
                    {
                        throw new InvalidOperationException($"取消任务 {id} 失败", ex);
                    }
                    return false;
                }
            }
            
            if (throwOnError)
            {
                throw new InvalidOperationException($"未找到ID为 {id} 的任务");
            }
            
            return false;
        }

        /// <summary>
        /// 取消指定标签的所有任务
        /// </summary>
        /// <param name="tag">任务标签</param>
        /// <param name="throwOnError">如果为true，则在取消失败时抛出异常</param>
        /// <returns>成功取消的任务数量</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public int CancelTasksByTag(string tag, bool throwOnError = false)
        {
            EnsureNotDisposed();
            
            if (string.IsNullOrEmpty(tag))
                return 0;

            var tasks = GetTasksByTag(tag);
            int canceledCount = 0;
            Exception? lastException = null;

            foreach (var task in tasks)
            {
                try
                {
                    if (!task.CancellationTokenSource.IsCancellationRequested)
                    {
                        task.CancellationTokenSource.Cancel();
                        canceledCount++;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (throwOnError)
                    {
                        throw new InvalidOperationException($"取消标签为 {tag} 的任务失败", ex);
                    }
                }
            }
            
            return canceledCount;
        }

        /// <summary>
        /// 取消所有任务
        /// </summary>
        /// <param name="throwOnError">如果为true，则在取消失败时抛出异常</param>
        /// <returns>成功取消的任务数量</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public int CancelAllTasks(bool throwOnError = false)
        {
            EnsureNotDisposed();
            
            int canceledCount = 0;
            Exception? lastException = null;

            foreach (var task in _tasks.Values)
            {
                try
                {
                    if (!task.CancellationTokenSource.IsCancellationRequested)
                    {
                        task.CancellationTokenSource.Cancel();
                        canceledCount++;
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (throwOnError)
                    {
                        throw new InvalidOperationException($"取消任务 {task.Id} 失败", ex);
                    }
                }
            }
            
            return canceledCount;
        }

        /// <summary>
        /// 等待指定ID的任务完成
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="throwOnTimeout">如果为true，则在超时时抛出异常</param>
        /// <returns>任务是否在超时前完成</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果throwOnTimeout为true且等待超时</exception>
        public bool WaitForTask(Guid id, int timeout = -1, bool throwOnTimeout = false)
        {
            EnsureNotDisposed();
            
            // 如果未指定超时时间，使用默认值
            if (timeout < 0)
            {
                timeout = DefaultTimeout;
            }
            
            if (_tasks.TryGetValue(id, out var task))
            {
                bool completed = task.Task.Wait(timeout);
                
                if (!completed && throwOnTimeout)
                {
                    throw new TimeoutException($"等待任务 {id} 完成超时");
                }
                
                return completed;
            }
            
            return true; // 任务不存在视为已完成
        }

        /// <summary>
        /// 等待指定标签的所有任务完成
        /// </summary>
        /// <param name="tag">任务标签</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="throwOnTimeout">如果为true，则在超时时抛出异常</param>
        /// <returns>所有任务是否在超时前完成</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果throwOnTimeout为true且等待超时</exception>
        public bool WaitForTasksByTag(string tag, int timeout = -1, bool throwOnTimeout = false)
        {
            EnsureNotDisposed();
            
            // 如果未指定超时时间，使用默认值
            if (timeout < 0)
            {
                timeout = DefaultTimeout;
            }
            
            var tasks = GetTasksByTag(tag);
            if (tasks.Count == 0)
                return true;

            var taskArray = tasks.Select(t => t.Task).ToArray();
            bool completed = Task.WaitAll(taskArray, timeout);
            
            if (!completed && throwOnTimeout)
            {
                throw new TimeoutException($"等待标签为 {tag} 的任务完成超时");
            }
            
            return completed;
        }

        /// <summary>
        /// 等待所有任务完成
        /// </summary>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="throwOnTimeout">如果为true，则在超时时抛出异常</param>
        /// <returns>所有任务是否在超时前完成</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果throwOnTimeout为true且等待超时</exception>
        public bool WaitForAllTasks(int timeout = -1, bool throwOnTimeout = false)
        {
            EnsureNotDisposed();
            
            // 如果未指定超时时间，使用默认值
            if (timeout < 0)
            {
                timeout = DefaultTimeout;
            }
            
            var tasks = _tasks.Values.Select(t => t.Task).ToArray();
            if (tasks.Length == 0)
                return true;

            bool completed = Task.WaitAll(tasks, timeout);
            
            if (!completed && throwOnTimeout)
            {
                throw new TimeoutException("等待所有任务完成超时");
            }
            
            return completed;
        }

        /// <summary>
        /// 异步等待指定ID的任务完成
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示等待操作的任务</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public async Task WaitForTaskAsync(Guid id, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            
            if (_tasks.TryGetValue(id, out var task))
            {
                var timeoutTask = task.Timeout > 0 
                    ? Task.Delay(task.Timeout, cancellationToken) 
                    : Task.Delay(-1, cancellationToken);
                
                var completedTask = await Task.WhenAny(task.Task, timeoutTask).ConfigureAwait(false);
                
                if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                {
                    task.MarkAsTimedOut();
                    task.CancellationTokenSource.Cancel();
                    throw new TimeoutException($"等待任务 {id} 完成超时");
                }
                
                // 如果任务失败，则抛出异常
                if (task.IsFaulted && task.Exception != null)
                {
                    throw task.Exception;
                }
                
                await task.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 异步等待指定标签的所有任务完成
        /// </summary>
        /// <param name="tag">任务标签</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示等待操作的任务</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public async Task WaitForTasksByTagAsync(string tag, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            
            var tasks = GetTasksByTag(tag);
            if (tasks.Count > 0)
            {
                var taskArray = tasks.Select(t => t.Task).ToArray();
                
                var timeoutTasks = tasks.Where(t => t.Timeout > 0).ToArray();
                if (timeoutTasks.Length > 0)
                {
                    int minTimeout = timeoutTasks.Min(t => t.Timeout);
                    var timeoutTask = Task.Delay(minTimeout, cancellationToken);
                    
                    var allTasksTask = Task.WhenAll(taskArray);
                    var completedTask = await Task.WhenAny(allTasksTask, timeoutTask).ConfigureAwait(false);
                    
                    if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                    {
                        // 超时了，取消所有未完成的任务
                        foreach (var task in tasks.Where(t => !t.IsCompleted))
                        {
                            task.MarkAsTimedOut();
                            task.CancellationTokenSource.Cancel();
                        }
                        
                        throw new TimeoutException($"等待标签为 {tag} 的任务完成超时");
                    }
                }
                else
                {
                    await Task.WhenAll(taskArray).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 异步等待所有任务完成
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示等待操作的任务</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public async Task WaitForAllTasksAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            
            var tasks = _tasks.Values.ToArray();
            if (tasks.Length > 0)
            {
                var taskArray = tasks.Select(t => t.Task).ToArray();
                
                var timeoutTasks = tasks.Where(t => t.Timeout > 0).ToArray();
                if (timeoutTasks.Length > 0)
                {
                    int minTimeout = timeoutTasks.Min(t => t.Timeout);
                    var timeoutTask = Task.Delay(minTimeout, cancellationToken);
                    
                    var allTasksTask = Task.WhenAll(taskArray);
                    var completedTask = await Task.WhenAny(allTasksTask, timeoutTask).ConfigureAwait(false);
                    
                    if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                    {
                        // 超时了，取消所有未完成的任务
                        foreach (var task in tasks.Where(t => !t.IsCompleted))
                        {
                            task.MarkAsTimedOut();
                            task.CancellationTokenSource.Cancel();
                        }
                        
                        throw new TimeoutException("等待所有任务完成超时");
                    }
                }
                else
                {
                    await Task.WhenAll(taskArray).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 获取任务的结果（适用于有返回值的任务）
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="id">任务ID</param>
        /// <param name="defaultValue">默认值，当任务不存在或类型不匹配时返回</param>
        /// <param name="throwOnError">如果为true，则在任务失败时抛出异常</param>
        /// <returns>任务结果，如果任务不存在或类型不匹配则返回默认值</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="AggregateException">如果throwOnError为true且任务失败</exception>
        public TResult GetTaskResult<TResult>(Guid id, TResult defaultValue = default, bool throwOnError = false)
        {
            EnsureNotDisposed();
            
            if (_tasks.TryGetValue(id, out var managedTask))
            {
                if (managedTask.Task is Task<TResult> resultTask)
                {
                    if (resultTask.IsCompleted && !resultTask.IsFaulted && !resultTask.IsCanceled)
                    {
                        return resultTask.Result;
                    }
                    else if (throwOnError)
                    {
                        if (resultTask.IsCanceled)
                        {
                            throw new OperationCanceledException("任务已取消");
                        }
                        else if (resultTask.IsFaulted && resultTask.Exception != null)
                        {
                            throw resultTask.Exception;
                        }
                        else if (!resultTask.IsCompleted)
                        {
                            throw new InvalidOperationException("任务尚未完成");
                        }
                    }
                }
                else if (throwOnError)
                {
                    throw new InvalidCastException($"任务返回类型不匹配。预期类型为 {typeof(TResult).Name}，实际类型为 {managedTask.Task.GetType().Name}");
                }
            }
            else if (throwOnError)
            {
                throw new InvalidOperationException($"未找到ID为 {id} 的任务");
            }
            
            return defaultValue;
        }

        /// <summary>
        /// 异步获取任务的结果（适用于有返回值的任务）
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="id">任务ID</param>
        /// <param name="defaultValue">默认值，当任务不存在或类型不匹配时返回</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示获取结果操作的任务</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public async Task<TResult> GetTaskResultAsync<TResult>(Guid id, TResult defaultValue = default, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            
            if (_tasks.TryGetValue(id, out var managedTask))
            {
                if (managedTask.Task is Task<TResult> resultTask)
                {
                    try
                    {
                        // 如果任务还未完成，等待完成
                        if (!resultTask.IsCompleted)
                        {
                            var timeoutTask = managedTask.Timeout > 0 
                                ? Task.Delay(managedTask.Timeout, cancellationToken) 
                                : Task.Delay(-1, cancellationToken);
                            
                            var completedTask = await Task.WhenAny(resultTask, timeoutTask).ConfigureAwait(false);
                            
                            if (completedTask == timeoutTask && !cancellationToken.IsCancellationRequested)
                            {
                                managedTask.MarkAsTimedOut();
                                managedTask.CancellationTokenSource.Cancel();
                                throw new TimeoutException($"等待任务 {id} 完成超时");
                            }
                        }
                        
                        return await resultTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // 外部取消，返回默认值
                        return defaultValue;
                    }
                    catch (Exception)
                    {
                        // 任务失败，返回默认值
                        return defaultValue;
                    }
                }
            }
            
            return defaultValue;
        }
        
        /// <summary>
        /// 获取任务状态信息
        /// </summary>
        /// <returns>任务状态摘要</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public string GetTasksStatusSummary()
        {
            EnsureNotDisposed();
            
            var tasks = _tasks.Values.ToArray();
            int totalCount = tasks.Length;
            int runningCount = tasks.Count(t => t.IsRunning);
            int completedCount = tasks.Count(t => t.IsCompleted);
            int faultedCount = tasks.Count(t => t.IsFaulted);
            int canceledCount = tasks.Count(t => t.IsCanceled);
            int waitingCount = tasks.Count(t => t.IsWaiting);
            
            return $"任务总数: {totalCount}, 运行中: {runningCount}, 等待中: {waitingCount}, " +
                   $"已完成: {completedCount}, 失败: {faultedCount}, 已取消: {canceledCount}";
        }
        
        /// <summary>
        /// 清除所有已完成的任务（包括成功、失败和取消的任务）
        /// </summary>
        /// <returns>清除的任务数量</returns>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public int ClearCompletedTasks()
        {
            EnsureNotDisposed();
            
            int removedCount = 0;
            var completedTaskIds = _tasks.Values
                .Where(t => t.IsCompleted)
                .Select(t => t.Id)
                .ToArray();
                
            foreach (var id in completedTaskIds)
            {
                if (_tasks.TryRemove(id, out _))
                {
                    removedCount++;
                }
            }
            
            return removedCount;
        }
        
        /// <summary>
        /// 处理带结果的异步任务重试
        /// </summary>
        private async Task<TResult> HandleRetryWithResultAsync<TResult>(Guid taskId, Func<CancellationToken, Task<TResult>> function, 
            CancellationToken token, Exception exception, int currentRetry, int maxRetries)
        {
            if (currentRetry > maxRetries || token.IsCancellationRequested || _disposed)
            {
                // 达到最大重试次数或已取消，抛出异常
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException("任务已取消", exception, token);
                }
                else
                {
                    throw new AggregateException($"在{maxRetries}次重试后，任务仍然失败", exception);
                }
            }
            
            if (_tasks.TryGetValue(taskId, out var task))
            {
                task.IncrementRetryCount();
                
                // 使用指数退避策略
                int delayMs = Math.Min(1000 * (int)Math.Pow(2, currentRetry - 1), 30000);
                
                Debug.WriteLine($"任务重试: {taskId}, 重试次数: {currentRetry}/{maxRetries}, 延迟: {delayMs}ms");
                
                // 等待一段时间后重试
                await Task.Delay(delayMs, token);
                
                try
                {
                    return await function(token);
                }
                catch (Exception ex) when (currentRetry < maxRetries && !token.IsCancellationRequested)
                {
                    // 继续重试
                    return await HandleRetryWithResultAsync(taskId, function, token, ex, currentRetry + 1, maxRetries);
                }
            }
            
            throw new InvalidOperationException("未找到要重试的任务");
        }

        /// <summary>
        /// 创建并启动一个新的异步任务
        /// </summary>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="priority">任务优先级</param>
        /// <returns>被管理任务的ID</returns>
        /// <exception cref="ArgumentNullException">function为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public Guid StartNewAsync(Func<CancellationToken, Task> function, string tag = "", string description = "",
            int timeout = -1, int maxRetries = 0, ManagedTask.TaskPriority priority = ManagedTask.TaskPriority.Normal)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            EnsureNotDisposed();

            var id = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(async () =>
            {
                try
                {
                    await function(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 任务被取消，正常退出
                }
                catch (Exception ex) when (maxRetries > 0 && !token.IsCancellationRequested)
                {
                    // 重试逻辑
                    try
                    {
                        await Task.Run(() => HandleRetry(id, function, token, ex, 1, maxRetries), token);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        // 重试过程中被取消
                        throw;
                    }
                }
            }, token);

            var managedTask = CreateManagedTask(id, tag, description, task, cts, timeout, maxRetries, priority);
            return id;
        }

        /// <summary>
        /// 创建并启动一个新的异步任务，带有返回值
        /// </summary>
        /// <typeparam name="TResult">任务返回值类型</typeparam>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <param name="timeout">超时时间（毫秒），默认为-1表示无限等待</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="priority">任务优先级</param>
        /// <returns>被管理任务的ID</returns>
        /// <exception cref="ArgumentNullException">function为null时抛出</exception>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        public Guid StartNewAsync<TResult>(Func<CancellationToken, Task<TResult>> function, string tag = "", string description = "",
            int timeout = -1, int maxRetries = 0, ManagedTask.TaskPriority priority = ManagedTask.TaskPriority.Normal)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));
            
            EnsureNotDisposed();

            var id = Guid.NewGuid();
            var cts = new CancellationTokenSource();
            var token = cts.Token;

            var task = Task.Run(async () =>
            {
                try
                {
                    return await function(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 任务被取消，抛出异常以便Task.IsCanceled为true
                    throw;
                }
                catch (Exception ex) when (maxRetries > 0 && !token.IsCancellationRequested)
                {
                    // 重试逻辑
                    return await HandleRetryWithResultAsync(id, function, token, ex, 1, maxRetries);
                }
            }, token);

            var managedTask = CreateManagedTask(id, tag, description, task, cts, timeout, maxRetries, priority);
            return id;
        }

        #region IDisposable Implementation
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 取消所有任务
                    foreach (var task in _tasks.Values)
                    {
                        try
                        {
                            if (!task.CancellationTokenSource.IsCancellationRequested)
                            {
                                task.CancellationTokenSource.Cancel();
                            }
                            task.CancellationTokenSource.Dispose();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                    
                    // 清空任务字典
                    _tasks.Clear();
                }
                
                _disposed = true;
            }
        }
        
        /// <summary>
        /// 析构函数
        /// </summary>
        ~TaskManager()
        {
            Dispose(false);
        }
        
        #endregion
    }
} 