namespace CentralService.Admin.Models;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    int UserId,
    string Username,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record BootstrapStatusResponse(
    bool Enabled,
    bool HasAnyUser,
    bool IsLoopbackRequest,
    bool CanBootstrap,
    string Message);

