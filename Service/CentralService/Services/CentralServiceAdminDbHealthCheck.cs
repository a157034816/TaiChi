using CentralService.Admin.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CentralService.Services;

public sealed class CentralServiceAdminDbHealthCheck : IHealthCheck
{
    private readonly CentralServiceAdminDb _db;

    public CentralServiceAdminDbHealthCheck(CentralServiceAdminDb db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _db.EnsureCreated();
            return Task.FromResult(HealthCheckResult.Healthy("CentralServiceAdminDb OK"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("CentralServiceAdminDb failed", ex));
        }
    }
}

