using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示服务实例当前的网络连通性指标。
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
        /// 获取或设置响应时间，单位为毫秒。
        /// </summary>
        [DataMember(Name = "responseTime", Order = 2)]
        public long ResponseTime { get; set; }

        /// <summary>
        /// 获取或设置丢包率，通常以百分比表示。
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
        /// 获取或设置服务当前是否可用。
        /// </summary>
        [DataMember(Name = "isAvailable", Order = 7)]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// 根据响应时间和丢包率计算 0 到 100 的网络评分。
        /// </summary>
        /// <returns>网络评分；不可用时直接返回 0。</returns>
        public int CalculateScore()
        {
            if (!IsAvailable)
            {
                return 0;
            }

            // 评分被拆成响应时间和丢包率两部分，各占 50 分；超过阈值直接归零，阈值内按线性衰减。
            var responseTimeScore = ResponseTime >= 1000
                ? 0
                : ResponseTime <= 50
                    ? 50
                    : (int)(50 * (1 - (ResponseTime - 50) / 950.0));

            var packetLossScore = PacketLoss >= 50
                ? 0
                : PacketLoss <= 0
                    ? 50
                    : (int)(50 * (1 - PacketLoss / 50.0));

            return responseTimeScore + packetLossScore;
        }
    }
}
