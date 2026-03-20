namespace CentralService.Admin.Models;

public sealed record RoleDto(int Id, string Name, string? Description, bool IsSystem);

public sealed record CreateRoleRequest(string Name, string? Description);

public sealed record SetRolePermissionsRequest(IReadOnlyList<string> PermissionKeys);

public sealed record UserListItem(int Id, string Username, bool IsDisabled, IReadOnlyList<string> Roles);

public sealed record CreateUserRequest(string Username, string Password, IReadOnlyList<int> RoleIds);

public sealed record SetUserPasswordRequest(string NewPassword);

public sealed record SetUserRolesRequest(IReadOnlyList<int> RoleIds);

public sealed record SetUserDisabledRequest(bool IsDisabled);

