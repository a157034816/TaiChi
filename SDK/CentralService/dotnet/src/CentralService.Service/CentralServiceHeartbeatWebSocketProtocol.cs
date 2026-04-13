namespace CentralService.Service
{
    /// <summary>
    /// 定义中心服务与周边服务之间的 WebSocket 心跳协议常量。
    /// </summary>
    public static class CentralServiceHeartbeatWebSocketProtocol
    {
        /// <summary>
        /// 心跳 WebSocket 连接地址（相对中心服务根地址）。
        /// </summary>
        public const string HeartbeatWebSocketPath = "/api/Service/heartbeat/ws";

        /// <summary>
        /// WebSocket 握手查询参数：服务实例标识。
        /// </summary>
        public const string ServiceIdQueryKey = "serviceId";

        /// <summary>
        /// 中心服务 -> 周边服务：心跳请求消息（Text）。
        /// </summary>
        public const string HeartbeatRequestMessage = "heartbeat";

        /// <summary>
        /// 周边服务 -> 中心服务：心跳响应消息（Text）。
        /// </summary>
        public const string HeartbeatResponseMessage = "heartbeat_ok";
    }
}

