namespace CentralService.Admin;

public static class CentralServicePermissions
{
    public static class Services
    {
        public const string Read = "centralservice.services.read";
        public const string Manage = "centralservice.services.manage";
    }

    public static class Config
    {
        public const string Read = "centralservice.config.read";
        public const string Edit = "centralservice.config.edit";
        public const string Publish = "centralservice.config.publish";
        public const string Rollback = "centralservice.config.rollback";
    }

    public static class Audit
    {
        public const string Read = "centralservice.audit.read";
    }

    public static class Users
    {
        public const string Manage = "centralservice.users.manage";
    }

    public static class Monitoring
    {
        public const string Read = "centralservice.monitoring.read";
        public const string Manage = "centralservice.monitoring.manage";
    }

    public static readonly IReadOnlyList<PermissionDefinition> Definitions = new[]
    {
        new PermissionDefinition(Services.Read, "服务：查看", "服务管理"),
        new PermissionDefinition(Services.Manage, "服务：管理", "服务管理"),

        new PermissionDefinition(Config.Read, "配置：查看", "配置管理"),
        new PermissionDefinition(Config.Edit, "配置：编辑", "配置管理"),
        new PermissionDefinition(Config.Publish, "配置：发布", "发布回滚"),
        new PermissionDefinition(Config.Rollback, "配置：回滚", "发布回滚"),

        new PermissionDefinition(Audit.Read, "审计：查看", "审计日志"),

        new PermissionDefinition(Users.Manage, "用户：管理", "用户与权限"),

        new PermissionDefinition(Monitoring.Read, "监控：查看", "监控运维"),
        new PermissionDefinition(Monitoring.Manage, "监控：管理", "监控运维"),
    };

    public static readonly IReadOnlyList<string> AllKeys = Definitions.Select(x => x.Key).ToArray();

    public sealed record PermissionDefinition(string Key, string Name, string Group);
}
