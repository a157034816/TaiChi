using System;
using System.Reflection;
using System.Text;

namespace TaiChi.LuaHost.Naming;

/// <summary>
/// 使用 snake_case 命名的默认策略。
/// </summary>
public sealed class SnakeCaseNamingStrategy : ILuaNamingStrategy
{
    /// <summary>
    /// 获取默认实例。
    /// </summary>
    public static SnakeCaseNamingStrategy Instance { get; } = new();

    /// <inheritdoc />
    public string GetName(MethodInfo method)
    {
        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        return ToSnakeCase(method.Name);
    }

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length * 2);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch))
            {
                if (i > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
