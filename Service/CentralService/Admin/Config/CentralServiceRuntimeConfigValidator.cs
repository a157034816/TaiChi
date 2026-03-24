using System.Text.Json;

namespace CentralService.Admin.Config;

public static class CentralServiceRuntimeConfigValidator
{
    public static IReadOnlyList<string> ValidateJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new[] { "ConfigJson 不能为空" };
        }

        CentralServiceRuntimeConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<CentralServiceRuntimeConfig>(json, CentralServiceRuntimeConfigJson.Options);
        }
        catch (Exception ex)
        {
            return new[] { $"ConfigJson 不是有效 JSON: {ex.Message}" };
        }

        if (config == null)
        {
            return new[] { "ConfigJson 解析失败" };
        }

        return Validate(config);
    }

    public static IReadOnlyList<string> Validate(CentralServiceRuntimeConfig config)
    {
        var errors = new List<string>();

        if (config.Services == null)
        {
            errors.Add("Services 不能为空（可为空数组）");
        }
        else
        {
            for (var i = 0; i < config.Services.Count; i++)
            {
                var policy = config.Services[i];
                if (policy == null)
                {
                    errors.Add($"Services[{i}] 不能为空");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(policy.ServiceName))
                {
                    errors.Add($"Services[{i}].ServiceName 不能为空");
                }
                else if (policy.ServiceName.Length > 128)
                {
                    errors.Add($"Services[{i}].ServiceName 过长");
                }

                if (policy.MinHealthyInstances is < 1)
                {
                    errors.Add($"Services[{i}].MinHealthyInstances 必须 >= 1");
                }
            }
        }

        if (config.Instances == null)
        {
            errors.Add("Instances 不能为空（可为空数组）");
        }
        else
        {
            for (var i = 0; i < config.Instances.Count; i++)
            {
                var instance = config.Instances[i];
                if (instance == null)
                {
                    errors.Add($"Instances[{i}] 不能为空");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(instance.ServiceId))
                {
                    errors.Add($"Instances[{i}].ServiceId 不能为空");
                }
                else if (instance.ServiceId.Length > 64)
                {
                    errors.Add($"Instances[{i}].ServiceId 过长");
                }

                if (instance.Weight is < 0)
                {
                    errors.Add($"Instances[{i}].Weight 必须 >= 0");
                }

                if (instance.ServiceName != null && instance.ServiceName.Length > 128)
                {
                    errors.Add($"Instances[{i}].ServiceName 过长");
                }
            }
        }

        return errors;
    }
}

