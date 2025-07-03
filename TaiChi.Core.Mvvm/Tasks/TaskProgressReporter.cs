using System;
using System.Collections.Concurrent;
using System.Threading;
using TaiChi.Core.Utils;

namespace TaiChi.Core.Mvvm.Tasks
{
    /// <summary>
    /// 任务进度报告器，用于在任务执行过程中报告进度
    /// </summary>
    public class TaskProgressReporter
    {
        private static readonly Lazy<TaskProgressReporter> _instance = new Lazy<TaskProgressReporter>(() => new TaskProgressReporter());

        /// <summary>
        /// 获取任务进度报告器的单例实例
        /// </summary>
        public static TaskProgressReporter Instance => _instance.Value;

        /// <summary>
        /// 存储任务ID和其对应的进度信息
        /// </summary>
        private readonly ConcurrentDictionary<Guid, TaskProgress> _taskProgresses = new ConcurrentDictionary<Guid, TaskProgress>();

        /// <summary>
        /// 私有构造函数，防止外部实例化
        /// </summary>
        private TaskProgressReporter() { }

        /// <summary>
        /// 表示任务的进度信息
        /// </summary>
        public class TaskProgress
        {
            /// <summary>
            /// 任务的唯一标识符
            /// </summary>
            public Guid TaskId { get; }

            /// <summary>
            /// 当前进度值（0-100）
            /// </summary>
            public double Value { get; private set; }

            /// <summary>
            /// 进度描述信息
            /// </summary>
            public string Message { get; private set; }

            /// <summary>
            /// 上次更新时间
            /// </summary>
            public DateTime LastUpdateTime { get; private set; }

            /// <summary>
            /// 进度变化事件
            /// </summary>
            public event EventHandler<TaskProgressChangedEventArgs> ProgressChanged;

            /// <summary>
            /// 构造一个进度信息对象
            /// </summary>
            internal TaskProgress(Guid taskId)
            {
                TaskId = taskId;
                Value = 0;
                Message = string.Empty;
                LastUpdateTime = DateTime.Now;
            }

            /// <summary>
            /// 更新进度信息
            /// </summary>
            internal void Update(double value, string message)
            {
                Value = Math.Max(0, Math.Min(100, value)); // 确保值在0-100范围内
                Message = message ?? string.Empty;
                LastUpdateTime = DateTime.Now;

                // 触发进度变化事件
                ProgressChanged?.Invoke(this, new TaskProgressChangedEventArgs(TaskId, Value, Message));
            }
        }

        /// <summary>
        /// 任务进度变化事件参数
        /// </summary>
        public class TaskProgressChangedEventArgs : EventArgs
        {
            /// <summary>
            /// 任务的唯一标识符
            /// </summary>
            public Guid TaskId { get; }

            /// <summary>
            /// 进度值（0-100）
            /// </summary>
            public double Value { get; }

            /// <summary>
            /// 进度描述信息
            /// </summary>
            public string Message { get; }

            /// <summary>
            /// 构造进度变化事件参数
            /// </summary>
            public TaskProgressChangedEventArgs(Guid taskId, double value, string message)
            {
                TaskId = taskId;
                Value = value;
                Message = message ?? string.Empty;
            }
        }

        /// <summary>
        /// 获取指定任务的进度信息
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>进度信息，如果不存在则返回null</returns>
        public TaskProgress GetTaskProgress(Guid taskId)
        {
            _taskProgresses.TryGetValue(taskId, out var progress);
            return progress;
        }

        /// <summary>
        /// 创建进度报告器
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>进度报告器</returns>
        public IProgress<(double value, string message)> CreateProgressReporter(Guid taskId)
        {
            var progress = _taskProgresses.GetOrAdd(taskId, id => new TaskProgress(id));
            return new Progress<(double value, string message)>(report => progress.Update(report.value, report.message));
        }

        /// <summary>
        /// 创建进度报告器（只报告百分比）
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>进度报告器</returns>
        public IProgress<double> CreateDoubleProgressReporter(Guid taskId)
        {
            var progress = _taskProgresses.GetOrAdd(taskId, id => new TaskProgress(id));
            return new Progress<double>(value => progress.Update(value, progress.Message));
        }

        /// <summary>
        /// 报告任务进度
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <param name="value">进度值（0-100）</param>
        /// <param name="message">进度描述信息</param>
        public void ReportProgress(Guid taskId, double value, string message = null)
        {
            var progress = _taskProgresses.GetOrAdd(taskId, id => new TaskProgress(id));
            progress.Update(value, message);
        }

        /// <summary>
        /// 移除任务进度信息
        /// </summary>
        /// <param name="taskId">任务ID</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveTaskProgress(Guid taskId)
        {
            return _taskProgresses.TryRemove(taskId, out _);
        }

        /// <summary>
        /// 清除所有任务进度信息
        /// </summary>
        public void ClearAllTaskProgresses()
        {
            _taskProgresses.Clear();
        }
    }
}