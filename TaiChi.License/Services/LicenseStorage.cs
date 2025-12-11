using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaiChi.License.Interfaces;
using TaiChi.License.Models;

namespace TaiChi.License.Services
{
    /// <summary>
    /// 文件系统实现的许可证存储。支持原子写入与基本异常处理。
    /// </summary>
    public class LicenseStorage : ILicenseStorage
    {
        private readonly ISerializer _serializer;

        public string BaseDirectory { get; }
        public string Extension { get; }

        public LicenseStorage(string baseDirectory, ISerializer serializer, string extension = ".lic")
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentNullException(nameof(baseDirectory));
            BaseDirectory = baseDirectory;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Extension = NormalizeExtension(extension);
            Directory.CreateDirectory(BaseDirectory);
        }

        public void Save(LicenseKey license, string name)
        {
            if (license == null) throw new ArgumentNullException(nameof(license));
            var path = ResolvePath(name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var bytes = _serializer.Serialize(license);
            var tmp = path + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllBytes(tmp, bytes);

            try
            {
                // 优先使用 Replace（近似原子），不可用时回退删除+移动
                try
                {
                    File.Replace(tmp, path, null);
                }
                catch
                {
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            finally
            {
                // 若 Replace/Move 失败，清理临时文件
                if (File.Exists(tmp))
                {
                    try { File.Delete(tmp); } catch { /* ignore */ }
                }
            }
        }

        public LicenseKey? Load(string name)
        {
            var path = ResolvePath(name);
            if (!File.Exists(path)) return null;
            try
            {
                var bytes = File.ReadAllBytes(path);
                return _serializer.Deserialize<LicenseKey>(bytes);
            }
            catch
            {
                return null;
            }
        }

        public bool Delete(string name)
        {
            var path = ResolvePath(name);
            if (!File.Exists(path)) return false;
            try { File.Delete(path); return true; }
            catch { return false; }
        }

        public bool Exists(string name)
        {
            var path = ResolvePath(name);
            return File.Exists(path);
        }

        public IEnumerable<string> ListNames()
        {
            if (!Directory.Exists(BaseDirectory)) yield break;
            foreach (var file in Directory.EnumerateFiles(BaseDirectory, "*" + Extension, SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrWhiteSpace(name)) yield return name;
            }
        }

        public string ResolvePath(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            // 防御性：只取文件名，避免路径穿越
            var safeName = Path.GetFileName(name);
            if (string.IsNullOrWhiteSpace(safeName)) throw new ArgumentException("名称无效", nameof(name));
            return Path.Combine(BaseDirectory, safeName + Extension);
        }

        private static string NormalizeExtension(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return ".lic";
            return ext.StartsWith(".") ? ext : "." + ext;
        }
    }
}

