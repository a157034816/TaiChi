using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace TaiChi.I18n.Wpf
{
    /// <summary>
    /// WPF应用本地化扩展方法
    /// 实现绑定扩展、资源自动更新、XAML支持
    /// </summary>
    public static class WpfLocalizationExtensions
    {
        private static ILocalizationService? _localizationService;
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取或设置本地化服务
        /// </summary>
        public static ILocalizationService LocalizationService
        {
            get => _localizationService!;
            set
            {
                lock (_lock)
                {
                    if (_localizationService != null)
                    {
                        _localizationService.LanguageChanged -= OnLanguageChanged;
                    }

                    _localizationService = value;

                    if (_localizationService != null)
                    {
                        _localizationService.LanguageChanged += OnLanguageChanged;
                    }
                }
            }
        }

        /// <summary>
        /// 语言变化事件处理
        /// </summary>
        private static void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            // 在主线程上更新所有绑定的本地化资源
            if (Application.Current.Dispatcher.CheckAccess())
            {
                UpdateAllLocalizations();
            }
            else
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(UpdateAllLocalizations));
            }
        }

        /// <summary>
        /// 更新所有本地化绑定
        /// </summary>
        private static void UpdateAllLocalizations()
        {
            // 通知所有本地化绑定更新
            LocalizedBinding.UpdateAllBindings();
        }

        /// <summary>
        /// 获取本地化字符串
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static string Localize(this string key, params object[] args)
        {
            if (_localizationService == null)
                return key;

            try
            {
                return _localizationService.GetFormattedString(key, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Localization failed for key '{key}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// 获取本地化字符串（指定文化）
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <param name="args">格式化参数</param>
        /// <returns>本地化字符串</returns>
        public static string Localize(this string key, CultureInfo culture, params object[] args)
        {
            if (_localizationService == null)
                return key;

            try
            {
                return _localizationService.GetFormattedString(key, culture, args);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Localization failed for key '{key}' with culture '{culture.Name}': {ex.Message}");
                return key;
            }
        }

        /// <summary>
        /// 获取本地化图片路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>图片路径或null</returns>
        public static string? LocalizeImage(this string key)
        {
            if (_localizationService == null)
                return null;

            try
            {
                return _localizationService.GetImagePath(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Image localization failed for key '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 创建本地化图片位图
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>图片位图或null</returns>
        public static BitmapImage? LocalizeBitmap(this string key)
        {
            var imagePath = key.LocalizeImage();
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze(); // 冻结以便跨线程访问
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load bitmap for key '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取本地化音频路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <returns>音频路径或null</returns>
        public static string? LocalizeAudio(this string key)
        {
            if (_localizationService == null)
                return null;

            try
            {
                return _localizationService.GetAudioPath(key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audio localization failed for key '{key}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 为FrameworkElement设置本地化资源键
        /// </summary>
        /// <param name="element">FrameworkElement</param>
        /// <param name="key">资源键</param>
        public static void SetLocalizationKey(this FrameworkElement element, string key)
        {
            if (element == null || string.IsNullOrEmpty(key))
                return;

            element.Tag = new LocalizationTag(key, null);
            UpdateElementLocalization(element);
        }

        /// <summary>
        /// 为FrameworkElement设置本地化资源键和参数
        /// </summary>
        /// <param name="element">FrameworkElement</param>
        /// <param name="key">资源键</param>
        /// <param name="args">格式化参数</param>
        public static void SetLocalizationKey(this FrameworkElement element, string key, params object[] args)
        {
            if (element == null || string.IsNullOrEmpty(key))
                return;

            element.Tag = new LocalizationTag(key, args);
            UpdateElementLocalization(element);
        }

        /// <summary>
        /// 更新元素的本地化内容
        /// </summary>
        /// <param name="element">FrameworkElement</param>
        private static void UpdateElementLocalization(FrameworkElement element)
        {
            if (element.Tag is LocalizationTag tag && !string.IsNullOrEmpty(tag.Key))
            {
                try
                {
                    var localizedText = tag.Key.Localize(tag.Args ?? Array.Empty<object>());

                    // 根据元素类型设置本地化内容
                    switch (element)
                    {
                        case Window window:
                            window.Title = localizedText;
                            break;
                        case Button button:
                            button.Content = localizedText;
                            break;
                        case Label label:
                            label.Content = localizedText;
                            break;
                        case TextBlock textBlock:
                            textBlock.Text = localizedText;
                            break;
                        case TextBox textBox:
                            if (tag.Args == null || tag.Args.Length == 0)
                                textBox.Text = localizedText;
                            break;
                        case TabItem tabItem:
                            tabItem.Header = localizedText;
                            break;
                        case HeaderedContentControl headeredControl:
                            headeredControl.Header = localizedText;
                            break;
                        case MenuItem menuItem:
                            menuItem.Header = localizedText;
                            break;
                        case ContentControl contentControl:
                            contentControl.Content = localizedText;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update element localization: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 注册元素的本地化更新
        /// </summary>
        /// <param name="element">FrameworkElement</param>
        public static void RegisterLocalization(this FrameworkElement element)
        {
            if (element == null) return;

            // 注册语言变化事件
            if (_localizationService != null)
            {
                element.Unloaded -= OnElementUnloaded;
                element.Unloaded += OnElementUnloaded;
            }

            // 首次更新
            UpdateElementLocalization(element);
        }

        /// <summary>
        /// 元素卸载事件处理
        /// </summary>
        private static void OnElementUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element)
            {
                element.Unloaded -= OnElementUnloaded;
            }
        }

        /// <summary>
        /// 设置数据上下文的本地化绑定
        /// </summary>
        /// <param name="frameworkElement">FrameworkElement</param>
        /// <param name="key">资源键</param>
        /// <returns>绑定对象</returns>
        public static Binding SetLocalizationBinding(this FrameworkElement frameworkElement, string key)
        {
            var binding = new Binding
            {
                Source = new LocalizedBindingSource(key, null),
                Path = new PropertyPath("Value"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            frameworkElement.SetBinding(FrameworkElement.DataContextProperty, binding);
            return binding;
        }

        /// <summary>
        /// 初始化WPF本地化
        /// </summary>
        /// <param name="localizationService">本地化服务</param>
        /// <param name="config">配置</param>
        public static void InitializeWpfLocalization(ILocalizationService localizationService, LocalizationConfig? config = null)
        {
            if (localizationService == null)
                throw new ArgumentNullException(nameof(localizationService));

            LocalizationService = localizationService;

            if (config != null)
            {
                localizationService.EnableFileMonitoring(config.EnableFileMonitoring);
            }

            // 设置应用程序的文化信息
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CultureInfo.CurrentUICulture = localizationService.CurrentCulture;
                    CultureInfo.CurrentCulture = localizationService.CurrentCulture;
                }));
            }
        }

        /// <summary>
        /// 切换语言并更新UI
        /// </summary>
        /// <param name="cultureName">文化名称</param>
        /// <returns>是否成功</returns>
        public static bool SwitchLanguage(string cultureName)
        {
            if (_localizationService == null)
                return false;

            var success = _localizationService.SetLanguage(cultureName);
            if (success && Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CultureInfo.CurrentUICulture = _localizationService.CurrentCulture;
                    CultureInfo.CurrentCulture = _localizationService.CurrentCulture;
                }));
            }
            return success;
        }

        /// <summary>
        /// 获取当前支持的所有语言
        /// </summary>
        /// <returns>支持的文化列表</returns>
        public static List<CultureInfo> GetSupportedCultures()
        {
            return _localizationService?.GetSupportedCultures() ?? new List<CultureInfo>();
        }

        /// <summary>
        /// 清理WPF本地化资源
        /// </summary>
        public static void Cleanup()
        {
            if (_localizationService != null)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
                _localizationService = null;
            }
        }
    }

    /// <summary>
    /// 本地化标签
    /// </summary>
    internal class LocalizationTag
    {
        public string Key { get; }
        public object[]? Args { get; }

        public LocalizationTag(string key, object[]? args)
        {
            Key = key;
            Args = args;
        }
    }
}