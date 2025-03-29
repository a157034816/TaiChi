// See https://aka.ms/new-console-template for more information

using TaiChi.Core.Utils;

Console.WriteLine("Hello, World!");

// 创建一个简单任务
Guid taskId = TaskManager.Instance.StartNew(token => 
{
    for (int i = 0; i < 10; i++)
    {
        token.ThrowIfCancellationRequested();
        Console.WriteLine($"Task running: {i}");
        Thread.Sleep(1000);
    }
}, "BackgroundProcess", "示例后台任务");

// 创建一个有返回值的任务
Guid calculationTaskId = TaskManager.Instance.StartNew<int>(token => 
{
    int sum = 0;
    for (int i = 1; i <= 100; i++)
    {
        token.ThrowIfCancellationRequested();
        sum += i;
    }
    return sum;
}, "Calculation", "计算1到100的和");

// 取消特定标签的所有任务
TaskManager.Instance.CancelTasksByTag("BackgroundProcess");

// 获取所有任务
var allTasks = TaskManager.Instance.GetAllTasks();
foreach (var task in allTasks)
{
    Console.WriteLine($"任务ID: {task.Id}, 标签: {task.Tag}, 描述: {task.Description}, 状态: {task.Status}");
}

// 等待特定任务完成并获取结果
int? result = TaskManager.Instance.GetTaskResult<int>(calculationTaskId);
Console.WriteLine($"计算结果: {result}");
