using System;

namespace CentralService.Models
{
    /// <summary>
    /// 服务网络状态模型
    /// </summary>
    public class ServiceNetworkStatus
    {
        /// <summary>
        /// 服务ID
        /// </summary>
        public string ServiceId { get; set; }

        /// <summary>
        /// 响应时间(毫秒)
        /// </summary>
        public long ResponseTime { get; set; }

        /// <summary>
        /// 丢包率(0-100)
        /// </summary>
        public double PacketLoss { get; set; }

        /// <summary>
        /// 最后检测时间
        /// </summary>
        public DateTime LastCheckTime { get; set; }

        /// <summary>
        /// 连续成功次数
        /// </summary>
        public int ConsecutiveSuccesses { get; set; }

        /// <summary>
        /// 连续失败次数
        /// </summary>
        public int ConsecutiveFailures { get; set; }

        /// <summary>
        /// 是否可用
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// 计算网络状态评分(0-100)
        /// 根据响应时间和丢包率综合计算,分数越高表示网络状态越好
        /// </summary>
        /// <returns>网络状态评分</returns>
        public int CalculateScore()
        {
            if (!IsAvailable)
                return 0;
                
            // 响应时间评分(0-50分)
            // 响应时间越短,得分越高
            // 响应时间>1000ms得0分,<50ms得满分
            int responseTimeScore = ResponseTime >= 1000 ? 0 : 
                                    ResponseTime <= 50 ? 50 : 
                                    (int)(50 * (1 - (ResponseTime - 50) / 950.0));
            
            // 丢包率评分(0-50分)
            // 丢包率越低,得分越高
            // 丢包率>50%得0分,=0%得满分
            int packetLossScore = PacketLoss >= 50 ? 0 : 
                                  PacketLoss <= 0 ? 50 : 
                                  (int)(50 * (1 - PacketLoss / 50.0));
            
            return responseTimeScore + packetLossScore;
        }
    }
} 