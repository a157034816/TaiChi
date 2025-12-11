using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Core.Utils;
using Xunit;

namespace TaiChi.Core.Test.Utils
{
    /// <summary>
    /// TaskManager类的单元测试
    /// 测试覆盖了以下功能：
    /// - 任务创建与启动
    /// - 任务状态管理与查询
    /// - 任务取消功能
    /// - 任务等待与超时
    /// - 任务重试机制
    /// - 异步任务处理
    /// - 任务事件通知
    /// - 任务结果获取
    /// </summary>
    public class TaskManagerTests
    {
        /// <summary>
        /// 测试创建并启动一个同步任务
        /// </summary>
        [Fact]
        public void StartNew_ShouldCreateAndExecuteTask()
        {
            // 准备
            bool taskExecuted = false;
            var manager = TaskManager.Instance;

            // 执行
            var taskId = manager.StartNew(ct =>
            {
                taskExecuted = true;
                Thread.Sleep(100); // 模拟工作
            }, "测试任务", "测试基本任务创建");
            var task = manager.GetTask(taskId);

            // 等待任务完成
            manager.WaitForTask(taskId);

            // 验证
            Assert.True(taskExecuted, "任务应该已经执行");

            Assert.NotNull(task);
            Assert.Equal(TaskStatus.RanToCompletion, task!.Status);
            Assert.True(task.IsSuccessfullyCompleted);
        }

