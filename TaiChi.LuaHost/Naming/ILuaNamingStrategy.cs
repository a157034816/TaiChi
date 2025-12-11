using System.Reflection;

namespace TaiChi.LuaHost.Naming;

/// <summary>
/// 定义方法导出名称计算策略。
/// </summary>
public interface ILuaNamingStrategy
{
    /// <summary>
    /// 根据方法信息返回导出名称。
    /// </summary>
    string GetName(MethodInfo method);
}
