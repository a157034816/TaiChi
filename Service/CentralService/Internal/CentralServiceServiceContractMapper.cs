using CentralService.Models;
using ServiceApiInfo = CentralService.Service.Models.ServiceInfo;

namespace CentralService.Internal;

internal static class CentralServiceServiceContractMapper
{
    public static ServiceApiInfo ToApiModel(ServiceInfo service)
    {
        var api = new ServiceApiInfo
        {
            Id = service.Id,
            Name = service.Name,
            Host = service.Host,
            LocalIp = service.LocalIp,
            OperatorIp = service.OperatorIp,
            PublicIp = service.PublicIp,
            Port = service.Port,
            Url = service.Url,
            ServiceType = service.ServiceType,
            Status = service.Status,
            HealthCheckUrl = service.HealthCheckUrl,
            HealthCheckPort = service.HealthCheckPort,
            HeartbeatIntervalSeconds = service.HeartbeatIntervalSeconds,
            RegisterTime = service.RegisterTime == default ? null : service.RegisterTime.ToString("O"),
            LastHeartbeatTime = service.LastHeartbeatTime == default ? null : service.LastHeartbeatTime.ToString("O"),
            Weight = service.Weight,
            Metadata = service.Metadata,
            IsLocalNetwork = service.IsLocalNetwork
        };

        return api;
    }
}
