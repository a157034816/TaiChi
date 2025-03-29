using System;
using System.Threading;
using System.Threading.Tasks;
using TaiChi.Core.Utils;

namespace TaiChi.Core.Mvvm.Tasks
{
    /// <summary>
    /// TaskManagerViewModel的使用示例
    /// </summary>
    public static class TaskManagerExample
    {
        /// <summary>
        /// 演示TaskManagerViewModel的基本用法
        /// </summary>
        /// <remarks>
        /// 此方法仅用于展示如何在应用程序中使用TaskManagerViewModel
        /// </remarks>
        public static void DemonstrateTaskManagerViewModel()
        {
            // 创建ViewModel实例
            var viewModel = new TaskManagerViewModel();

            // 示例：创建并监控任务
            // 实际应用中，这通常由UI绑定来完成

            // 1. 启动一个简单任务
            TaskManager.Instance.StartNew(token =>
            {
                for (int i = 0; i < 10; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // 模拟工作
                    Thread.Sleep(500);
                }
            }, "DemoTask", "演示任务");

            // 2. 启动带进度报告的任务
            var taskId = TaskManager.Instance.StartNewWithProgress((token, progress) =>
            {
                for (int i = 0; i <= 100; i += 10)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // 报告进度
                    progress.Report(i);

                    // 模拟工作
                    Thread.Sleep(500);
                }
            }, "ProgressTask", "带进度的任务");

            // 3. 启动异步任务
            TaskManager.Instance.StartNewAsyncWithProgress(async (token, progress) =>
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    if (token.IsCancellationRequested)
                        break;

                    // 报告进度和消息
                    progress.Report((i, $"处理中...{i}%"));

                    // 模拟异步工作
                    await Task.Delay(300, token);
                }
            }, "AsyncTask", "异步任务");

            // 示例：使用命令（在实际应用中，通常由UI触发）

            // 等待2秒后刷新任务列表
            Task.Delay(2000).Wait();
            viewModel.RefreshTasks();

            // 如果有任务，选择第一个任务并取消它
            if (viewModel.Tasks.Count > 0)
            {
                viewModel.SelectedTask = viewModel.Tasks[0];
                if (viewModel.CancelTaskCommand.CanExecute(viewModel.SelectedTask))
                {
                    viewModel.CancelTaskCommand.Execute(viewModel.SelectedTask);
                }
            }

            // 等待所有任务完成
            TaskManager.Instance.WaitForAllTasks();
        }

        /// <summary>
        /// 示例：如何在实际MVVM应用中使用
        /// </summary>
        /// <remarks>
        /// 此代码仅作为参考，不会被实际调用
        /// </remarks>
        public static void SampleUsageInMvvmApplication()
        {
            /*
            // 在ViewModel中创建TaskManagerViewModel
            public class MainViewModel : TaiChiObservableObject
            {
                private TaskManagerViewModel _taskManager;
                
                public MainViewModel()
                {
                    _taskManager = new TaskManagerViewModel();
                    StartTaskCommand = new RelayCommand(StartTask);
                }
                
                public TaskManagerViewModel TaskManager => _taskManager;
                
                public ICommand StartTaskCommand { get; }
                
                private void StartTask()
                {
                    // 启动一个示例任务
                    TaskManager.Instance.StartNewWithProgress((token, progress) =>
                    {
                        for (int i = 0; i <= 100; i += 5)
                        {
                            if (token.IsCancellationRequested)
                                break;
                                
                            progress.Report((i, $"处理任务 {i}%"));
                            Thread.Sleep(100);
                        }
                    }, "UserTask", "用户启动的任务");
                    
                    // 刷新任务列表
                    _taskManager.RefreshTasks();
                }
            }
            
            // 在XAML中绑定
            <Window ...>
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="300"/>
                    </Grid.ColumnDefinitions>
                    
                    <!-- 主内容 -->
                    <StackPanel>
                        <Button Content="启动任务" Command="{Binding StartTaskCommand}" />
                    </StackPanel>
                    
                    <!-- 任务管理器面板 -->
                    <ListView Grid.Column="1" 
                              ItemsSource="{Binding TaskManager.Tasks}" 
                              SelectedItem="{Binding TaskManager.SelectedTask}">
                        <ListView.ItemTemplate>
                            <DataTemplate>
                                <Grid>
                                    <Grid.RowDefinitions>
                                        <RowDefinition Height="Auto"/>
                                        <RowDefinition Height="Auto"/>
                                        <RowDefinition Height="Auto"/>
                                    </Grid.RowDefinitions>
                                    
                                    <TextBlock Text="{Binding Description}" FontWeight="Bold"/>
                                    <StackPanel Grid.Row="1" Orientation="Horizontal">
                                        <TextBlock Text="状态: "/>
                                        <TextBlock Text="{Binding StatusText}"/>
                                    </StackPanel>
                                    <ProgressBar Grid.Row="2" Value="{Binding Progress}" Maximum="100" />
                                </Grid>
                            </DataTemplate>
                        </ListView.ItemTemplate>
                    </ListView>
                    
                    <StackPanel Grid.Column="1" VerticalAlignment="Bottom">
                        <Button Content="刷新" Command="{Binding TaskManager.RefreshCommand}" />
                        <Button Content="取消任务" 
                                Command="{Binding TaskManager.CancelTaskCommand}" 
                                CommandParameter="{Binding TaskManager.SelectedTask}" />
                        <Button Content="取消全部" 
                                Command="{Binding TaskManager.CancelAllTasksCommand}" />
                    </StackPanel>
                </Grid>
            </Window>
            */
        }
    }
}