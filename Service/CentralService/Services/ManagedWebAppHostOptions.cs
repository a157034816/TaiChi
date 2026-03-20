using Microsoft.Extensions.Hosting;

namespace CentralService.Services;

public sealed class ManagedWebAppHostOptions
{
    public int PollIntervalSeconds { get; set; } = 5;

    public List<ManagedWebAppDefinitionOptions> Definitions { get; set; } = new();
}

public sealed class ManagedWebAppDefinitionOptions
{
    public string Name { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string WorkingDirectory { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = new();

    public Dictionary<string, string> EnvironmentVariables { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> RequiredPaths { get; set; } = new();

    public string PrepareCommand { get; set; } = string.Empty;

    public List<string> PrepareArguments { get; set; } = new();

    public int PrepareTimeoutSeconds { get; set; } = 600;

    public string HealthCheckUrl { get; set; } = string.Empty;

    public int StartupTimeoutSeconds { get; set; } = 60;

    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    public int MaxConsecutiveHealthFailures { get; set; } = 3;

    public int RestartDelaySeconds { get; set; } = 5;

    public string GetTaskName()
    {
        return $"ManagedWebApp:{Name}";
    }

    public string ResolveWorkingDirectory(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        var rawPath = (WorkingDirectory ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return hostEnvironment.ContentRootPath;
        }

        if (Path.IsPathRooted(rawPath))
        {
            return Path.GetFullPath(rawPath);
        }

        return Path.GetFullPath(Path.Combine(hostEnvironment.ContentRootPath, rawPath));
    }
}
