using System;
using System.Collections.Generic;
using Client = CentralService.Client;
using Service = CentralService.Service;
using ServiceRegistrationRequest = CentralService.Service.Models.ServiceRegistrationRequest;

internal static class Program
{
    private static string BaseUrl()
    {
        var env = Environment.GetEnvironmentVariable("CENTRAL_SERVICE_BASEURL");
        if (!string.IsNullOrEmpty(env))
        {
            return env.Trim();
        }

        return "http://127.0.0.1:5000";
    }

    public static int Main(string[] args)
    {
        var serviceOptions = new Service.CentralServiceSdkOptions(BaseUrl());
        var discoveryOptions = new Client.CentralServiceSdkOptions(BaseUrl());
        var serviceClient = new Service.CentralServiceServiceClient(serviceOptions);
        var discoveryClient = new Client.CentralServiceDiscoveryClient(discoveryOptions);

        Console.WriteLine("[net40] BaseUrl=" + serviceOptions.BaseUrl);

        var request = new ServiceRegistrationRequest
        {
            Id = string.Empty,
            Name = "SdkE2E",
            Host = "127.0.0.1",
            LocalIp = "127.0.0.1",
            OperatorIp = "127.0.0.1",
            PublicIp = "127.0.0.1",
            Port = 18085,
            ServiceType = "Web",
            HealthCheckType = "Http",
            HealthCheckUrl = "/health",
            Weight = 1,
            Metadata = new Dictionary<string, string>
            {
                { "sdk", "net40" }
            }
        };

        var reg = serviceClient.Register(request);
        Console.WriteLine("[net40] registered id=" + reg.Id);

        serviceClient.Heartbeat(reg.Id);
        Console.WriteLine("[net40] heartbeat ok");

        var list = discoveryClient.List("SdkE2E");
        var count = (list != null && list.Services != null) ? list.Services.Length : 0;
        Console.WriteLine("[net40] list count=" + count);

        var rr = discoveryClient.DiscoverRoundRobin("SdkE2E");
        Console.WriteLine("[net40] discover roundrobin id=" + (rr != null ? rr.Id : string.Empty));

        var weighted = discoveryClient.DiscoverWeighted("SdkE2E");
        Console.WriteLine("[net40] discover weighted id=" + (weighted != null ? weighted.Id : string.Empty));

        var best = discoveryClient.DiscoverBest("SdkE2E");
        Console.WriteLine("[net40] discover best id=" + (best != null ? best.Id : string.Empty));

        var evaluated = discoveryClient.EvaluateNetwork(reg.Id);
        Console.WriteLine("[net40] network evaluated score=" + evaluated.CalculateScore());

        var net = discoveryClient.GetNetwork(reg.Id);
        Console.WriteLine("[net40] network get score=" + net.CalculateScore());

        var all = discoveryClient.GetNetworkAll();
        Console.WriteLine("[net40] network all count=" + (all != null ? all.Length : 0));

        serviceClient.Deregister(reg.Id);
        Console.WriteLine("[net40] deregister ok");

        return 0;
    }
}