        /// <summary>
        /// 测试创建并启动一个有返回值的同步任务
        /// </summary>
        [Fact]
        public void StartNew_WithResult_ShouldCreateAndReturnValue()
        {
            // 准备
            var expectedResult = 42;
            var manager = TaskManager.Instance;

            // 执行
            var taskId = manager.StartNew(ct =>
            {
                Thread.Sleep(100); // 模拟工作
                return expectedResult;
            }, "测试任务", "测试带返回值的任务创建");

            // 等待任务完成
            manager.WaitForTask(taskId);

            // 获取结果
            var result = manager.GetTaskResult<int>(taskId);

            // 验证
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// 测试任务取消功能
        /// </summary>
        [Fact]
        public void CancelTask_ShouldCancelRunningTask()
        {
            // 准备
            var manualResetEvent = new ManualResetEventSlim(false);
            var cancellationCompletedEvent = new ManualResetEventSlim(false);
            var manager = TaskManager.Instance;

            // 执行 - 创建一个长时间运行的任务
            var taskId = manager.StartNew(ct =>
            {
                manualResetEvent.Set(); // 通知任务已开始
                try
                {
                    // 循环检查取消令牌
                    while (!ct.IsCancellationRequested)
                    {
                        Thread.Sleep(10); // 减小睡眠时间，更频繁检查取消状态
                    }
                    // 显式检查并抛出取消异常
                    ct.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    // 预期的异常
                    cancellationCompletedEvent.Set(); // 通知已处理取消请求
                }
            }, "测试任务", "测试任务取消");

            // 获取最新的任务状态
            var task = manager.GetTask(taskId);

            // 等待任务开始
            Assert.True(manualResetEvent.Wait(1000), "任务应该在超时前开始");

            // 取消任务
            bool cancelResult = manager.CancelTask(taskId);

            // 等待取消操作在任务内被处理
            Assert.True(cancellationCompletedEvent.Wait(1000), "任务应该在超时前处理取消请求");

            // 等待任务完成（被取消），确保有足够时间更新任务状态
            manager.WaitForTask(taskId);

            // 验证
            Assert.True(cancelResult, "取消操作应该成功");
            Assert.NotNull(task);

            // 确保任务已被取消或已标记为取消或超时
            Assert.True(task!.IsCanceledOrTimedOut, "任务应该已被取消或超时");
            
            // 输出任务的详细状态以便调试
            System.Diagnostics.Debug.WriteLine($"任务状态: Status={task.Status}, IsCanceled={task.IsCanceled}, IsCanceledOrTimedOut={task.IsCanceledOrTimedOut}");
        }

        /// <summary>
        /// 测试任务超时功能
        /// </summary>
        [Fact]
        public void TaskTimeout_ShouldCancelTaskAfterTimeout()
        {
            // 准备
            var manager = TaskManager.Instance;
            int timeoutMs = 100; // 100毫秒后超时
            bool exceptionCaught = false;
            var manualResetEvent = new ManualResetEventSlim(false);

            // 执行 - 创建一个会超时的任务
            var taskId = manager.StartNew(ct =>
            {
                try
                {
                    manualResetEvent.Set(); // 通知任务已开始执行
                    Debug.WriteLine("任务开始执行");

                    // 尝试运行超过超时时间
                    for (int i = 0; i < 10; i++)
                    {
                        // 每次循环检查取消令牌
                        ct.ThrowIfCancellationRequested();
                        Debug.WriteLine($"任务循环 {i}");
                        Thread.Sleep(50);
                    }
                    
                    Debug.WriteLine("任务执行完成，没有被取消");
                }
                catch (OperationCanceledException ex)
                {
                    // 捕获到预期的取消异常
                    exceptionCaught = true;
                    Debug.WriteLine($"捕获到 OperationCanceledException: {ex.Message}");
                    throw; // 重新抛出以便任务被正确标记为取消
                }
                catch (Exception ex)
                {
                    // 捕获到其他异常
                    Debug.WriteLine($"捕获到其他异常: {ex.GetType().Name}: {ex.Message}");
                    throw;
                }
            }, "测试任务", "测试任务超时", timeoutMs);
            var task = manager.GetTask(taskId);

            // 等待任务开始执行
            Assert.True(manualResetEvent.Wait(1000), "任务应该开始执行");

            // 等待足够长的时间让任务超时
            Thread.Sleep(timeoutMs * 3);

            // 等待任务完成（被取消）
            manager.WaitForTask(taskId);

            // 验证
            Assert.NotNull(task);
            
            // 输出任务的完成和取消状态，帮助诊断问题
            Debug.WriteLine($"任务状态: IsCompleted={task!.IsCompleted}, IsCanceled={task.IsCanceled}, IsTimedOut={task.IsTimedOut}, IsCanceledOrTimedOut={task.IsCanceledOrTimedOut}, IsCancelRequested={task.IsCancelRequested}");

            // 验证任务是否已超时或被取消
            Assert.True(task.IsCanceledOrTimedOut, "任务应该已被取消或超时");

            // 任务可能因为超时而被标记为IsTimedOut=true
            if (!task.IsCanceled)
            {
                Assert.True(task.IsTimedOut, "如果任务未标记为已取消，则应该标记为已超时");
            }

            // 确认是否捕获到了异常
            Assert.True(exceptionCaught, "应该捕获到 OperationCanceledException 异常");
        }

        /// <summary>
        /// 测试任务重试机制
        /// </summary>
        [Fact]
        public void TaskRetry_ShouldRetryFailedTask()
        {
            // 准备
            int executionCount = 0;
            int maxRetries = 2;
            var manager = TaskManager.Instance;

            // 执行 - 创建一个会失败几次然后成功的任务
            var taskId = manager.StartNew(ct =>
            {
                executionCount++;
                if (executionCount <= maxRetries)
                {
                    throw new InvalidOperationException("模拟失败");
                }
                // 最后一次执行成功
            }, "测试任务", "测试任务重试", -1, maxRetries);

            // 等待任务完成（包括重试）
            manager.WaitForTask(taskId);

            // 验证
            var task = manager.GetTask(taskId);
            Assert.NotNull(task);
            Assert.Equal(maxRetries, task!.RetryCount);
            Assert.True(task.IsSuccessfullyCompleted, "任务最终应该成功完成");
            Assert.Equal(maxRetries + 1, executionCount);
        }

        /// <summary>
        /// 测试异步任务的创建和执行
        /// </summary>
        [Fact]
        public async Task StartNewAsync_ShouldCreateAndExecuteAsyncTask()
        {
            // 准备
            bool taskExecuted = false;
            var manager = TaskManager.Instance;

            // 执行
            var taskId = manager.StartNewAsync(async ct =>
            {
                await Task.Delay(100, ct); // 异步等待
                taskExecuted = true;
            }, "测试任务", "测试异步任务创建");

            // 异步等待任务完成
            await manager.WaitForTaskAsync(taskId);

            // 验证
            Assert.True(taskExecuted, "异步任务应该已经执行");

            var task = manager.GetTask(taskId);
            Assert.NotNull(task);
            Assert.Equal(TaskStatus.RanToCompletion, task!.Status);
        }

        /// <summary>
        /// 测试异步任务的返回值
        /// </summary>
        [Fact]
        public async Task StartNewAsync_WithResult_ShouldCreateAndReturnAsyncValue()
        {
            // 准备
            var expectedResult = "异步结果";
            var manager = TaskManager.Instance;

            // 执行
            var taskId = manager.StartNewAsync(async ct =>
            {
                await Task.Delay(100, ct); // 异步等待
                return expectedResult;
            }, "测试任务", "测试带返回值的异步任务");

            // 异步等待并获取结果
            var result = await manager.GetTaskResultAsync<string>(taskId);

            // 验证
            Assert.Equal(expectedResult, result);
        }

        /// <summary>
        /// 测试按标签获取任务
        /// </summary>
        [Fact]
        public void GetTasksByTag_ShouldReturnMatchingTasks()
        {
            // 准备
            var manager = TaskManager.Instance;
            string testTag = "标签测试";

            // 清理已有任务
            manager.ClearCompletedTasks();

            // 创建3个带相同标签的任务
            var taskIds = new List<Guid>();
            for (int i = 0; i < 3; i++)
            {
                var id = manager.StartNew(ct => Thread.Sleep(100), testTag, $"测试任务 {i}");
                taskIds.Add(id);
            }

            // 创建1个不同标签的任务
            manager.StartNew(ct => Thread.Sleep(100), "其他标签", "不同标签任务");

            // 执行
            var taggedTasks = manager.GetTasksByTag(testTag);

            // 验证
            Assert.Equal(3, taggedTasks.Count);
            Assert.All(taggedTasks, task => Assert.Equal(testTag, task.Tag));

            // 清理
            manager.WaitForAllTasks();
        }

        /// <summary>
        /// 测试任务事件通知
        /// </summary>
        [Fact]
        public async Task TaskEvents_ShouldTriggerAppropriateEvents()
        {
            // 准备
            var manager = TaskManager.Instance;
            bool addedEventTriggered = false;
            bool completedEventTriggered = false;
            TaskManager.TaskEventArgs? lastEventArgs = null;

            // 注册事件
            manager.TaskAdded += (sender, args) =>
            {
                addedEventTriggered = true;
                lastEventArgs = args;
            };

            manager.TaskCompleted += (sender, args) => { completedEventTriggered = true; };

            // 执行
            var taskId = manager.StartNew(ct => Thread.Sleep(50), "事件测试", "测试任务事件");

            // 等待任务完成
            manager.WaitForTask(taskId);

            // 验证
            Assert.True(addedEventTriggered, "应触发任务添加事件");
            Assert.True(completedEventTriggered, "应触发任务完成事件");
            Assert.NotNull(lastEventArgs);
            Assert.Equal(taskId, lastEventArgs!.TaskId);
            Assert.Equal("事件测试", lastEventArgs.TaskTag);

            // 清理事件处理程序（避免影响其他测试）
            // 注意：实际应用中应提供更好的事件清理机制
            var field = typeof(TaskManager).GetField("TaskAdded",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            // 先注销事件处理程序再继续其他测试
            Thread.Sleep(100);
        }

        /// <summary>
        /// 测试任务优先级
        /// </summary>
        [Fact]
        public void TaskPriority_ShouldSetTaskPriority()
        {
            // 准备
            var manager = TaskManager.Instance;

            // 执行 - 创建不同优先级的任务
            var lowPriorityId = manager.StartNew(
                ct => Thread.Sleep(50),
                "优先级测试",
                "低优先级任务",
                -1, 0,
                TaskManager.ManagedTask.TaskPriority.Low);

            var highPriorityId = manager.StartNew(
                ct => Thread.Sleep(50),
                "优先级测试",
                "高优先级任务",
                -1, 0,
                TaskManager.ManagedTask.TaskPriority.High);

            // 验证
            var lowPriorityTask = manager.GetTask(lowPriorityId);
            var highPriorityTask = manager.GetTask(highPriorityId);

            Assert.NotNull(lowPriorityTask);
            Assert.NotNull(highPriorityTask);
            Assert.Equal(TaskManager.ManagedTask.TaskPriority.Low, lowPriorityTask!.Priority);
            Assert.Equal(TaskManager.ManagedTask.TaskPriority.High, highPriorityTask!.Priority);

            // 清理
            manager.WaitForAllTasks();
        }

        /// <summary>
        /// 测试并发任务执行
        /// </summary>
        [Fact]
        public void ConcurrentTasks_ShouldExecuteInParallel()
        {
            // 准备
            var manager = TaskManager.Instance;
            int taskCount = 5;
            var startTime = DateTime.Now;
            var completedTasks = 0;
            var lockObj = new object();
            var manualResetEvent = new ManualResetEventSlim(false);

            // 执行 - 创建多个并发任务
            for (int i = 0; i < taskCount; i++)
            {
                manager.StartNew(ct =>
                    {
                        // 每个任务睡眠相同的时间
                        Thread.Sleep(500);

                        lock (lockObj)
                        {
                            completedTasks++;
                            if (completedTasks == taskCount)
                            {
                                manualResetEvent.Set();
                            }
                        }
                    }, "并发测试", $"并发任务 {i}");
            }

            // 等待所有任务完成
            manualResetEvent.Wait();
            var endTime = DateTime.Now;
            var totalDuration = (endTime - startTime).TotalMilliseconds;

            // 验证 - 如果是并行执行，总时间应该接近单个任务的时间
            // 允许一定的时间误差
            Assert.True(totalDuration < 1000, $"并发任务应该并行执行，总时间: {totalDuration}ms");
            Assert.Equal(taskCount, completedTasks);
        }

        /// <summary>
        /// 测试等待特定标签的所有任务
        /// </summary>
        [Fact]
        public async Task WaitForTasksByTag_ShouldWaitForAllMatchingTasks()
        {
            // 准备
            var manager = TaskManager.Instance;
            string testTag = "等待标签测试";
            int completedCount = 0;
            var lockObj = new object();

            // 创建几个带相同标签的任务，执行时间不同
            for (int i = 0; i < 3; i++)
            {
                int delay = (i + 1) * 100; // 递增延迟
                manager.StartNewAsync(async ct =>
                    {
                        await Task.Delay(delay, ct);
                        lock (lockObj)
                        {
                            completedCount++;
                        }
                    }, testTag, $"延迟任务 {i}");
            }

            // 执行 - 等待所有标签匹配的任务
            await manager.WaitForTasksByTagAsync(testTag);

            // 验证
            Assert.Equal(3, completedCount);
        }

        /// <summary>
        /// 测试获取任务状态摘要
        /// </summary>
        [Fact]
        public void GetTasksStatusSummary_ShouldReturnCorrectSummary()
        {
            // 准备 - 清理并创建一些任务
            var manager = TaskManager.Instance;
            manager.ClearCompletedTasks();

            // 创建一个运行中的任务
            var runningTaskId = manager.StartNew(ct =>
            {
                Thread.Sleep(1000); // 长时间运行
            }, "状态测试", "运行中任务");

            // 创建一个快速完成的任务
            var completedTaskId = manager.StartNew(ct =>
            {
                // 立即返回
            }, "状态测试", "已完成任务");

            // 等待完成任务结束
            manager.WaitForTask(completedTaskId);

            // 执行
            string summary = manager.GetTasksStatusSummary();

            // 验证 - 摘要应包含运行中和已完成的任务
            Assert.Contains("运行中", summary);
            Assert.Contains("已完成", summary);

            // 清理 - 取消运行中的任务
            manager.CancelTask(runningTaskId);
        }
    }
}