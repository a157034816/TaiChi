using System.Collections.Concurrent;

namespace CentralService.Services;

public sealed class CentralServiceBackgroundTaskMonitor
{
    private readonly ConcurrentDictionary<string, CentralServiceBackgroundTaskStatus> _tasks = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<CentralServiceBackgroundTaskStatus> GetAll()
    {
        return _tasks.Values
            .OrderBy(x => x.TaskName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void MarkSuccess(string taskName)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        var now = DateTime.UtcNow;
        _tasks.AddOrUpdate(
            taskName,
            _ => new CentralServiceBackgroundTaskStatus(taskName)
            {
                LastRunAtUtc = now,
                LastSuccessAtUtc = now,
                IsHealthy = true,
            },
            (_, existing) =>
            {
                existing.LastRunAtUtc = now;
                existing.LastSuccessAtUtc = now;
                existing.IsHealthy = true;
                existing.LastError = null;
                return existing;
            });
    }

    public void MarkError(string taskName, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var error = exception?.Message;

        _tasks.AddOrUpdate(
            taskName,
            _ => new CentralServiceBackgroundTaskStatus(taskName)
            {
                LastRunAtUtc = now,
                LastErrorAtUtc = now,
                IsHealthy = false,
                LastError = error,
            },
            (_, existing) =>
            {
                existing.LastRunAtUtc = now;
                existing.LastErrorAtUtc = now;
                existing.IsHealthy = false;
                existing.LastError = error;
                return existing;
            });
    }
}

public sealed class CentralServiceBackgroundTaskStatus
{
    public CentralServiceBackgroundTaskStatus(string taskName)
    {
        TaskName = taskName;
    }

    public string TaskName { get; }

    public bool IsHealthy { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    public DateTime? LastSuccessAtUtc { get; set; }

    public DateTime? LastErrorAtUtc { get; set; }

    public string? LastError { get; set; }
}

