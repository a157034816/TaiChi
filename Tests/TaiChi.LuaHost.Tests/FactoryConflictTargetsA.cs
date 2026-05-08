namespace TaiChi.LuaHost.Tests.FactoryConflictA;

/// <summary>
/// 用于验证全局工厂函数「同名类型冲突」场景的示例普通类（命名空间 A）。
/// </summary>
public sealed class FactoryConflictTarget
{
    /// <summary>
    /// 标识来源命名空间。
    /// </summary>
    public string Source { get; } = "A";
}
