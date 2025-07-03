using System;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Core.Utils;

namespace TaiChi.Core.Mvvm.Tasks
{
    /// <summary>
    /// TaskManager的扩展方法，添加进度报告功能
    /// </summary>
    public static class TaskManagerExtensions
    {
        /// <summary>
        /// 创建并启动一个带进度报告的新任务
        /// </summary>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="action">要执行的操作</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewWithProgress(this TaskManager taskManager, Action<CancellationToken, IProgress<double>> action, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateDoubleProgressReporter(taskId);

            var result = taskManager.StartNew((token) =>
            {
                try
                {
                    action(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 创建并启动一个带进度报告的新任务，支持消息报告
        /// </summary>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="action">要执行的操作</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewWithProgress(this TaskManager taskManager, Action<CancellationToken, IProgress<(double value, string message)>> action, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateProgressReporter(taskId);

            var result = taskManager.StartNew((token) =>
            {
                try
                {
                    action(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 创建并启动一个带进度报告的新异步任务
        /// </summary>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewAsyncWithProgress(this TaskManager taskManager, Func<CancellationToken, IProgress<double>, Task> function, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateDoubleProgressReporter(taskId);

            var result = taskManager.StartNewAsync(async (token) =>
            {
                try
                {
                    await function(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 创建并启动一个带进度报告的新异步任务，支持消息报告
        /// </summary>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewAsyncWithProgress(this TaskManager taskManager, Func<CancellationToken, IProgress<(double value, string message)>, Task> function, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateProgressReporter(taskId);

            var result = taskManager.StartNewAsync(async (token) =>
            {
                try
                {
                    await function(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 创建并启动一个带进度报告和返回值的新异步任务
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewAsyncWithProgress<TResult>(this TaskManager taskManager, Func<CancellationToken, IProgress<double>, Task<TResult>> function, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateDoubleProgressReporter(taskId);

            var result = taskManager.StartNewAsync(async (token) =>
            {
                try
                {
                    return await function(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 创建并启动一个带进度报告和返回值的新异步任务，支持消息报告
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="function">要执行的异步函数</param>
        /// <param name="tag">任务标签</param>
        /// <param name="description">任务描述</param>
        /// <returns>被管理任务的ID</returns>
        public static Guid StartNewAsyncWithProgress<TResult>(this TaskManager taskManager, Func<CancellationToken, IProgress<(double value, string message)>, Task<TResult>> function, string tag = "", string description = "")
        {
            if (taskManager == null)
                throw new ArgumentNullException(nameof(taskManager));
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var taskId = Guid.NewGuid();
            var progress = TaskProgressReporter.Instance.CreateProgressReporter(taskId);

            var result = taskManager.StartNewAsync(async (token) =>
            {
                try
                {
                    return await function(token, progress);
                }
                finally
                {
                    // 任务完成后自动清理进度信息
                    TaskProgressReporter.Instance.RemoveTaskProgress(taskId);
                }
            }, tag, description);

            return result;
        }

        /// <summary>
        /// 更新TaskItemViewModel中的进度信息
        /// </summary>
        /// <param name="taskManager">任务管理器</param>
        /// <param name="taskViewModel">任务ViewModel</param>
        /// <returns>是否成功更新</returns>
        public static bool UpdateTaskProgress(this TaskManager taskManager, TaskItemViewModel taskViewModel)
        {
            if (taskManager == null || taskViewModel == null)
                return false;

            var progress = TaskProgressReporter.Instance.GetTaskProgress(taskViewModel.Id);
            if (progress != null)
            {
                taskViewModel.Progress = progress.Value;
                return true;
            }

            return false;
        }
    }
}