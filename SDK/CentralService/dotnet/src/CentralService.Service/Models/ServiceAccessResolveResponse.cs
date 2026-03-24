using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    [DataContract]
    public sealed class ServiceAccessResolveResponse
    {
        [DataMember(Name = "accessTicket", Order = 1)]
        public string AccessTicket { get; set; } = string.Empty;

        [DataMember(Name = "maxAttempts", Order = 2)]
        public int MaxAttempts { get; set; }

        [DataMember(Name = "service", Order = 3)]
        public ServiceInfo Service { get; set; }
    }
}
