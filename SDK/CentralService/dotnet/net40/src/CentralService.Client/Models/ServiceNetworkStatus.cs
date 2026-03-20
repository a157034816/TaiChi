using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务网络连通性与质量状态。
    /// </summary>
    [DataContract]
    public sealed class ServiceNetworkStatus
    {
        /// <summary>
        /// 获取或设置服务实例标识。
        /// </summary>
        [DataMember(Name = "serviceId", Order = 1)]
        public string ServiceId { get; set; }

        /// <summary>
        /// 获取或设置最近一次检测的响应时长，单位为毫秒。
        /// </summary>
        [DataMember(Name = "responseTime", Order = 2)]
        public long ResponseTime { get; set; }

        /// <summary>
        /// 获取或设置最近一次检测的丢包率。
        /// </summary>
        [DataMember(Name = "packetLoss", Order = 3)]
        public double PacketLoss { get; set; }

        /// <summary>
        /// 获取或设置最近一次检测时间。
        /// </summary>
        [DataMember(Name = "lastCheckTime", Order = 4, EmitDefaultValue = false)]
        public string LastCheckTime { get; set; }

        /// <summary>
        /// 获取或设置连续成功次数。
        /// </summary>
        [DataMember(Name = "consecutiveSuccesses", Order = 5, EmitDefaultValue = false)]
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>
        /// 获取或设置连续失败次数。
        /// </summary>
        [DataMember(Name = "consecutiveFailures", Order = 6, EmitDefaultValue = false)]
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 获取或设置当前服务是否可用。
        /// </summary>
        [DataMember(Name = "isAvailable", Order = 7)]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// 根据响应时长和丢包率计算 0 到 100 的网络质量评分。
        /// </summary>
        /// <returns>网络质量评分，分值越高表示网络质量越好。</returns>
        public int CalculateScore()
        {
            if (!IsAvailable)
            {
                return 0;
            }

            // 响应时长与丢包率各占 50 分：在可接受阈值内线性衰减，超过阈值直接记 0 分。
            var responseTimeScore = ResponseTime >= 1000
                ? 0
                : ResponseTime <= 50
                    ? 50
                    : (int)(50 * (1 - (ResponseTime - 50) / 950.0));

            // 丢包率越低越接近满分，50% 及以上视为该维度完全不可用。
            var packetLossScore = PacketLoss >= 50
                ? 0
                : PacketLoss <= 0
                    ? 50
                    : (int)(50 * (1 - PacketLoss / 50.0));

            return responseTimeScore + packetLossScore;
        }
    }
}
