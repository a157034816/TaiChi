namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示一次通过中心服务获取到的目标服务上下文。
    /// </summary>
    public sealed class ServiceAccessContext
    {
        /// <summary>
        /// 获取或设置当前选中的服务实例。
        /// </summary>
        public ServiceInfo Service { get; set; }
    }
}
