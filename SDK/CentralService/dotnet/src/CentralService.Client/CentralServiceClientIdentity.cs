namespace CentralService.Client
{
    /// <summary>
    /// 描述服务消费方自身在中心服务中的身份信息。
    /// </summary>
    public sealed class CentralServiceClientIdentity
    {
        /// <summary>
        /// 获取或设置客户端名称。
        /// </summary>
        public string ClientName { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置客户端所在主机的局域网IP。
        /// </summary>
        public string LocalIp { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置客户端所在主机的运营商IP。
        /// </summary>
        public string OperatorIp { get; set; } = string.Empty;

        /// <summary>
        /// 获取或设置客户端所在主机的公网IP。
        /// </summary>
        public string PublicIp { get; set; } = string.Empty;
    }
}
