namespace CentralService.Shared.Internal
{
    internal interface ICentralServiceTransport
    {
        CentralServiceTransportResult Send(string method, string path, string? jsonBody);
    }
}

