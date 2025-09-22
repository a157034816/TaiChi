using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TaiChi.I18n.Wpf
{
    /// <summary>
    /// 图片资源转换器
    /// 支持本地化图片资源的加载和转换
    /// </summary>
    [ValueConversion(typeof(string), typeof(BitmapSource))]
    public class ImageResourceConverter : IValueConverter
    {
        private static ILocalizationService? _localizationService;

        /// <summary>
        /// 获取或设置本地化服务
        /// </summary>
        public static ILocalizationService? LocalizationService
        {
            get => _localizationService;
            set => _localizationService = value;
        }

        /// <summary>
        /// 转换图片资源路径为图片位图
        /// </summary>
        /// <param name="value">图片资源路径或键</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">文化信息</param>
        /// <returns>转换后的图片位图</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return GetDefaultImage();

            var imagePath = value as string;
            if (string.IsNullOrEmpty(imagePath))
                return GetDefaultImage();

            try
            {
                // 如果是资源键，先获取本地化路径
                if (IsResourceKey(imagePath))
                {
                    var localizedPath = GetLocalizedImagePath(imagePath, culture);
                    if (string.IsNullOrEmpty(localizedPath))
                        return GetDefaultImage();

                    imagePath = localizedPath;
                }

                // 加载图片
                return LoadBitmapFromPath(imagePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to convert image resource '{imagePath}': {ex.Message}");
                return GetDefaultImage();
            }
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("ImageResourceConverter does not support conversion back from BitmapSource to string.");
        }

        /// <summary>
        /// 判断是否为资源键
        /// </summary>
        /// <param name="path">路径或键</param>
        /// <returns>是否为资源键</returns>
        private bool IsResourceKey(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // 如果路径不包含扩展名，认为是资源键
            return !Path.HasExtension(path) ||
                   path.StartsWith("Resources.", StringComparison.OrdinalIgnoreCase) ||
                   !Path.IsPathRooted(path);
        }

        /// <summary>
        /// 获取本地化图片路径
        /// </summary>
        /// <param name="key">资源键</param>
        /// <param name="culture">文化信息</param>
        /// <returns>本地化图片路径</returns>
        private string? GetLocalizedImagePath(string key, CultureInfo? culture)
        {
            var service = _localizationService ?? WpfLocalizationExtensions.LocalizationService;
            if (service == null)
                return null;

            return service.GetImagePath(key, culture);
        }

        /// <summary>
        /// 从路径加载图片位图
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>图片位图</returns>
        private BitmapSource LoadBitmapFromPath(string imagePath)
        {
            // 处理相对路径和绝对路径
            var uri = CreateUri(imagePath);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = uri;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            // 设置解码选项以提高性能
            bitmap.DecodePixelWidth = 0; // 自动解码
            bitmap.DecodePixelHeight = 0; // 自动解码

            bitmap.EndInit();
            bitmap.Freeze(); // 冻结以便跨线程访问

            return bitmap;
        }

        /// <summary>
        /// 创建URI
        /// </summary>
        /// <param name="path">路径</param>
        /// <returns>URI对象</returns>
        private Uri CreateUri(string path)
        {
            // 尝试不同URI格式
            var uriFormats = new[]
            {
                UriKind.Absolute,
                UriKind.Relative,
                UriKind.RelativeOrAbsolute
            };

            foreach (var kind in uriFormats)
            {
                try
                {
                    if (Uri.TryCreate(path, kind, out var uri))
                    {
                        return uri;
                    }
                }
                catch (UriFormatException)
                {
                    continue;
                }
            }

            // 如果都失败，尝试pack URI格式
            var packPath = path.StartsWith("/") ? path : $"/{path}";
            if (Uri.TryCreate($"pack://application:,,,{packPath}", UriKind.Absolute, out var packUri))
            {
                return packUri;
            }

            // 最后尝试相对URI
            return new Uri(path, UriKind.Relative);
        }

        /// <summary>
        /// 获取默认图片
        /// </summary>
        /// <returns>默认图片位图</returns>
        private BitmapSource GetDefaultImage()
        {
            // 返回一个 1x1 透明像素，避免未设置 Uri/Stream 导致的 EndInit 异常
            var pixels = new byte[] { 0, 0, 0, 0 }; // BGRA32: 全透明
            var bmp = BitmapSource.Create(
                1, 1,
                96, 96,
                PixelFormats.Bgra32,
                null,
                pixels,
                4);
            bmp.Freeze();
            return bmp;
        }
    }

    /// <summary>
    /// 异步图片资源转换器
    /// </summary>
    [ValueConversion(typeof(string), typeof(BitmapSource))]
    public class AsyncImageResourceConverter : IValueConverter
    {
        private static ILocalizationService? _localizationService;

        /// <summary>
        /// 获取或设置本地化服务
        /// </summary>
        public static ILocalizationService? LocalizationService
        {
            get => _localizationService;
            set => _localizationService = value;
        }

        /// <summary>
        /// 转换图片资源路径为异步图片加载器
        /// </summary>
        /// <param name="value">图片资源路径或键</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">文化信息</param>
        /// <returns>异步图片加载器</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return new AsyncImageLoader(null);

            var imagePath = value as string;
            if (string.IsNullOrEmpty(imagePath))
                return new AsyncImageLoader(null);

            return new AsyncImageLoader(imagePath, culture);
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("AsyncImageResourceConverter does not support conversion back.");
        }
    }

    /// <summary>
    /// 异步图片加载器
    /// </summary>
    public class AsyncImageLoader : INotifyPropertyChanged
    {
        private readonly string? _imagePath;
        private readonly CultureInfo? _culture;
        private BitmapSource? _image;
        private bool _isLoading;
        private string? _errorMessage;
        private bool _loadFailed;

        /// <summary>
        /// 加载的图片
        /// </summary>
        public BitmapSource? Image
        {
            get => _image;
            private set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged(nameof(Image));
                    OnPropertyChanged(nameof(HasImage));
                }
            }
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (_errorMessage != value)
                {
                    _errorMessage = value;
                    OnPropertyChanged(nameof(ErrorMessage));
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        /// <summary>
        /// 是否有图片
        /// </summary>
        public bool HasImage => Image != null;

        /// <summary>
        /// 是否有错误
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// 是否加载失败
        /// </summary>
        public bool LoadFailed
        {
            get => _loadFailed;
            private set
            {
                if (_loadFailed != value)
                {
                    _loadFailed = value;
                    OnPropertyChanged(nameof(LoadFailed));
                }
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <param name="culture">文化信息</param>
        public AsyncImageLoader(string? imagePath, CultureInfo? culture = null)
        {
            _imagePath = imagePath;
            _culture = culture;

            if (!string.IsNullOrEmpty(imagePath))
            {
                LoadImageAsync();
            }
        }

        /// <summary>
        /// 异步加载图片
        /// </summary>
        private async void LoadImageAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                LoadFailed = false;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    LoadImage();
                });
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                LoadFailed = true;
                System.Diagnostics.Debug.WriteLine($"Failed to load image '{_imagePath}': {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 加载图片
        /// </summary>
        private void LoadImage()
        {
            var converter = new ImageResourceConverter();
            Image = (BitmapSource?)converter.Convert(_imagePath ?? string.Empty, typeof(BitmapSource), string.Empty, _culture ?? CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// 重新加载图片
        /// </summary>
        public void Reload()
        {
            if (!string.IsNullOrEmpty(_imagePath))
            {
                LoadImageAsync();
            }
        }

        /// <summary>
        /// 属性变化事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性变化事件
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 图片缓存管理器
    /// </summary>
    public static class ImageCacheManager
    {
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, WeakReference<BitmapSource>> _cache = new();
        private static readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(30);

        /// <summary>
        /// 获取缓存的图片
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <returns>缓存的图片或null</returns>
        public static BitmapSource? GetCachedImage(string key)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var weakRef) &&
                    weakRef.TryGetTarget(out var bitmap))
                {
                    return bitmap;
                }
            }
            return null;
        }

        /// <summary>
        /// 缓存图片
        /// </summary>
        /// <param name="key">缓存键</param>
        /// <param name="bitmap">图片位图</param>
        public static void CacheImage(string key, BitmapSource bitmap)
        {
            if (string.IsNullOrEmpty(key) || bitmap == null)
                return;

            lock (_lock)
            {
                _cache[key] = new WeakReference<BitmapSource>(bitmap);
            }
        }

        /// <summary>
        /// 清理缓存
        /// </summary>
        public static void ClearCache()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// 清理过期的缓存项
        /// </summary>
        public static void CleanupExpiredCache()
        {
            lock (_lock)
            {
                var expiredKeys = new List<string>();
                foreach (var kvp in _cache)
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _cache.Remove(key);
                }
            }
        }
    }
}
