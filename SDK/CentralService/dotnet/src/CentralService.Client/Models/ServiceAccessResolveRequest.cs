using System;
using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    [DataContract]
    public sealed class ServiceAccessResolveRequest
    {
        [DataMember(Name = "serviceName", Order = 1)]
        public string ServiceName { get; set; } = string.Empty;

        [DataMember(Name = "clientName", Order = 2)]
        public string ClientName { get; set; } = string.Empty;

        [DataMember(Name = "clientLocalIp", Order = 3, EmitDefaultValue = false)]
        public string ClientLocalIp { get; set; } = string.Empty;

        [DataMember(Name = "clientOperatorIp", Order = 4, EmitDefaultValue = false)]
        public string ClientOperatorIp { get; set; } = string.Empty;

        [DataMember(Name = "clientPublicIp", Order = 5, EmitDefaultValue = false)]
        public string ClientPublicIp { get; set; } = string.Empty;

        [DataMember(Name = "excludedServiceIds", Order = 6, EmitDefaultValue = false)]
        public string[] ExcludedServiceIds { get; set; } = Array.Empty<string>();
    }
}
