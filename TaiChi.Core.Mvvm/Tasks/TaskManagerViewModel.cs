using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaiChi.Core.Utils;
using TaiChi.MVVM.Core;

namespace TaiChi.Core.Mvvm.Tasks
{
    /// <summary>
    /// TaskManager的MVVM实现，提供任务管理的UI交互功能
    /// </summary>
    public class TaskManagerViewModel : TaiChiObservableObject
    {
        private readonly TaskManager _taskManager;
        private ObservableCollection<TaskItemViewModel> _tasks;
        private string _filterTag = string.Empty;
        private bool _isRefreshing;
        private TaskItemViewModel _selectedTask;

        /// <summary>
        /// 初始化TaskManagerViewModel的新实例
        /// </summary>
        /// <param name="taskManager">要使用的TaskManager实例，如果为null则使用单例</param>
        public TaskManagerViewModel(TaskManager taskManager = null)
        {
            _taskManager = taskManager ?? TaskManager.Instance;
            _tasks = new ObservableCollection<TaskItemViewModel>();

            RefreshCommand = new RelayCommand(RefreshTasks);
            CancelTaskCommand = new RelayCommand<TaskItemViewModel>(CancelTask, CanCancelTask);
            CancelAllTasksCommand = new RelayCommand(CancelAllTasks, CanCancelAllTasks);
            CancelTasksByTagCommand = new RelayCommand(CancelTasksByTag, CanCancelTasksByTag);
            WaitForTaskCommand = new AsyncRelayCommand<TaskItemViewModel>(WaitForTaskAsync, CanWaitForTask);

            RefreshTasks();

            // 定期刷新任务列表
            StartAutoRefresh();
        }

        /// <summary>
        /// 获取任务列表
        /// </summary>
        public ObservableCollection<TaskItemViewModel> Tasks
        {
            get => _tasks;
            private set => SetProperty(ref _tasks, value);
        }

        /// <summary>
        /// 获取或设置筛选标签
        /// </summary>
        public string FilterTag
        {
            get => _filterTag;
            set
            {
                if (SetProperty(ref _filterTag, value))
                {
                    RefreshTasks();
                }
            }
        }

        /// <summary>
        /// 获取或设置是否正在刷新
        /// </summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set => SetProperty(ref _isRefreshing, value);
        }

        /// <summary>
        /// 获取或设置选中的任务
        /// </summary>
        public TaskItemViewModel SelectedTask
        {
            get => _selectedTask;
            set
            {
                SetProperty(ref _selectedTask, value);
                // 通知命令状态可能已更改
                (CancelTaskCommand as RelayCommand<TaskItemViewModel>)?.NotifyCanExecuteChanged();
                (WaitForTaskCommand as AsyncRelayCommand<TaskItemViewModel>)?.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// 刷新任务列表命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 取消选中任务命令
        /// </summary>
        public ICommand CancelTaskCommand { get; }

        /// <summary>
        /// 取消所有任务命令
        /// </summary>
        public ICommand CancelAllTasksCommand { get; }

        /// <summary>
        /// 按标签取消任务命令
        /// </summary>
        public ICommand CancelTasksByTagCommand { get; }

        /// <summary>
        /// 等待任务完成命令
        /// </summary>
        public ICommand WaitForTaskCommand { get; }

        /// <summary>
        /// 刷新任务列表
        /// </summary>
        public void RefreshTasks()
        {
            if (IsRefreshing)
                return;

            IsRefreshing = true;

            try
            {
                IReadOnlyList<TaskManager.ManagedTask> managedTasks;

                if (string.IsNullOrWhiteSpace(FilterTag))
                {
                    managedTasks = _taskManager.GetAllTasks();
                }
                else
                {
                    managedTasks = _taskManager.GetTasksByTag(FilterTag);
                }

                // 创建ViewModel并保留现有的ViewModel
                var existingViewModels = new Dictionary<Guid, TaskItemViewModel>();
                foreach (var task in Tasks)
                {
                    existingViewModels[task.Id] = task;
                }

                var newTasks = new ObservableCollection<TaskItemViewModel>();

                foreach (var managedTask in managedTasks)
                {
                    TaskItemViewModel viewModel;
                    if (existingViewModels.TryGetValue(managedTask.Id, out var existingViewModel))
                    {
                        // 使用现有的ViewModel
                        viewModel = existingViewModel;
                        viewModel.Update(managedTask);
                    }
                    else
                    {
                        // 创建新的ViewModel
                        viewModel = new TaskItemViewModel(managedTask);
                    }

                    newTasks.Add(viewModel);
                }

                Tasks = newTasks;
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 开始自动刷新
        /// </summary>
        private void StartAutoRefresh()
        {
            // 创建一个定时刷新的任务
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(1000); // 每秒刷新一次
                    
                    try
                    {
                        await Task.Run(() => RefreshTasks());
                    }
                    catch
                    {
                        // 忽略刷新过程中的异常
                    }
                    
                    // 检查是否已释放资源
                    if (IsDisposed)
                        break;
                }
            });
        }

        // 命令执行逻辑
        private void CancelTask(TaskItemViewModel task)
        {
            if (task != null)
            {
                _taskManager.CancelTask(task.Id);
                RefreshTasks();
            }
        }

        private bool CanCancelTask(TaskItemViewModel task)
        {
            return task != null && !task.IsCompleted && !task.IsCanceled;
        }

        private void CancelAllTasks()
        {
            _taskManager.CancelAllTasks();
            RefreshTasks();
        }

        private bool CanCancelAllTasks()
        {
            return Tasks.Any(t => !t.IsCompleted && !t.IsCanceled);
        }

        private void CancelTasksByTag()
        {
            if (!string.IsNullOrWhiteSpace(FilterTag))
            {
                _taskManager.CancelTasksByTag(FilterTag);
                RefreshTasks();
            }
        }

        private bool CanCancelTasksByTag()
        {
            return !string.IsNullOrWhiteSpace(FilterTag) && 
                   Tasks.Any(t => !t.IsCompleted && !t.IsCanceled && t.Tag == FilterTag);
        }

        private async Task WaitForTaskAsync(TaskItemViewModel task)
        {
            if (task != null)
            {
                await _taskManager.WaitForTaskAsync(task.Id);
                RefreshTasks();
            }
        }

        private bool CanWaitForTask(TaskItemViewModel task)
        {
            return task != null && !task.IsCompleted;
        }

        // 添加IsDisposed属性
        private bool _isDisposed;
        
        /// <summary>
        /// 获取对象是否已释放
        /// </summary>
        protected bool IsDisposed => _isDisposed;

        // IDisposable实现
        protected override void ReleaseManagedResources()
        {
            base.ReleaseManagedResources();
            _isDisposed = true;
        }
    }
}