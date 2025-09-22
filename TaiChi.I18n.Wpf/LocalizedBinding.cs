using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace TaiChi.I18n.Wpf
{
    /// <summary>
    /// WPF本地化数据绑定标记扩展
    /// 实现XAML中的本地化绑定语法
    /// </summary>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class LocalizedBinding : MarkupExtension
    {
        private static ILocalizationService? _localizationService;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        /// <summary>
        /// 本地化键
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 格式化参数
        /// </summary>
        public object[]? Args { get; set; }

        /// <summary>
        /// 绑定模式
        /// </summary>
        public BindingMode Mode { get; set; } = BindingMode.OneWay;

        /// <summary>
        /// 更新源触发器
        /// </summary>
        public UpdateSourceTrigger UpdateSourceTrigger { get; set; } = UpdateSourceTrigger.PropertyChanged;

        /// <summary>
        /// 转换器
        /// </summary>
        public IValueConverter? Converter { get; set; }

        /// <summary>
        /// 转换器参数
        /// </summary>
        public object? ConverterParameter { get; set; }

        /// <summary>
        /// 转换器文化
        /// </summary>
        public CultureInfo? ConverterCulture { get; set; }

        /// <summary>
        /// 字符串格式
        /// </summary>
        public string? StringFormat { get; set; }

        /// <summary>
        /// 目标为空时的值
        /// </summary>
        public object? TargetNullValue { get; set; }

        /// <summary>
        /// 回退值
        /// </summary>
        public object? FallbackValue { get; set; }

        /// <summary>
        /// 元素名称（用于ElementName绑定）
        /// </summary>
        public string? ElementName { get; set; }

        /// <summary>
        /// 相对源
        /// </summary>
        public RelativeSource? RelativeSource { get; set; }

        /// <summary>
        /// 源路径
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// 获取或设置本地化服务
        /// </summary>
        public static ILocalizationService? LocalizationService
        {
            get => _localizationService;
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
                        _isInitialized = true;
                    }
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public LocalizedBinding()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">本地化键</param>
        public LocalizedBinding(string key)
        {
            Key = key ?? string.Empty;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">本地化键</param>
        /// <param name="args">格式化参数</param>
        public LocalizedBinding(string key, params object[] args)
        {
            Key = key ?? string.Empty;
            Args = args;
        }

        /// <summary>
        /// 提供绑定值
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>绑定对象</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            // 确保服务已初始化
            EnsureInitialized();

            // 创建绑定
            var binding = new Binding
            {
                Source = new LocalizedBindingSource(Key, Args),
                Path = new PropertyPath("Value"),
                Mode = Mode,
                UpdateSourceTrigger = UpdateSourceTrigger,
                Converter = Converter,
                ConverterParameter = ConverterParameter,
                StringFormat = StringFormat,
                FallbackValue = FallbackValue ?? Key
            };

            // 仅在非空时设置，以避免 WPF 在处理 TargetNullValue=null 时的 NullReferenceException
            if (ConverterCulture != null)
            {
                binding.ConverterCulture = ConverterCulture;
            }

            if (TargetNullValue != null)
            {
                binding.TargetNullValue = TargetNullValue;
            }

            if (!string.IsNullOrEmpty(ElementName))
            {
                binding.ElementName = ElementName;
            }

            if (RelativeSource != null)
            {
                binding.RelativeSource = RelativeSource;
            }

            if (!string.IsNullOrEmpty(Path))
            {
                binding.Path = new PropertyPath(Path);
            }

            // 返回绑定
            return binding.ProvideValue(serviceProvider);
        }

        /// <summary>
        /// 确保本地化服务已初始化
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                // 尝试从应用程序资源中获取本地化服务
                if (Application.Current != null && Application.Current.Resources.Contains("LocalizationService"))
                {
                    LocalizationService = Application.Current.Resources["LocalizationService"] as ILocalizationService;
                }
                else
                {
                    // 尝试从WpfExtensions中获取
                    var service = WpfLocalizationExtensions.LocalizationService;
                    if (service != null)
                    {
                        LocalizationService = service;
                    }
                    else
                    {
                        // 如果仍未初始化，使用默认服务
                        var config = LocalizationConfig.Default;
                        LocalizationService = new LocalizationService(config);
                    }
                }
            }
        }

        /// <summary>
        /// 语言变化事件处理
        /// </summary>
        private static void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            // 更新所有绑定源
            LocalizedBindingSource.UpdateAllSources();
        }

        /// <summary>
        /// 更新所有绑定
        /// </summary>
        public static void UpdateAllBindings()
        {
            LocalizedBindingSource.UpdateAllSources();
        }

        /// <summary>
        /// 重置静态状态
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                if (_localizationService != null)
                {
                    _localizationService.LanguageChanged -= OnLanguageChanged;
                    _localizationService = null;
                }
                _isInitialized = false;
            }
        }
    }

    /// <summary>
    /// 本地化绑定源
    /// </summary>
    internal class LocalizedBindingSource : INotifyPropertyChanged
    {
        private readonly string _key;
        private readonly object[]? _args;
        private string? _value;
        private static readonly object _lock = new object();
        private static List<WeakReference<LocalizedBindingSource>>? _sources;

        /// <summary>
        /// 当前值
        /// </summary>
        public string? Value
        {
            get => _value;
            private set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">本地化键</param>
        /// <param name="args">格式化参数</param>
        public LocalizedBindingSource(string key, object[]? args)
        {
            _key = key ?? string.Empty;
            _args = args;

            // 注册源
            RegisterSource(this);

            // 初始化值
            UpdateValue();

            // 订阅语言变化事件
            var service = LocalizedBinding.LocalizationService;
            if (service != null)
            {
                service.LanguageChanged += OnLanguageChanged;
            }
        }

        /// <summary>
        /// 注册绑定源
        /// </summary>
        /// <param name="source">绑定源</param>
        private static void RegisterSource(LocalizedBindingSource source)
        {
            lock (_lock)
            {
                _sources ??= new List<WeakReference<LocalizedBindingSource>>();

                // 清理失效的引用
                _sources.RemoveAll(r => !r.TryGetTarget(out _));

                // 添加新引用
                _sources.Add(new WeakReference<LocalizedBindingSource>(source));
            }
        }

        /// <summary>
        /// 更新所有源
        /// </summary>
        public static void UpdateAllSources()
        {
            lock (_lock)
            {
                if (_sources == null) return;

                var validSources = new List<LocalizedBindingSource>();

                foreach (var weakRef in _sources)
                {
                    if (weakRef.TryGetTarget(out var source))
                    {
                        validSources.Add(source);
                    }
                }

                // 更新所有有效源
                foreach (var source in validSources)
                {
                    source.UpdateValue();
                }

                // 如果有失效引用，清理列表
                if (validSources.Count != _sources.Count)
                {
                    _sources.Clear();
                    foreach (var source in validSources)
                    {
                        _sources.Add(new WeakReference<LocalizedBindingSource>(source));
                    }
                }
            }
        }

        /// <summary>
        /// 语言变化事件处理
        /// </summary>
        private void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            UpdateValue();
        }

        /// <summary>
        /// 更新值
        /// </summary>
        private void UpdateValue()
        {
            try
            {
                var service = LocalizedBinding.LocalizationService;
                if (service != null)
                {
                    if (_args == null || _args.Length == 0)
                    {
                        Value = service.GetString(_key);
                    }
                    else
                    {
                        Value = service.GetFormattedString(_key, _args);
                    }
                }
                else
                {
                    Value = _key;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update localized binding for key '{_key}': {ex.Message}");
                Value = _key;
            }
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 析构函数
        /// </summary>
        ~LocalizedBindingSource()
        {
            // 从静态列表中移除引用
            lock (_lock)
            {
                if (_sources != null)
                {
                    _sources.RemoveAll(r => !r.TryGetTarget(out var target) || target == this);
                }
            }
        }
    }

    /// <summary>
    /// 本地化图片绑定标记扩展
    /// </summary>
    [MarkupExtensionReturnType(typeof(Binding))]
    public class LocalizedImageBinding : MarkupExtension
    {
        /// <summary>
        /// 图片资源键
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 构造函数
        /// </summary>
        public LocalizedImageBinding()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">图片资源键</param>
        public LocalizedImageBinding(string key)
        {
            Key = key ?? string.Empty;
        }

        /// <summary>
        /// 提供绑定值
        /// </summary>
        /// <param name="serviceProvider">服务提供者</param>
        /// <returns>绑定对象</returns>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding
            {
                Source = new LocalizedImageBindingSource(Key),
                Path = new PropertyPath("Value"),
                Mode = BindingMode.OneWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                Converter = new ImageResourceConverter(),
                FallbackValue = Key
            };

            return binding.ProvideValue(serviceProvider);
        }
    }

    /// <summary>
    /// 本地化图片绑定源
    /// </summary>
    internal class LocalizedImageBindingSource : INotifyPropertyChanged
    {
        private readonly string _key;
        private string? _value;
        private static List<WeakReference<LocalizedImageBindingSource>>? _sources;

        /// <summary>
        /// 当前值
        /// </summary>
        public string? Value
        {
            get => _value;
            private set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="key">图片资源键</param>
        public LocalizedImageBindingSource(string key)
        {
            _key = key ?? string.Empty;

            RegisterSource(this);
            UpdateValue();

            var service = LocalizedBinding.LocalizationService;
            if (service != null)
            {
                service.LanguageChanged += OnLanguageChanged;
            }
        }

        /// <summary>
        /// 注册绑定源
        /// </summary>
        /// <param name="source">绑定源</param>
        private static void RegisterSource(LocalizedImageBindingSource source)
        {
            _sources ??= new List<WeakReference<LocalizedImageBindingSource>>();
            _sources.RemoveAll(r => !r.TryGetTarget(out _));
            _sources.Add(new WeakReference<LocalizedImageBindingSource>(source));
        }

        /// <summary>
        /// 语言变化事件处理
        /// </summary>
        private void OnLanguageChanged(object? sender, CultureInfo culture)
        {
            UpdateValue();
        }

        /// <summary>
        /// 更新值
        /// </summary>
        private void UpdateValue()
        {
            try
            {
                var service = LocalizedBinding.LocalizationService;
                Value = service?.GetImagePath(_key);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update localized image binding for key '{_key}': {ex.Message}");
                Value = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
