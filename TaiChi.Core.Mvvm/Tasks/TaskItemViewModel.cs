using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TaiChi.Core.Utils;
using TaiChi.MVVM.Core;

namespace TaiChi.Core.Mvvm.Tasks
{
    /// <summary>
    /// 表示任务管理器中的单个任务
    /// </summary>
    public class TaskItemViewModel : TaiChiObservableObject
    {
        private Guid _id;
        private string _tag;
        private string _description;
        private DateTime _creationTime;
        private TaskStatus _status;
        private bool _isCompleted;
        private bool _isCanceled;
        private bool _isFaulted;
        private string _exceptionMessage;
        private double _progress;

        /// <summary>
        /// 初始化TaskItemViewModel的新实例
        /// </summary>
        /// <param name="managedTask">要包装的任务</param>
        public TaskItemViewModel(TaskManager.ManagedTask managedTask)
        {
            Update(managedTask);
        }

        /// <summary>
        /// 任务的唯一标识符
        /// </summary>
        public Guid Id
        {
            get => _id;
            private set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 任务的标签
        /// </summary>
        public string Tag
        {
            get => _tag;
            private set => SetProperty(ref _tag, value);
        }

        /// <summary>
        /// 任务的描述
        /// </summary>
        public string Description
        {
            get => _description;
            private set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// 任务的创建时间
        /// </summary>
        public DateTime CreationTime
        {
            get => _creationTime;
            private set => SetProperty(ref _creationTime, value);
        }

        /// <summary>
        /// 任务的状态
        /// </summary>
        public TaskStatus Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// 任务是否已完成
        /// </summary>
        public bool IsCompleted
        {
            get => _isCompleted;
            private set => SetProperty(ref _isCompleted, value);
        }

        /// <summary>
        /// 任务是否已取消
        /// </summary>
        public bool IsCanceled
        {
            get => _isCanceled;
            private set => SetProperty(ref _isCanceled, value);
        }

        /// <summary>
        /// 任务是否出现异常
        /// </summary>
        public bool IsFaulted
        {
            get => _isFaulted;
            private set => SetProperty(ref _isFaulted, value);
        }

        /// <summary>
        /// 任务的异常信息
        /// </summary>
        public string ExceptionMessage
        {
            get => _exceptionMessage;
            private set => SetProperty(ref _exceptionMessage, value);
        }

        /// <summary>
        /// 任务的进度（0-100）
        /// </summary>
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        /// <summary>
        /// 任务的运行时间
        /// </summary>
        public TimeSpan RunningTime => DateTime.Now - CreationTime;

        /// <summary>
        /// 任务的状态显示文本
        /// </summary>
        public string StatusText
        {
            get
            {
                if (IsCanceled)
                    return "已取消";
                if (IsFaulted)
                    return "出错";
                if (IsCompleted)
                    return "已完成";

                switch (Status)
                {
                    case TaskStatus.Created:
                        return "已创建";
                    case TaskStatus.WaitingForActivation:
                        return "等待激活";
                    case TaskStatus.WaitingToRun:
                        return "等待运行";
                    case TaskStatus.Running:
                        return "运行中";
                    default:
                        return Status.ToString();
                }
            }
        }

        /// <summary>
        /// 更新任务信息
        /// </summary>
        /// <param name="managedTask">最新的任务信息</param>
        public void Update(TaskManager.ManagedTask managedTask)
        {
            if (managedTask == null)
                throw new ArgumentNullException(nameof(managedTask));

            Id = managedTask.Id;
            Tag = managedTask.Tag;
            Description = managedTask.Description;
            CreationTime = managedTask.CreationTime;
            Status = managedTask.Status;
            IsCompleted = managedTask.IsCompleted;
            IsCanceled = managedTask.IsCanceled;
            IsFaulted = managedTask.IsFaulted;

            // 处理异常信息
            if (managedTask.Exception != null)
            {
                ExceptionMessage = managedTask.Exception.InnerException?.Message ?? managedTask.Exception.Message;
            }
            else
            {
                ExceptionMessage = string.Empty;
            }

            // 可以在这里设置进度（如果有进度报告机制的话）
            // Progress = ...
        }
    }
}