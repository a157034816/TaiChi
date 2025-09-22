using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using TaiChi.I18n;
using TaiChi.I18n.Wpf;
using System.Collections.Generic;
using System.Linq;

namespace TaiChi.I18n.WpfExample
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILocalizationService _localizationService;

        public MainWindow()
        {
            InitializeComponent();
            _localizationService = App.LocalizationService;

            // 设置数据上下文
            DataContext = this;

            // 初始化语言选择
            InitializeLanguageComboBox();

            // 注册本地化更新
            RegisterForLocalizationUpdates();
        }

        private void InitializeLanguageComboBox()
        {
            // 添加支持的语言
            LanguageComboBox.Items.Add(new ComboBoxItem
            {
                Content = "中文 (简体)",
                Tag = "zh-CN"
            });
            LanguageComboBox.Items.Add(new ComboBoxItem
            {
                Content = "English",
                Tag = "en-US"
            });

            // 设置当前选中的语言
            var currentCulture = _localizationService.CurrentCulture.Name;
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == currentCulture)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void RegisterForLocalizationUpdates()
        {
            // 注册窗口中的控件以接收本地化更新
            this.RegisterLocalization();
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var cultureName = selectedItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(cultureName))
                {
                    var culture = new CultureInfo(cultureName);
                    _localizationService.SetLanguage(culture);
                }
            }
        }

        private void ShowMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var message = _localizationService.GetString("Messages.WelcomeMessage");
            var title = _localizationService.GetString("Messages.InfoTitle");

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowFormattedMessageButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = UserNameTextBox.Text;
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = _localizationService.GetString("General.DefaultUserName");
            }

            var message = _localizationService.GetFormattedString("Messages.PersonalizedWelcome", userName);
            var title = _localizationService.GetString("Messages.InfoTitle");

            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ValidateFormButton_Click(object sender, RoutedEventArgs e)
        {
            var userName = UserNameTextBox.Text;
            var email = EmailTextBox.Text;

            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(userName))
            {
                errors.Add(_localizationService.GetString("Validation.UserNameRequired"));
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add(_localizationService.GetString("Validation.EmailRequired"));
            }
            else if (!IsValidEmail(email))
            {
                errors.Add(_localizationService.GetString("Validation.EmailInvalid"));
            }

            if (errors.Any())
            {
                var title = _localizationService.GetString("Messages.ValidationErrorTitle");
                var message = string.Join("\n", errors);
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                var title = _localizationService.GetString("Messages.SuccessTitle");
                var message = _localizationService.GetString("Messages.ValidationSuccess");
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private void GenerateResourcesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 演示资源生成工具的使用
                var generator = new ResourceGenerator();
                var config = new ResourceGenerator.GeneratorConfig
                {
                    ScanDirectories = new List<string> { "." },
                    OutputDirectory = "Resources\\Generated",
                    GenerateComments = true,
                    GroupByCategory = true
                };

                // 这里只是演示，实际中应该使用异步调用
                var task = generator.GenerateResourceFilesAsync(config);
                task.Wait();

                var report = task.Result;
                var message = report.Success ?
                    $"资源文件生成成功！\n发现 {report.DiscoveredKeys} 个键\n生成 {report.GeneratedFiles.Count} 个文件" :
                    $"资源文件生成失败：{report.Message}";

                MessageBox.Show(message, "资源生成", MessageBoxButton.OK,
                    report.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"生成资源时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ValidateResourcesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 演示资源验证工具的使用
                var validator = new ResourceValidator();
                var config = new ResourceValidator.ValidationConfig
                {
                    ResourceDirectory = "Resources",
                    CheckEmptyValues = true,
                    CheckDuplicateKeys = true,
                    ValidateFormatStrings = true
                };

                // 这里只是演示，实际中应该使用异步调用
                var task = validator.ValidateAsync(config);
                task.Wait();

                var report = task.Result;
                var message = $"验证完成！\n错误: {report.ErrorCount}\n警告: {report.WarningCount}\n信息: {report.InfoCount}";

                MessageBox.Show(message, "资源验证", MessageBoxButton.OK,
                    report.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"验证资源时发生错误：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
