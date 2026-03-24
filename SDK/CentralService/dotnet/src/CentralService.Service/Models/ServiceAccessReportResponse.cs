using System.Runtime.Serialization;

namespace CentralService.Service.Models
{
    [DataContract]
    public sealed class ServiceAccessReportResponse
    {
        [DataMember(Name = "decisionCode", Order = 1)]
        public string DecisionCode { get; set; } = string.Empty;

        [DataMember(Name = "message", Order = 2, EmitDefaultValue = false)]
        public string Message { get; set; } = string.Empty;
    }
}
