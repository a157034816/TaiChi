using System.Net;

namespace CentralService.Service.Internal
{
    internal sealed class CentralServiceHttpResponse
    {
        public CentralServiceHttpResponse(HttpStatusCode statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public HttpStatusCode StatusCode { get; private set; }

        public string Body { get; private set; }
    }
}

