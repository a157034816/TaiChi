using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    /// <summary>
    /// 表示服务实例的网络探测状态。
    /// </summary>
    [DataContract]
    public sealed class ServiceNetworkStatus
    {
        /// <summary>
        /// 获取或设置对应的服务实例 ID。
        /// </summary>
        [DataMember(Name = "serviceId", Order = 1)]
        public string ServiceId { get; set; }

        /// <summary>
        /// 获取或设置最近一次探测的响应时间，单位为毫秒。
        /// </summary>
        [DataMember(Name = "responseTime", Order = 2)]
        public long ResponseTime { get; set; }

        /// <summary>
        /// 获取或设置最近一次探测的丢包率百分比。
        /// </summary>
        [DataMember(Name = "packetLoss", Order = 3)]
        public double PacketLoss { get; set; }

        /// <summary>
        /// 获取或设置最近一次探测时间。
        /// </summary>
        [DataMember(Name = "lastCheckTime", Order = 4, EmitDefaultValue = false)]
        public string LastCheckTime { get; set; }

        /// <summary>
        /// 获取或设置连续成功探测次数。
        /// </summary>
        [DataMember(Name = "consecutiveSuccesses", Order = 5, EmitDefaultValue = false)]
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>
        /// 获取或设置连续失败探测次数。
        /// </summary>
        [DataMember(Name = "consecutiveFailures", Order = 6, EmitDefaultValue = false)]
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 获取或设置该服务实例当前是否可用。
        /// </summary>
        [DataMember(Name = "isAvailable", Order = 7)]
        public bool IsAvailable { get; set; }

        /// <summary>
        /// 根据响应时间与丢包率计算网络质量评分。
        /// </summary>
        /// <returns>范围为 0 到 100 的整数分值。</returns>
        public int CalculateScore()
        {
            if (!IsAvailable)
            {
                return 0;
            }

            // 响应时间与丢包率各占 50 分，便于在旧运行时中复用服务端相同的评估权重。
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
