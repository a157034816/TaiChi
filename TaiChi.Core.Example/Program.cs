// See https://aka.ms/new-console-template for more information

using TaiChi.Core.Utils;
using System.Threading.Tasks;

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

// 最佳实践示例：创建任务并立即保存对Task的引用
Console.WriteLine("\n=== 最佳实践示例 ===");
Guid newCalcTaskId = TaskManager.Instance.StartNew<int>(token => 
{
    Console.WriteLine("开始计算5到15的和...");
    int sum = 0;
    for (int i = 5; i <= 15; i++)
    {
        token.ThrowIfCancellationRequested();
        sum += i;
        Thread.Sleep(100); // 模拟计算过程
    }
    Console.WriteLine("计算完成！");
    return sum;
}, "NewCalculation", "计算5到15的和");

// 立即获取Task引用，避免之后任务完成被清理的问题
var managedTask = TaskManager.Instance.GetTask(newCalcTaskId);
if (managedTask?.Task is Task<int> calcTask)
{
    // 使用ConfigureAwait(false)避免死锁
    calcTask.ContinueWith(t => 
    {
        if (t.IsCompletedSuccessfully)
        {
            Console.WriteLine($"✅ 最佳实践结果: 5到15的和 = {t.Result}");
        }
        else if (t.IsFaulted)
        {
            Console.WriteLine($"❌ 最佳实践任务执行出错: {t.Exception?.InnerException?.Message}");
        }
        else if (t.IsCanceled)
        {
            Console.WriteLine("❌ 最佳实践任务被取消");
        }
    }, TaskScheduler.Current);
}
else
{
    Console.WriteLine("❌ 无法获取任务引用");
}

// 创建一个有返回值的任务（可能会在完成后被清理）
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

try 
{
    // 方法1：使用GetTaskResult方法（如果任务已经完成并且被清理，会返回默认值）
    int? result = TaskManager.Instance.GetTaskResult<int>(calculationTaskId, defaultValue: -1);
    Console.WriteLine($"计算结果 (GetTaskResult): {(result != -1 ? result.ToString() : "任务已被清理或未完成")}");
    
    // 方法2：使用GetTaskResultAsync方法（更健壮，可以等待任务完成）
    // 注意：这个方法如果任务已经被清理，也会返回默认值
    var asyncResult = TaskManager.Instance.GetTaskResultAsync<int>(calculationTaskId, defaultValue: -1).GetAwaiter().GetResult();
    Console.WriteLine($"计算结果 (GetTaskResultAsync): {(asyncResult != -1 ? asyncResult.ToString() : "任务已被清理或未完成")}");
    
    // 方法3：使用Task.WhenAny在任务创建后立即开始监控其完成
    // 这种方式需要在任务创建后立即实施，不能在任务可能已完成后使用
    Console.WriteLine("推荐的最佳实践方式：");
    Console.WriteLine("创建任务后立即存储Task对象引用，并使用await Task.WhenAny监控完成");
    Console.WriteLine(@"
    // 示例代码:
    // var calculationTask = (Task<int>)TaskManager.Instance.GetTask(calculationTaskId).Task;
    // var completedTask = await Task.WhenAny(calculationTask, Task.Delay(timeout));
    // if (completedTask == calculationTask)
    // {
    //     var result = calculationTask.Result;
    //     Console.WriteLine($""计算结果: {result}"");
    // }
    ");
}
catch (Exception ex)
{
    Console.WriteLine($"获取任务结果时出错: {ex.Message}");
}

Console.ReadKey();