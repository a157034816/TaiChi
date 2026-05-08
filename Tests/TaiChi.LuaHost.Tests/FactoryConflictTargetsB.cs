namespace TaiChi.LuaHost.Tests.FactoryConflictB;

/// <summary>
/// 用于验证全局工厂函数「同名类型冲突」场景的示例普通类（命名空间 B）。
/// </summary>
public sealed class FactoryConflictTarget
{
    /// <summary>
    /// 标识来源命名空间。
    /// </summary>
    public string Source { get; } = "B";
}
