using System.Runtime.Serialization;

namespace CentralService.Client.Models
{
    [DataContract]
    public sealed class ServiceAccessReportRequest
    {
        [DataMember(Name = "accessTicket", Order = 1)]
        public string AccessTicket { get; set; } = string.Empty;

        [DataMember(Name = "clientName", Order = 2)]
        public string ClientName { get; set; } = string.Empty;

        [DataMember(Name = "clientLocalIp", Order = 3, EmitDefaultValue = false)]
        public string ClientLocalIp { get; set; } = string.Empty;

        [DataMember(Name = "clientOperatorIp", Order = 4, EmitDefaultValue = false)]
        public string ClientOperatorIp { get; set; } = string.Empty;

        [DataMember(Name = "clientPublicIp", Order = 5, EmitDefaultValue = false)]
        public string ClientPublicIp { get; set; } = string.Empty;

        [DataMember(Name = "success", Order = 6)]
        public bool Success { get; set; }

        [DataMember(Name = "failureKind", Order = 7, EmitDefaultValue = false)]
        public string FailureKind { get; set; } = string.Empty;

        [DataMember(Name = "failureMessage", Order = 8, EmitDefaultValue = false)]
        public string FailureMessage { get; set; } = string.Empty;
    }
}
