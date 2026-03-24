namespace CentralService.DotNetCore20E2e
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            return CentralService.DotNetE2eShared.CentralServiceE2ERunner.Run("dotnetcore20", 18082);
        }
    }
}
