using System.Diagnostics;

namespace CentralService.Services;

public interface IManagedProcessLifetimeBinder
{
    void Attach(Process process);
}

internal sealed class NoOpManagedProcessLifetimeBinder : IManagedProcessLifetimeBinder
{
    public void Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);
    }
}
