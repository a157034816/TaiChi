using System;
using System.Globalization;
using System.Windows;
using TaiChi.I18n;
using TaiChi.I18n.Wpf;
using System.Collections.Generic;

namespace TaiChi.I18n.WpfExample
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 本地化服务
        /// </summary>
        public static ILocalizationService LocalizationService { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化本地化服务
            InitializeLocalization();

            // 初始化WPF本地化扩展
            WpfLocalizationExtensions.InitializeWpfLocalization(LocalizationService);
        }

        private void InitializeLocalization()
        {
            // 创建本地化配置
            var config = new LocalizationConfig
            {
                DefaultCulture = new CultureInfo("zh-CN"),
                SupportedCultures = new List<CultureInfo>
                {
                    new CultureInfo("zh-CN"),
                    new CultureInfo("en-US")
                },
                ResourcePath = "Resources",
                EnableFileMonitoring = true,
                EnableCache = true,
                CacheTimeout = 60 * 60
            };

            // 创建本地化服务
            LocalizationService = new LocalizationService(config);

            // 监听语言变化事件
            LocalizationService.LanguageChanged += OnLanguageChanged;
        }

        private void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            // 语言变化时可以执行的额外逻辑
            Console.WriteLine($"语言已切换到: {culture.DisplayName}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // 清理资源
            if (LocalizationService != null)
            {
                LocalizationService.LanguageChanged -= OnLanguageChanged;
                LocalizationService.Dispose();
            }

            base.OnExit(e);
        }
    }
}
