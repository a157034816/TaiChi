using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TaiChi.Wpf.Tour
{
    /// <summary>
    /// 气泡面板，用于显示单步引导信息与操作按钮。
    /// </summary>
    public partial class TourBubble : UserControl
    {
        /// <summary>
        /// 标题文本。
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(TourBubble), new PropertyMetadata(string.Empty));

        /// <summary>
        /// 说明文本。
        /// </summary>
        public static readonly DependencyProperty DescriptionProperty =
            DependencyProperty.Register(nameof(Description), typeof(string), typeof(TourBubble), new PropertyMetadata(string.Empty));

        /// <summary>
        /// 步骤显示文本，例如："(2/5)"。
        /// </summary>
        public static readonly DependencyProperty StepTextProperty =
            DependencyProperty.Register(nameof(StepText), typeof(string), typeof(TourBubble), new PropertyMetadata(string.Empty));

        /// <summary>
        /// 下一步命令。
        /// </summary>
        public static readonly DependencyProperty NextCommandProperty =
            DependencyProperty.Register(nameof(NextCommand), typeof(ICommand), typeof(TourBubble));

        /// <summary>
        /// 上一步命令。
        /// </summary>
        public static readonly DependencyProperty PrevCommandProperty =
            DependencyProperty.Register(nameof(PrevCommand), typeof(ICommand), typeof(TourBubble));

        /// <summary>
        /// 关闭命令。
        /// </summary>
        public static readonly DependencyProperty CloseCommandProperty =
            DependencyProperty.Register(nameof(CloseCommand), typeof(ICommand), typeof(TourBubble));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public string StepText
        {
            get => (string)GetValue(StepTextProperty);
            set => SetValue(StepTextProperty, value);
        }

        public ICommand? NextCommand
        {
            get => (ICommand?)GetValue(NextCommandProperty);
            set => SetValue(NextCommandProperty, value);
        }

        public ICommand? PrevCommand
        {
            get => (ICommand?)GetValue(PrevCommandProperty);
            set => SetValue(PrevCommandProperty, value);
        }

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }

        public TourBubble()
        {
            InitializeComponent();
        }
    }
}
