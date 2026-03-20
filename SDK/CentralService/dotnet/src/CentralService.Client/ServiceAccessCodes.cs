namespace CentralService.Client
{
    /// <summary>
    /// 定义服务访问闭环中使用的中心服务错误码与决策码。
    /// </summary>
    public static class ServiceAccessCodes
    {
        public const string NoAvailableInstance = "ACCESS_NO_AVAILABLE_INSTANCE";
        public const string CircuitOpen = "ACCESS_CIRCUIT_OPEN";
        public const string TryNextInstance = "ACCESS_TRY_NEXT_INSTANCE";
        public const string RetryResolve = "ACCESS_RETRY_RESOLVE";
        public const string InvalidClientIdentity = "ACCESS_INVALID_CLIENT_IDENTITY";
        public const string Complete = "ACCESS_COMPLETE";
    }
}
