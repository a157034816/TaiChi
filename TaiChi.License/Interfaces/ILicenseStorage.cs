using System.Collections.Generic;
using TaiChi.License.Models;

namespace TaiChi.License.Interfaces
{
    /// <summary>
    /// 许可证存储接口。默认提供基于文件系统的实现。
    /// </summary>
    public interface ILicenseStorage
    {
        /// <summary>
        /// 基础存储目录（外部注入，不得硬编码）。
        /// </summary>
        string BaseDirectory { get; }

        /// <summary>
        /// 许可证文件扩展名（默认 .lic）。
        /// </summary>
        string Extension { get; }

        /// <summary>
        /// 保存许可证（原子写入）。
        /// </summary>
        void Save(LicenseKey license, string name);

        /// <summary>
        /// 加载许可证；不存在或解析失败时返回 null。
        /// </summary>
        LicenseKey? Load(string name);

        /// <summary>
        /// 删除许可证文件；返回是否存在并成功删除。
        /// </summary>
        bool Delete(string name);

        /// <summary>
        /// 是否存在指定名称的许可证文件。
        /// </summary>
        bool Exists(string name);

        /// <summary>
        /// 列出存储中的许可证名称（不含扩展名）。
        /// </summary>
        IEnumerable<string> ListNames();

        /// <summary>
        /// 解析名称对应的完整文件路径。
        /// </summary>
        string ResolvePath(string name);
    }
}

