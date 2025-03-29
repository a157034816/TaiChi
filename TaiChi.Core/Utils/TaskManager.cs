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
    /// 任务管理器，用于创建、管理和取消异步任务。
    /// 提供了统一的接口来处理任务生命周期、超时控制、重试机制和事件通知。
    /// 具有以下特性：
    /// - 可创建具有超时机制的任务
    /// - 支持自动重试失败的任务
    /// - 任务分组管理（通过标签）
    /// - 任务优先级控制
    /// - 提供同步和异步API
    /// - 支持取消和等待任务
    /// - 事件通知机制
    /// </summary>
    public class TaskManager : IDisposable
    {
        /// <summary>
        /// 单例实例，使用延迟初始化模式
        /// </summary>
        private static readonly Lazy<TaskManager> _instance = new Lazy<TaskManager>(() => new TaskManager());

        /// <summary>
        /// 获取任务管理器的单例实例
        /// </summary>
        /// <remarks>
        /// 使用单例模式确保全局只有一个任务管理器实例，便于集中管理所有任务
        /// </remarks>
        public static TaskManager Instance => _instance.Value;

        /// <summary>
        /// 存储所有被管理的任务信息
        /// </summary>
        /// <remarks>
        /// 使用线程安全的ConcurrentDictionary确保多线程环境下的数据一致性
        /// </remarks>
        private readonly ConcurrentDictionary<Guid, ManagedTask> _tasks = new ConcurrentDictionary<Guid, ManagedTask>();
        
        /// <summary>
        /// 是否已释放资源
        /// </summary>
        /// <remarks>
        /// 用于防止在对象释放后继续使用
        /// </remarks>
        private bool _disposed;
        
        /// <summary>
        /// 用于同步的对象
        /// </summary>
        /// <remarks>
        /// 在需要原子操作而ConcurrentDictionary不足以保证线程安全时使用
        /// </remarks>
        private readonly object _syncLock = new object();
        
        /// <summary>
        /// 默认超时时间（毫秒），-1表示无限等待
        /// </summary>
        /// <remarks>
        /// 全局默认超时设置，可根据应用需求调整
        /// </remarks>
        public int DefaultTimeout { get; set; } = -1;
        
        /// <summary>
        /// 任务完成后保留在内存中的时间（毫秒），默认为30000（30秒）
        /// </summary>
        /// <remarks>
        /// 控制已完成任务在内存中保留的时间，以便其结果可以被后续操作获取。
        /// 设置为0或负值表示立即移除已完成的任务。
        /// </remarks>
        public int CompletedTaskRetentionTime { get; set; } = 30000;
        
        /// <summary>
        /// 存储定时移除已完成任务的取消令牌源
        /// </summary>
        /// <remarks>
        /// 用于在任务管理器释放时取消所有定时清理任务
        /// </remarks>
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cleanupTokenSources = new ConcurrentDictionary<Guid, CancellationTokenSource>();
        
        /// <summary>
        /// 当任务被添加时触发
        /// </summary>
        /// <remarks>
        /// 可用于任务创建的日志记录、监控或UI更新
        /// </remarks>
        public event EventHandler<TaskEventArgs>? TaskAdded;
        
        /// <summary>
        /// 当任务被完成时触发（不管成功、失败或取消）
        /// </summary>
        /// <remarks>
        /// 适用于需要在任务完成后执行通用清理或通知逻辑的场景
        /// </remarks>
        public event EventHandler<TaskEventArgs>? TaskCompleted;
        
        /// <summary>
        /// 当任务执行失败时触发
        /// </summary>
        /// <remarks>
        /// 可用于异常日志记录、错误通知或实现自定义错误处理逻辑
        /// </remarks>
        public event EventHandler<TaskExceptionEventArgs>? TaskFaulted;
        
        /// <summary>
        /// 当任务被取消时触发
        /// </summary>
        /// <remarks>
        /// 可用于资源清理、日志记录或用户通知
        /// </remarks>
        public event EventHandler<TaskEventArgs>? TaskCanceled;

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        /// <remarks>
        /// 实现单例模式的一部分，确保TaskManager只能通过Instance属性访问
        /// </remarks>
        private TaskManager() { }
        
        /// <summary>
        /// 表示任务事件的参数
        /// </summary>
        /// <remarks>
        /// 在触发任务相关事件时传递的基本信息，包含任务的标识、标签和描述
        /// </remarks>
        public class TaskEventArgs : EventArgs
        {
            /// <summary>
            /// 任务ID
            /// </summary>
            /// <remarks>
            /// 全局唯一标识符，用于在任务管理器中唯一标识一个任务
            /// </remarks>
            public Guid TaskId { get; }
            
            /// <summary>
            /// 任务标签
            /// </summary>
            /// <remarks>
            /// 用于对任务进行分类和分组，便于批量操作和查询
            /// </remarks>
            public string TaskTag { get; }
            
            /// <summary>
            /// 任务描述
            /// </summary>
            /// <remarks>
            /// 对任务目的和功能的描述性文本，有助于调试和日志记录
            /// </remarks>
            public string TaskDescription { get; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="taskId">任务ID</param>
            /// <param name="taskTag">任务标签</param>
            /// <param name="taskDescription">任务描述</param>
            /// <remarks>
            /// 创建一个新的任务事件参数实例，包含基本任务信息
            /// </remarks>
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
        /// <remarks>
        /// 在任务执行失败时提供额外的异常信息，继承自TaskEventArgs
        /// </remarks>
        public class TaskExceptionEventArgs : TaskEventArgs
        {
            /// <summary>
            /// 异常信息
            /// </summary>
            /// <remarks>
            /// 任务执行过程中抛出的异常对象，包含错误详情
            /// </remarks>
            public Exception Exception { get; }
            
            /// <summary>
            /// 构造函数
            /// </summary>
            /// <param name="taskId">任务ID</param>
            /// <param name="taskTag">任务标签</param>
            /// <param name="taskDescription">任务描述</param>
            /// <param name="exception">导致任务失败的异常</param>
            /// <remarks>
            /// 创建一个新的任务异常事件参数实例，包含任务基本信息和异常详情
            /// </remarks>
            public TaskExceptionEventArgs(Guid taskId, string taskTag, string taskDescription, Exception exception)
                : base(taskId, taskTag, taskDescription)
            {
                Exception = exception;
            }
        }

        /// <summary>
        /// 表示一个被管理的任务
        /// </summary>
        /// <remarks>
        /// 封装了任务的所有元数据和状态信息，提供对任务生命周期的完整跟踪和控制能力。
        /// 每个ManagedTask包含一个底层Task对象，并添加了额外的管理功能如超时控制、重试机制和状态跟踪。
        /// </remarks>
        public class ManagedTask
        {
            /// <summary>
            /// 任务的唯一标识符
            /// </summary>
            /// <remarks>
            /// 在任务管理器中唯一标识该任务的GUID
            /// </remarks>
            public Guid Id { get; }

            /// <summary>
            /// 任务的标签，用于分组和批量操作
            /// </summary>
            /// <remarks>
            /// 可用于对相关任务进行分组、筛选和批量管理
            /// </remarks>
            public string Tag { get; }

            /// <summary>
            /// 任务的描述信息
            /// </summary>
            /// <remarks>
            /// 提供对任务目的和功能的描述，便于日志记录和调试
            /// </remarks>
            public string Description { get; }

            /// <summary>
            /// 任务的创建时间
            /// </summary>
            /// <remarks>
            /// 任务实例被创建的精确时间，用于计算任务的等待时间和总生命周期
            /// </remarks>
            public DateTime CreationTime { get; }
            
            /// <summary>
            /// 任务的开始执行时间
            /// </summary>
            /// <remarks>
            /// 任务开始实际执行的时间（从WaitingToRun变为Running的时刻），可用于性能分析
            /// </remarks>
            public DateTime? StartTime { get; private set; }
            
            /// <summary>
            /// 任务的完成时间
            /// </summary>
            /// <remarks>
            /// 任务执行完成的时间（无论成功、失败或取消），与StartTime一起用于计算实际执行时长
            /// </remarks>
            public DateTime? CompletionTime { get; private set; }
            
            /// <summary>
            /// 任务的执行时长（毫秒）
            /// </summary>
            /// <remarks>
            /// 任务从开始执行到完成所花费的时间，仅在任务完成后有值。
            /// 用于性能监控和分析任务执行效率。
            /// </remarks>
            public long? ExecutionDuration => CompletionTime.HasValue && StartTime.HasValue
                ? (long?)(CompletionTime.Value - StartTime.Value).TotalMilliseconds
                : null;

            /// <summary>
            /// 任务的取消令牌源
            /// </summary>
            /// <remarks>
            /// 用于触发任务取消的CancellationTokenSource，可通过Cancel方法请求任务终止
            /// </remarks>
            public CancellationTokenSource CancellationTokenSource { get; }
            
            /// <summary>
            /// 任务的取消令牌
            /// </summary>
            /// <remarks>
            /// 传递给任务的CancellationToken，任务应定期检查此令牌以支持协作式取消
            /// </remarks>
            public CancellationToken CancellationToken => CancellationTokenSource.Token;

            /// <summary>
            /// 底层的Task对象
            /// </summary>
            /// <remarks>
            /// 封装实际工作的Task实例，由.NET Task系统调度和执行
            /// </remarks>
            public Task Task { get; }
            
            /// <summary>
            /// 任务的重试计数
            /// </summary>
            /// <remarks>
            /// 记录任务已重试的次数，用于控制重试逻辑和监控重试状态
            /// </remarks>
            public int RetryCount { get; private set; }
            
            /// <summary>
            /// 任务的最大重试次数
            /// </summary>
            /// <remarks>
            /// 任务失败后最多允许重试的次数，设为0表示不进行重试
            /// </remarks>
            public int MaxRetries { get; }
            
            /// <summary>
            /// 任务的优先级
            /// </summary>
            /// <remarks>
            /// 表示任务的相对重要性，可用于任务调度策略
            /// </remarks>
            public TaskPriority Priority { get; }
            
            /// <summary>
            /// 任务的超时时间（毫秒），-1表示无限等待
            /// </summary>
            /// <remarks>
            /// 任务的最大允许执行时间，超过此时间将被取消。设为-1表示任务可以无限期执行。
            /// </remarks>
            public int Timeout { get; }
            
            /// <summary>
            /// 任务是否已超时
            /// </summary>
            /// <remarks>
            /// 表示任务是否因执行时间超过超时设置而被标记为超时
            /// </remarks>
            public bool IsTimedOut { get; private set; }

            /// <summary>
            /// 任务是否已请求取消
            /// </summary>
            /// <remarks>
            /// 表示任务是否已收到取消请求，但任务可能尚未响应取消信号
            /// </remarks>
            public bool IsCancelRequested => CancellationTokenSource.IsCancellationRequested;

            /// <summary>
            /// 任务的当前状态
            /// </summary>
            /// <remarks>
            /// 反映底层Task对象的当前执行状态，如创建、等待、运行、完成等
            /// </remarks>
            public TaskStatus Status => Task.Status;

            /// <summary>
            /// 任务是否已完成
            /// </summary>
            /// <remarks>
            /// 表示任务是否已完成处理（不论成功、失败或取消）
            /// </remarks>
            public bool IsCompleted => Task.IsCompleted;

            /// <summary>
            /// 任务是否已取消
            /// </summary>
            /// <remarks>
            /// 表示任务是否因收到取消请求而终止。
            /// 包括两种情况：
            /// 1. 任务已响应取消请求并将自身状态设置为已取消
            /// 2. 任务因超时而被自动取消
            /// 注意：此属性反映的是任务的最终状态，而非是否收到了取消请求。
            /// 任务收到取消请求后，需要一定时间才会变为已取消状态。
            /// </remarks>
            public bool IsCanceled => Task.IsCanceled || IsTimedOut; // 任务超时时也应被视为已取消

            /// <summary>
            /// 任务是否出现异常
            /// </summary>
            /// <remarks>
            /// 表示任务是否因未处理异常而失败
            /// </remarks>
            public bool IsFaulted => Task.IsFaulted;

            /// <summary>
            /// 任务是否被取消或超时
            /// </summary>
            /// <remarks>
            /// 表示任务是否因取消请求或超时而终止。比IsCanceled更宽松，
            /// 可用于检查任务是否收到取消信号或已取消，特别适用于测试场景。
            /// </remarks>
            public bool IsCanceledOrTimedOut => Task.IsCanceled || IsTimedOut || IsCancelRequested;
            
            /// <summary>
            /// 任务是否正在运行
            /// </summary>
            /// <remarks>
            /// 表示任务当前是否处于活跃执行状态
            /// </remarks>
            public bool IsRunning => Task.Status == TaskStatus.Running;
            
            /// <summary>
            /// 任务是否正在等待
            /// </summary>
            /// <remarks>
            /// 表示任务是否处于等待状态，包括等待调度、等待激活或等待子任务完成
            /// </remarks>
            public bool IsWaiting => Task.Status == TaskStatus.WaitingToRun || 
                                    Task.Status == TaskStatus.WaitingForActivation || 
                                    Task.Status == TaskStatus.WaitingForChildrenToComplete;
            
            /// <summary>
            /// 任务是否成功完成
            /// </summary>
            /// <remarks>
            /// 表示任务是否正常完成，没有异常或取消
            /// </remarks>
            public bool IsSuccessfullyCompleted => Task.Status == TaskStatus.RanToCompletion;

            /// <summary>
            /// 任务的异常信息（如果有）
            /// </summary>
            /// <remarks>
            /// 如果任务执行失败，包含导致失败的异常信息，否则为null
            /// </remarks>
            public AggregateException? Exception => Task.Exception;
            
            /// <summary>
            /// 任务优先级枚举
            /// </summary>
            /// <remarks>
            /// 定义任务的优先级等级，从Low到Critical，用于任务调度决策
            /// </remarks>
            public enum TaskPriority
            {
                /// <summary>
                /// 低优先级，适用于后台任务或不紧急的操作
                /// </summary>
                Low = 0,
                
                /// <summary>
                /// 普通优先级，默认优先级级别
                /// </summary>
                Normal = 1,
                
                /// <summary>
                /// 高优先级，适用于需要较快响应的任务
                /// </summary>
                High = 2,
                
                /// <summary>
                /// 关键优先级，适用于系统关键操作或紧急任务
                /// </summary>
                Critical = 3
            }

            /// <summary>
            /// 构造一个被管理的任务
            /// </summary>
            /// <param name="id">任务的唯一ID</param>
            /// <param name="tag">任务的标签，用于分组</param>
            /// <param name="description">任务的描述</param>
            /// <param name="task">底层的Task对象</param>
            /// <param name="cts">取消令牌源</param>
            /// <param name="timeout">超时时间（毫秒）</param>
            /// <param name="maxRetries">最大重试次数</param>
            /// <param name="priority">任务优先级</param>
            /// <remarks>
            /// 创建一个新的被管理任务实例，初始化所有任务元数据并设置状态跟踪
            /// </remarks>
            /// <exception cref="ArgumentNullException">task或cts为null时抛出</exception>
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
            /// <remarks>
            /// 在任务重试时调用，递增重试计数器
            /// </remarks>
            internal void IncrementRetryCount()
            {
                RetryCount++;
            }
            
            /// <summary>
            /// 判断是否可以重试任务
            /// </summary>
            /// <remarks>
            /// 根据当前重试次数和最大允许重试次数确定是否可以进行下一次重试
            /// </remarks>
            internal bool CanRetry => RetryCount < MaxRetries;
            
            /// <summary>
            /// 标记任务为超时
            /// </summary>
            /// <remarks>
            /// 在任务执行时间超过Timeout设置时调用，将任务标记为已超时
            /// </remarks>
            internal void MarkAsTimedOut()
            {
                IsTimedOut = true;
            }
        }

        /// <summary>
        /// 验证任务管理器的状态
        /// </summary>
        /// <remarks>
        /// 在执行操作前检查任务管理器是否已释放，防止在对象无效时进行操作
        /// </remarks>
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
        /// <param name="task">被添加的任务对象</param>
        /// <remarks>
        /// 安全地调用TaskAdded事件处理程序，任务添加到管理器后调用
        /// </remarks>
        private void OnTaskAdded(ManagedTask task)
        {
            TaskAdded?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 触发任务完成事件
        /// </summary>
        /// <param name="task">已完成的任务对象</param>
        /// <remarks>
        /// 安全地调用TaskCompleted事件处理程序，任务完成（无论成功、失败或取消）时调用
        /// </remarks>
        private void OnTaskCompleted(ManagedTask task)
        {
            TaskCompleted?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 触发任务失败事件
        /// </summary>
        /// <param name="task">失败的任务对象</param>
        /// <param name="exception">导致任务失败的异常</param>
        /// <remarks>
        /// 安全地调用TaskFaulted事件处理程序，任务因异常失败时调用
        /// </remarks>
        private void OnTaskFaulted(ManagedTask task, Exception exception)
        {
            TaskFaulted?.Invoke(this, new TaskExceptionEventArgs(task.Id, task.Tag, task.Description, exception));
        }

        /// <summary>
        /// 触发任务取消事件
        /// </summary>
        /// <param name="task">被取消的任务对象</param>
        /// <remarks>
        /// 安全地调用TaskCanceled事件处理程序，任务被取消时调用
        /// </remarks>
        private void OnTaskCanceled(ManagedTask task)
        {
            TaskCanceled?.Invoke(this, new TaskEventArgs(task.Id, task.Tag, task.Description));
        }

        /// <summary>
        /// 注册任务完成事件
        /// </summary>
        /// <param name="task">需要注册回调的任务</param>
        /// <remarks>
        /// 为任务添加完成后的回调处理，处理任务完成、失败或取消的情况，并触发相应事件。
        /// 同时处理任务的延迟清理，以及超时监控设置。
        /// </remarks>
        private void RegisterTaskCompletionCallbacks(ManagedTask task)
        {
            task.Task.ContinueWith(t =>
            {
                try
                {
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
                    
                    // 如果设置了保留时间，则延迟移除任务
                    if (CompletedTaskRetentionTime > 0)
                    {
                        ScheduleTaskRemoval(task.Id);
                    }
                    else
                    {
                        // 立即移除任务
                        _tasks.TryRemove(task.Id, out _);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"任务完成回调处理异常: {ex}");
                }
            }, CancellationToken.None);
            
            // 处理超时
            if (task.Timeout > 0)
            {
                RegisterTimeoutHandler(task);
            }
        }
        
        /// <summary>
        /// 安排延迟移除已完成的任务
        /// </summary>
        /// <param name="taskId">要移除的任务ID</param>
        /// <remarks>
        /// 创建一个定时器，在CompletedTaskRetentionTime时间后移除已完成的任务。
        /// 这允许任务结果在一段时间内仍然可用，即使任务已经完成。
        /// </remarks>
        private void ScheduleTaskRemoval(Guid taskId)
        {
            // 创建取消令牌源
            var cts = new CancellationTokenSource();
            _cleanupTokenSources[taskId] = cts;
            
            // 计划在指定时间后移除任务
            Task.Delay(CompletedTaskRetentionTime, cts.Token).ContinueWith(t =>
            {
                if (!t.IsCanceled)
                {
                    // 移除任务
                    _tasks.TryRemove(taskId, out _);
                    // 移除取消令牌源
                    _cleanupTokenSources.TryRemove(taskId, out _);
                }
            }, CancellationToken.None);
        }
        
        /// <summary>
        /// 注册任务超时处理
        /// </summary>
        /// <param name="task">需要监控超时的任务</param>
        /// <remarks>
        /// 为任务设置超时监控机制，在任务超过指定Timeout时间后触发取消操作。
        /// 只有当任务指定了大于0的Timeout值时才会注册超时处理。
        /// </remarks>
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
                        if (!task.CancellationTokenSource.IsCancellationRequested)
                        {
                            task.CancellationTokenSource.Cancel();
                            
                            // 确保任务状态更新
                            _tasks.TryGetValue(task.Id, out _);
                            
                            Debug.WriteLine($"任务已超时并被取消: {task.Id}, 标签: {task.Tag}, 描述: {task.Description}");
                            
                            // 触发取消事件
                            OnTaskCanceled(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"取消超时任务时发生异常: {ex}");
                    }
                }
                else
                {
                    // 记录任务当前状态，即使任务已结束但正好在超时时间内完成
                    Debug.WriteLine($"任务超时检查 - 已完成: {task.IsCompleted}, 已取消: {task.IsCanceled}, 出错: {task.IsFaulted}, ID: {task.Id}");
                }
            }, CancellationToken.None);
        }
        
        /// <summary>
        /// 创建一个新的任务实例
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <param name="task">底层Task对象</param>
        /// <param name="cts">取消令牌源</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="priority">任务优先级</param>
        /// <returns>创建的被管理任务对象</returns>
        /// <remarks>
        /// 创建一个新的ManagedTask实例，添加到任务字典，注册事件回调和超时处理。
        /// 此方法是StartNew等公共方法的内部实现基础。
        /// </remarks>
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
        /// <remarks>
        /// 创建一个新的无返回值任务，并立即开始执行。
        /// 任务支持取消、超时、重试和事件通知机制。
        /// 任务创建后会自动添加到任务管理器中进行监控。
        /// </remarks>
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
                    // 确保任务执行前检查令牌
                    token.ThrowIfCancellationRequested();
                    action(token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    // 任务被取消，正常退出
                    Debug.WriteLine($"任务被取消: {id}, 标签: {tag}, 描述: {description}");
                    // 不重新抛出异常，让任务正常标记为已取消
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
        /// <typeparam name="T">动作类型，必须是引用类型</typeparam>
        /// <param name="taskId">任务ID</param>
        /// <param name="action">要重试的动作</param>
        /// <param name="token">取消令牌</param>
        /// <param name="exception">导致失败的异常</param>
        /// <param name="currentRetry">当前重试次数</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <param name="callerName">调用方法的名称，用于反射判断动作类型</param>
        /// <remarks>
        /// 通用任务重试处理逻辑，实现了指数退避策略。
        /// 根据调用者方法名和动作类型动态决定如何执行重试。
        /// 支持同步和异步操作的重试，并处理取消和资源释放的场景。
        /// </remarks>
        /// <exception cref="OperationCanceledException">当任务被取消时抛出</exception>
        /// <exception cref="AggregateException">当达到最大重试次数后仍然失败时抛出</exception>
        /// <exception cref="InvalidOperationException">当找不到要重试的任务或无法识别调用方法时抛出</exception>
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
        /// <remarks>
        /// 创建一个新的有返回值的任务，并立即开始执行。
        /// 任务支持取消、超时、重试和事件通知机制。
        /// 任务创建后会自动添加到任务管理器中进行监控。
        /// 任务结果可通过<see cref="GetTaskResult{TResult}"/>或<see cref="GetTaskResultAsync{TResult}"/>方法获取。
        /// </remarks>
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
        /// <typeparam name="TResult">任务结果类型</typeparam>
        /// <param name="taskId">任务ID</param>
        /// <param name="function">要重试的函数</param>
        /// <param name="token">取消令牌</param>
        /// <param name="exception">导致失败的异常</param>
        /// <param name="currentRetry">当前重试次数</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>函数执行结果</returns>
        /// <remarks>
        /// 处理带返回值的任务重试逻辑，实现了指数退避策略。
        /// 与HandleRetry不同，此方法专门处理有返回值的任务，并将返回值传递回调用者。
        /// </remarks>
        /// <exception cref="OperationCanceledException">当任务被取消时抛出</exception>
        /// <exception cref="AggregateException">当达到最大重试次数后仍然失败时抛出</exception>
        /// <exception cref="InvalidOperationException">当找不到要重试的任务时抛出</exception>
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
        /// <remarks>
        /// 尝试取消指定ID的任务。取消操作通过向任务的CancellationTokenSource发送取消信号实现。
        /// 任务需要正确处理取消信号才能及时响应取消请求。
        /// 
        /// 注意：仅调用CancellationTokenSource.Cancel()方法本身不会立即将Task状态更改为Canceled。
        /// 任务必须检查取消令牌并正确响应(通常是抛出OperationCanceledException)才会更新其状态。
        /// 
        /// 此方法返回true仅表示取消信号已成功发送，不表示任务已立即响应取消请求。
        /// 调用代码应该通过等待任务完成或检查IsCanceledOrTimedOut属性来确认取消是否已被处理。
        /// 
        /// 如果任务不存在或已经被取消，则不会采取任何操作。
        /// </remarks>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="InvalidOperationException">如果throwOnError为true且取消失败或任务不存在时抛出</exception>
        public bool CancelTask(Guid id, bool throwOnError = false)
        {
            EnsureNotDisposed();
            
            if (_tasks.TryGetValue(id, out var task))
            {
                try
                {
                    // 检查任务是否已请求取消
                    if (!task.CancellationTokenSource.IsCancellationRequested)
                    {
                        // 如果任务已完成或已取消，记录此信息但仍返回成功
                        if (task.IsCompleted)
                        {
                            Debug.WriteLine($"尝试取消任务 {id}，但任务已完成");
                            return true;
                        }
                        
                        if (task.IsCanceledOrTimedOut)
                        {
                            Debug.WriteLine($"尝试取消任务 {id}，但任务已被取消或超时");
                            return true;
                        }
                        
                        // 发送取消信号
                        Debug.WriteLine($"取消任务 {id}");
                        task.CancellationTokenSource.Cancel();
                        
                        // 触发取消事件，确保即使任务未及时响应，UI等也能获取到取消状态
                        OnTaskCanceled(task);
                    }
                    else
                    {
                        Debug.WriteLine($"任务 {id} 已经请求取消");
                    }
                    
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"取消任务 {id} 时发生异常: {ex.Message}");
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
        /// <remarks>
        /// 同步等待指定ID的任务完成。这将阻塞当前线程直到任务完成或超时。
        /// 如果任务不存在，则视为已完成并立即返回true。
        /// 如果timeout为-1，则使用TaskManager.DefaultTimeout值。
        /// </remarks>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果throwOnTimeout为true且等待超时时抛出</exception>
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
                
                // 如果任务已完成，确保事件被触发
                if (completed) 
                {
                    // 可能事件回调还未执行，确保直接触发事件
                    if (task.Task.IsCompleted) 
                    {
                        // 仅当任务真正完成，且没有被移除时触发事件
                        if (_tasks.TryGetValue(id, out _))
                        {
                            // 同步处理任务完成状态
                            try 
                            {
                                // 触发完成事件
                                OnTaskCompleted(task);
                                
                                // 根据任务状态触发对应事件
                                if (task.Task.IsFaulted && task.Task.Exception != null)
                                {
                                    OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                                }
                                else if (task.Task.IsCanceled)
                                {
                                    OnTaskCanceled(task);
                                }
                                
                                // 如果设置了保留时间，则延迟移除任务
                                if (CompletedTaskRetentionTime > 0)
                                {
                                    ScheduleTaskRemoval(task.Id);
                                }
                                else
                                {
                                    // 立即移除任务
                                    _tasks.TryRemove(task.Id, out _);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"任务完成回调处理异常: {ex}");
                            }
                        }
                    }
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
            
            // 如果任务已完成，确保事件被触发
            if (completed)
            {
                // 对于每个已完成的任务，确保事件被触发
                foreach (var task in tasks.Where(t => t.IsCompleted))
                {
                    // 仅当任务真正完成，且没有被移除时触发事件
                    if (_tasks.TryGetValue(task.Id, out _))
                    {
                        try
                        {
                            // 触发完成事件
                            OnTaskCompleted(task);
                            
                            // 根据任务状态触发对应事件
                            if (task.Task.IsFaulted && task.Task.Exception != null)
                            {
                                OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                            }
                            else if (task.Task.IsCanceled)
                            {
                                OnTaskCanceled(task);
                            }
                            
                            // 如果设置了保留时间，则延迟移除任务
                            if (CompletedTaskRetentionTime > 0)
                            {
                                ScheduleTaskRemoval(task.Id);
                            }
                            else
                            {
                                // 立即移除任务
                                _tasks.TryRemove(task.Id, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"任务完成回调处理异常: {ex}");
                        }
                    }
                }
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
            
            var tasks = _tasks.Values.ToArray();
            if (tasks.Length == 0)
                return true;

            var taskArray = tasks.Select(t => t.Task).ToArray();
            bool completed = Task.WaitAll(taskArray, timeout);
            
            if (!completed && throwOnTimeout)
            {
                throw new TimeoutException("等待所有任务完成超时");
            }
            
            // 如果任务已完成，确保事件被触发
            if (completed)
            {
                // 对于每个已完成的任务，确保事件被触发
                foreach (var task in tasks.Where(t => t.IsCompleted))
                {
                    // 仅当任务真正完成，且没有被移除时触发事件
                    if (_tasks.TryGetValue(task.Id, out _))
                    {
                        try
                        {
                            // 触发完成事件
                            OnTaskCompleted(task);
                            
                            // 根据任务状态触发对应事件
                            if (task.Task.IsFaulted && task.Task.Exception != null)
                            {
                                OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                            }
                            else if (task.Task.IsCanceled)
                            {
                                OnTaskCanceled(task);
                            }
                            
                            // 如果设置了保留时间，则延迟移除任务
                            if (CompletedTaskRetentionTime > 0)
                            {
                                ScheduleTaskRemoval(task.Id);
                            }
                            else
                            {
                                // 立即移除任务
                                _tasks.TryRemove(task.Id, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"任务完成回调处理异常: {ex}");
                        }
                    }
                }
            }
            
            return completed;
        }

        /// <summary>
        /// 异步等待指定ID的任务完成
        /// </summary>
        /// <param name="id">任务ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示等待操作的任务</returns>
        /// <remarks>
        /// 异步等待指定ID的任务完成，不会阻塞调用线程。
        /// 如果任务不存在，则立即完成并返回。
        /// 如果任务已设置超时，则会自动处理超时取消逻辑。
        /// 如果任务执行失败，原始异常将被抛出。
        /// </remarks>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果任务超时</exception>
        /// <exception cref="AggregateException">如果任务执行失败并抛出异常</exception>
        /// <exception cref="OperationCanceledException">如果等待被取消</exception>
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
                
                // 任务已完成，确保事件被触发
                if (task.IsCompleted)
                {
                    // 仅当任务真正完成，且没有被移除时触发事件
                    if (_tasks.TryGetValue(id, out _))
                    {
                        try
                        {
                            // 触发完成事件
                            OnTaskCompleted(task);
                            
                            // 根据任务状态触发对应事件
                            if (task.Task.IsFaulted && task.Task.Exception != null)
                            {
                                OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                            }
                            else if (task.Task.IsCanceled)
                            {
                                OnTaskCanceled(task);
                            }
                            
                            // 如果设置了保留时间，则延迟移除任务
                            if (CompletedTaskRetentionTime > 0)
                            {
                                ScheduleTaskRemoval(task.Id);
                            }
                            else
                            {
                                // 立即移除任务
                                _tasks.TryRemove(task.Id, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"任务完成回调处理异常: {ex}");
                        }
                    }
                }
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
                    
                    // 如果超时任务没有完成，等待所有其他任务
                    if (completedTask != timeoutTask)
                    {
                        await allTasksTask.ConfigureAwait(false);
                    }
                }
                else
                {
                    await Task.WhenAll(taskArray).ConfigureAwait(false);
                }
                
                // 完成后，确保所有已完成任务的事件被触发
                foreach (var task in tasks.Where(t => t.IsCompleted))
                {
                    // 仅当任务真正完成，且没有被移除时触发事件
                    if (_tasks.TryGetValue(task.Id, out _))
                    {
                        try
                        {
                            // 触发完成事件
                            OnTaskCompleted(task);
                            
                            // 根据任务状态触发对应事件
                            if (task.Task.IsFaulted && task.Task.Exception != null)
                            {
                                OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                            }
                            else if (task.Task.IsCanceled)
                            {
                                OnTaskCanceled(task);
                            }
                            
                            // 如果设置了保留时间，则延迟移除任务
                            if (CompletedTaskRetentionTime > 0)
                            {
                                ScheduleTaskRemoval(task.Id);
                            }
                            else
                            {
                                // 立即移除任务
                                _tasks.TryRemove(task.Id, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"任务完成回调处理异常: {ex}");
                        }
                    }
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
                    
                    // 如果超时任务没有完成，等待所有其他任务
                    if (completedTask != timeoutTask)
                    {
                        await allTasksTask.ConfigureAwait(false);
                    }
                }
                else
                {
                    await Task.WhenAll(taskArray).ConfigureAwait(false);
                }
                
                // 完成后，确保所有已完成任务的事件被触发
                foreach (var task in tasks.Where(t => t.IsCompleted))
                {
                    // 仅当任务真正完成，且没有被移除时触发事件
                    if (_tasks.TryGetValue(task.Id, out _))
                    {
                        try
                        {
                            // 触发完成事件
                            OnTaskCompleted(task);
                            
                            // 根据任务状态触发对应事件
                            if (task.Task.IsFaulted && task.Task.Exception != null)
                            {
                                OnTaskFaulted(task, task.Task.Exception.GetBaseException());
                            }
                            else if (task.Task.IsCanceled)
                            {
                                OnTaskCanceled(task);
                            }
                            
                            // 如果设置了保留时间，则延迟移除任务
                            if (CompletedTaskRetentionTime > 0)
                            {
                                ScheduleTaskRemoval(task.Id);
                            }
                            else
                            {
                                // 立即移除任务
                                _tasks.TryRemove(task.Id, out _);
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"任务完成回调处理异常: {ex}");
                        }
                    }
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
        /// <remarks>
        /// 同步获取任务的执行结果。此方法仅适用于通过StartNew{TResult}或StartNewAsync{TResult}创建的带返回值的任务。
        /// 如果任务尚未完成，此方法将阻塞调用线程直到任务完成。
        /// 如果throwOnError为false，则任务失败或类型不匹配时会返回defaultValue；
        /// 如果throwOnError为true，则会抛出相应异常。
        /// </remarks>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="AggregateException">如果throwOnError为true且任务失败</exception>
        /// <exception cref="InvalidCastException">如果throwOnError为true且任务类型与TResult不匹配</exception>
        /// <exception cref="InvalidOperationException">如果throwOnError为true且任务不存在或尚未完成</exception>
        /// <exception cref="OperationCanceledException">如果throwOnError为true且任务被取消</exception>
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
        /// <remarks>
        /// 异步获取任务的执行结果，不会阻塞调用线程。
        /// 如果任务尚未完成，此方法将异步等待任务完成。
        /// 与同步版本不同，此方法在任务失败、类型不匹配或取消时不会抛出异常，而是返回defaultValue。
        /// 如果任务设置了超时并且超时发生，会自动取消任务并返回defaultValue。
        /// </remarks>
        /// <exception cref="ObjectDisposedException">任务管理器已被释放时抛出</exception>
        /// <exception cref="TimeoutException">如果任务超时</exception>
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
                    
                    // 取消并清理相关的延迟删除定时器
                    if (_cleanupTokenSources.TryRemove(id, out var cts))
                    {
                        try
                        {
                            cts.Cancel();
                            cts.Dispose();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                }
            }
            
            return removedCount;
        }
        
        /// <summary>
        /// 处理带结果的异步任务重试
        /// </summary>
        /// <typeparam name="TResult">任务结果类型</typeparam>
        /// <param name="taskId">任务ID</param>
        /// <param name="function">要重试的异步函数</param>
        /// <param name="token">取消令牌</param>
        /// <param name="exception">导致失败的异常</param>
        /// <param name="currentRetry">当前重试次数</param>
        /// <param name="maxRetries">最大重试次数</param>
        /// <returns>包含函数执行结果的任务</returns>
        /// <remarks>
        /// 处理带返回值的异步任务重试逻辑，实现了指数退避策略。
        /// 与HandleRetryWithResult不同，此方法专门处理返回Task的异步函数，并保持异步执行链。
        /// </remarks>
        /// <exception cref="OperationCanceledException">当任务被取消时抛出</exception>
        /// <exception cref="AggregateException">当达到最大重试次数后仍然失败时抛出</exception>
        /// <exception cref="InvalidOperationException">当找不到要重试的任务时抛出</exception>
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
        /// <remarks>
        /// 创建一个新的无返回值异步任务，并立即开始执行。
        /// 与StartNew不同，此方法接受返回Task的异步函数，并正确处理异步执行链。
        /// 任务支持取消、超时、重试和事件通知机制。
        /// 任务创建后会自动添加到任务管理器中进行监控。
        /// </remarks>
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
        /// <remarks>
        /// 创建一个新的有返回值的异步任务，并立即开始执行。
        /// 与StartNew不同，此方法接受返回Task{TResult}的异步函数，并正确处理异步执行链。
        /// 任务支持取消、超时、重试和事件通知机制。
        /// 任务创建后会自动添加到任务管理器中进行监控。
        /// 任务结果可通过<see cref="GetTaskResult{TResult}"/>或<see cref="GetTaskResultAsync{TResult}"/>方法获取。
        /// </remarks>
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
        /// <remarks>
        /// 实现IDisposable接口，释放所有管理的任务资源。
        /// 会取消所有正在运行的任务，并释放相关的令牌源。
        /// 调用此方法后，任务管理器将不再可用，任何后续操作都将抛出ObjectDisposedException。
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放托管资源</param>
        /// <remarks>
        /// 处理托管和非托管资源的释放。
        /// 如果disposing为true，则释放托管资源（取消任务和清空任务字典）。
        /// 可被子类重写以提供自定义清理逻辑。
        /// </remarks>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 取消所有清理任务
                    foreach (var pair in _cleanupTokenSources)
                    {
                        try
                        {
                            pair.Value.Cancel();
                            pair.Value.Dispose();
                        }
                        catch
                        {
                            // 忽略异常
                        }
                    }
                    _cleanupTokenSources.Clear();
                    
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
        /// <remarks>
        /// 提供终结器，确保在未显式调用Dispose方法的情况下，资源也能够被清理。
        /// 析构函数只会清理非托管资源，不处理托管资源。
        /// </remarks>
        ~TaskManager()
        {
            Dispose(false);
        }
        
        #endregion
    }
} 