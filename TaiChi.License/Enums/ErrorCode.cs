using System;

namespace TaiChi.License.Enums
{
    /// <summary>
    /// 许可证相关错误码（50001-51000）
    /// </summary>
    public enum ErrorCode
    {
        /// <summary>
        /// 许可证格式无效（内容缺失或结构不合法）
        /// </summary>
        许可证_格式无效 = 50001,

        /// <summary>
        /// 许可证已过期
        /// </summary>
        许可证_已过期 = 50002,

        /// <summary>
        /// 许可证尚未生效（未达生效时间）
        /// </summary>
        许可证_尚未生效 = 50003,

        /// <summary>
        /// 数字签名无效或被篡改
        /// </summary>
        许可证_签名无效 = 50004,

        /// <summary>
        /// 硬件信息不匹配（指纹校验失败）
        /// </summary>
        许可证_硬件不匹配 = 50005,

        /// <summary>
        /// 产品标识不匹配
        /// </summary>
        许可证_产品不匹配 = 50006,

        /// <summary>
        /// 产品版本不匹配
        /// </summary>
        许可证_版本不匹配 = 50007,

        /// <summary>
        /// 功能未授权
        /// </summary>
        许可证_功能未授权 = 50008,

        /// <summary>
        /// 用户数量超出许可限制
        /// </summary>
        许可证_用户数超限 = 50009,

        /// <summary>
        /// 许可证反序列化失败
        /// </summary>
        许可证_反序列化失败 = 50010,
    }
}

