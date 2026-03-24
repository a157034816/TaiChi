namespace TaiChi.LuaHost.Tests.StaticA;

/// <summary>
/// 用于验证 <c>static</c> 根表自动注册“同名冲突”场景的示例静态类（命名空间 A）。
/// </summary>
public static class ConflictStatic
{
    /// <summary>
    /// 获取示例值。
    /// </summary>
    public static int Value { get; set; } = 1;
}

