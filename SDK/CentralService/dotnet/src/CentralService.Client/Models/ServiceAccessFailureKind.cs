namespace CentralService.Client.Models
{
    /// <summary>
    /// 表示开发者回调返回的连接失败类型。
    /// </summary>
    public enum ServiceAccessFailureKind
    {
        Transport,
        Timeout,
        Refused,
        Business,
        Unknown,
    }
}
