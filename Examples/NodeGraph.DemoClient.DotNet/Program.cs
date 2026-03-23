using System.Globalization;
using System.Text.Json;
using NodeGraphSdk;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.WriteIndented = true;
});

builder.Services.AddSingleton(_ =>
{
    var config = DemoConfig.FromEnvironment();
    var runtime = HelloWorldFactory.CreateRuntime(config);
    var client = new NodeGraphClient(new HttpClient(), config.NodeGraphBaseUrl);
    return new DemoHost(config, runtime, client);
});

var app = builder.Build();
var host = app.Services.GetRequiredService<DemoHost>();

app.Urls.Clear();
app.Urls.Add(host.Config.ListenUrl);

app.MapGet("/", (DemoHost demoHost) => Results.Json(demoHost.GetOverview()));
app.MapGet("/api/health", (DemoHost demoHost) => Results.Json(demoHost.GetHealth()));
app.MapGet("/api/runtime/library", (DemoHost demoHost) => Results.Json(demoHost.GetLibraryPayload()));
app.MapGet("/api/runtime/field-options", (HttpRequest request) =>
{
    var nodeType = request.Query["nodeType"].ToString();
    var fieldKey = request.Query["fieldKey"].ToString();
    var locale = request.Query["locale"].ToString();

    if (string.Equals(nodeType, "demo_source", StringComparison.Ordinal) &&
        string.Equals(fieldKey, "punctuation", StringComparison.Ordinal))
    {
        var isZh = locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);
        return Results.Json(new
        {
            options = new[]
            {
                new { value = "!", label = isZh ? "感叹号 (!)" : "Exclamation (!)" },
                new { value = "?", label = isZh ? "问号 (?)" : "Question (?)" },
                new { value = ".", label = isZh ? "句号 (.)" : "Dot (.)" },
                new { value = "...", label = isZh ? "省略号 (...)" : "Ellipsis (...)" },
            },
        });
    }

    return Results.Json(new { options = Array.Empty<object>() });
});
app.MapGet("/api/results/latest", async (DemoHost demoHost) => Results.Json(await demoHost.GetLatestAsync()));

app.MapPost("/api/runtime/register", async (RegisterRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.RegisterRuntimeAsync(request?.Force == true));
});

app.MapPost("/api/runtime/execute", async (GraphRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.ExecuteAsync(request?.GraphMode, request?.GraphName));
});

app.MapPost("/api/runtime/debug/sample", async (GraphRequest? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.DebugSampleAsync(request?.GraphMode, request?.GraphName));
});

app.MapPost("/api/create-session", async (CreateSessionPayload? request, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.CreateSessionAsync(
        request?.GraphMode,
        request?.GraphName,
        request?.ForceRefresh == true));
});

app.MapPost("/api/completed", async (JsonElement payload, DemoHost demoHost) =>
{
    return Results.Json(await demoHost.StoreCompletionAsync(payload.Clone()));
});

app.Run();

/// <summary>
/// Demo 宿主服务的基础配置。
/// </summary>
internal sealed class DemoConfig
{
    /// <summary>
    /// NodeGraph 服务地址。
    /// </summary>
    public required string NodeGraphBaseUrl { get; init; }

    /// <summary>
    /// Demo 宿主对外地址。
    /// </summary>
    public required string DemoClientBaseUrl { get; init; }

    /// <summary>
    /// 监听地址。
    /// </summary>
    public required string ListenUrl { get; init; }

    /// <summary>
    /// Demo 使用的业务域。
    /// </summary>
    public required string DemoDomain { get; init; }

    /// <summary>
    /// Demo 客户端名称。
    /// </summary>
    public required string ClientName { get; init; }

    /// <summary>
    /// 从环境变量中构造默认配置。
    /// </summary>
    public static DemoConfig FromEnvironment()
    {
        var port = int.TryParse(Environment.GetEnvironmentVariable("DEMO_CLIENT_PORT"), out var parsedPort)
            ? parsedPort
            : 3200;
        var host = Environment.GetEnvironmentVariable("DEMO_CLIENT_HOST") ?? "127.0.0.1";
        var demoClientBaseUrl = Environment.GetEnvironmentVariable("DEMO_CLIENT_BASE_URL") ?? $"http://localhost:{port}";

        return new DemoConfig
        {
            NodeGraphBaseUrl = Environment.GetEnvironmentVariable("NODEGRAPH_BASE_URL") ?? "http://localhost:3000",
            DemoClientBaseUrl = demoClientBaseUrl.TrimEnd('/'),
            ListenUrl = $"http://{host}:{port}",
            DemoDomain = Environment.GetEnvironmentVariable("DEMO_CLIENT_DOMAIN") ?? "demo-hello-world",
            ClientName = Environment.GetEnvironmentVariable("DEMO_CLIENT_NAME") ?? "NodeGraph Demo Client (.NET)",
        };
    }
}

/// <summary>
/// Demo Showcase 节点图与运行时工厂。
/// </summary>
internal static class HelloWorldFactory
{
    private const string LibraryVersion = "demo-showcase@1";
    private const string DemoTextType = "hello/text";
    private const string DemoNumberType = "demo/number";
    private const string DemoBooleanType = "demo/boolean";
    private const string DemoDateType = "demo/date";
    private const string DemoColorType = "demo/color";
    private const string DemoDecimalType = "demo/decimal";

    private const string DefaultTemplate =
        "Greeting: {greeting}\nLucky: {lucky}\nDate: {today}\nTheme: {theme}\nAmount: {amount}";

    private const string EmittedStateKey = "__emitted";

    /// <summary>
    /// 判断当前节点是否已经完成过一次输出，避免同一次执行中因多次触发导致重复输出。
    /// </summary>
    private static bool HasEmittedOnce(NodeExecutionContext context)
    {
        return context.State.TryGetValue(EmittedStateKey, out var value) && value is bool emitted && emitted;
    }

    /// <summary>
    /// 标记当前节点已完成一次输出。
    /// </summary>
    private static void MarkEmittedOnce(NodeExecutionContext context)
    {
        context.State[EmittedStateKey] = true;
    }

    /// <summary>
    /// 将任意输入值尽量转换为非空字符串；无法转换时返回 fallback。
    /// </summary>
    private static string CoerceString(object? value, string fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        var text = value is string stringValue ? stringValue : Convert.ToString(value);
        return string.IsNullOrWhiteSpace(text) ? fallback : text.Trim();
    }

    /// <summary>
    /// 将任意输入值尽量转换为非空字符串，但保留首尾空白。
    /// </summary>
    private static string CoerceNonBlankString(object? value, string fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        var text = value is string stringValue ? stringValue : Convert.ToString(value);
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    /// <summary>
    /// 将任意输入值尽量转换为 double；无法转换时返回 fallback。
    /// </summary>
    private static double CoerceDouble(object? value, double fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is double doubleValue)
        {
            return doubleValue;
        }

        if (value is float floatValue)
        {
            return floatValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        if (value is long longValue)
        {
            return longValue;
        }

        if (value is decimal decimalValue)
        {
            return (double)decimalValue;
        }

        if (value is string raw &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        try
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    /// <summary>
    /// 将任意输入值尽量转换为 bool；无法转换时返回 fallback。
    /// </summary>
    private static bool CoerceBoolean(object? value, bool fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        if (value is string raw)
        {
            var normalized = raw.Trim().ToLowerInvariant();
            if (normalized == "true")
            {
                return true;
            }
            if (normalized == "false")
            {
                return false;
            }
        }

        if (value is int intValue)
        {
            return intValue != 0;
        }

        if (value is long longValue)
        {
            return longValue != 0;
        }

        if (value is double doubleValue)
        {
            return Math.Abs(doubleValue) > double.Epsilon;
        }

        return fallback;
    }

    /// <summary>
    /// 创建带有一组 Showcase 节点的运行时。
    /// </summary>
    public static NodeGraphRuntime CreateRuntime(DemoConfig config)
    {
        var runtime = new NodeGraphRuntime(new NodeGraphRuntimeOptions
        {
            Domain = config.DemoDomain,
            ClientName = config.ClientName,
            ControlBaseUrl = $"{config.DemoClientBaseUrl}/api/runtime",
            LibraryVersion = LibraryVersion,
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoTextType,
            Type = "DemoText",
            Color = "#2563eb",
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoNumberType,
            Type = "DemoNumber",
            Color = "#f97316",
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoBooleanType,
            Type = "DemoBoolean",
            Color = "#22c55e",
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoDateType,
            Type = "DemoDate",
            Color = "#a855f7",
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoColorType,
            Type = "DemoColor",
            Color = "#e11d48",
        });

        runtime.RegisterTypeMapping(new TypeMappingEntry
        {
            CanonicalId = DemoDecimalType,
            Type = "DemoDecimal",
            Color = "#06b6d4",
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "greeting_source",
            DisplayName = "Greeting Source",
            Description = "Create the greeting text that will be sent to the output node.",
            Category = "Hello World",
            Outputs =
            [
                new NodePortDefinition
                {
                    Id = "text",
                    Label = "Text",
                    DataType = DemoTextType,
                },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "name",
                    Label = "Name",
                    Kind = "text",
                    DefaultValue = "World",
                    Placeholder = "Who should be greeted?",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#eff6ff",
                BorderColor = "#2563eb",
                TextColor = "#1e3a8a",
            },
            ExecuteAsync = context =>
            {
                var name = CoerceString(context.Values.TryGetValue("name", out var value) ? value : null, "World");
                context.Emit("text", $"Hello, {name}!");
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "console_output",
            DisplayName = "Console Output",
            Description = "Collect the final greeting into the runtime result buffer.",
            Category = "Debug",
            Inputs =
            [
                new NodePortDefinition
                {
                    Id = "text",
                    Label = "Text",
                    DataType = DemoTextType,
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#f0fdf4",
                BorderColor = "#16a34a",
                TextColor = "#14532d",
            },
            ExecuteAsync = context =>
            {
                context.PushResult("console", context.ReadInput("text") ?? "Hello, World!");
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "demo_source",
            DisplayName = "Demo Source",
            Description = "Emit a bundle of typed values so downstream nodes can demonstrate rich editor features.",
            Category = "Playground",
            Outputs =
            [
                new NodePortDefinition { Id = "name", Label = "Name", DataType = DemoTextType },
                new NodePortDefinition { Id = "punctuation", Label = "Punctuation", DataType = DemoTextType },
                new NodePortDefinition { Id = "enabled", Label = "Enabled", DataType = DemoBooleanType },
                new NodePortDefinition { Id = "baseNumber", Label = "Base", DataType = DemoNumberType },
                new NodePortDefinition { Id = "delta", Label = "Delta", DataType = DemoNumberType },
                new NodePortDefinition { Id = "today", Label = "Date", DataType = DemoDateType },
                new NodePortDefinition { Id = "theme", Label = "Theme", DataType = DemoColorType },
                new NodePortDefinition { Id = "amount", Label = "Amount", DataType = DemoDecimalType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "name",
                    Label = "Name",
                    Kind = "text",
                    DefaultValue = "Codex",
                    Placeholder = "The name used in the greeting pipeline.",
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "punctuation",
                    Label = "Punctuation",
                    Kind = "select",
                    DefaultValue = "!",
                    OptionsEndpoint = $"{config.DemoClientBaseUrl}/api/runtime/field-options",
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "enabled",
                    Label = "Enabled",
                    Kind = "boolean",
                    DefaultValue = true,
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "baseNumber",
                    Label = "Base Number",
                    Kind = "int",
                    DefaultValue = 7,
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "delta",
                    Label = "Delta",
                    Kind = "double",
                    DefaultValue = 5,
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "today",
                    Label = "Date",
                    Kind = "date",
                    DefaultValue = "2026-03-21",
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "theme",
                    Label = "Theme Color",
                    Kind = "color",
                    DefaultValue = "#2563eb",
                },
                new NodeLibraryFieldDefinition
                {
                    Key = "amount",
                    Label = "Amount (decimal string)",
                    Kind = "decimal",
                    DefaultValue = "123.45",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#0b1220",
                BorderColor = "#38bdf8",
                TextColor = "#e0f2fe",
            },
            ExecuteAsync = context =>
            {
                context.Emit("name", CoerceString(context.Values.TryGetValue("name", out var value) ? value : null, "Codex"));
                context.Emit("punctuation", CoerceString(context.Values.TryGetValue("punctuation", out var punctuation) ? punctuation : null, "!"));
                context.Emit("enabled", CoerceBoolean(context.Values.TryGetValue("enabled", out var enabled) ? enabled : null, true));
                context.Emit("baseNumber", CoerceDouble(context.Values.TryGetValue("baseNumber", out var baseNumber) ? baseNumber : null, 7));
                context.Emit("delta", CoerceDouble(context.Values.TryGetValue("delta", out var delta) ? delta : null, 5));
                context.Emit("today", CoerceString(context.Values.TryGetValue("today", out var today) ? today : null, "2026-03-21"));
                context.Emit("theme", CoerceString(context.Values.TryGetValue("theme", out var theme) ? theme : null, "#2563eb"));
                context.Emit("amount", CoerceString(context.Values.TryGetValue("amount", out var amount) ? amount : null, "123.45"));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "greeting_builder",
            DisplayName = "Greeting Builder",
            Description = "Build a greeting message from inputs and emit it only when both inputs are ready.",
            Category = "Text",
            Inputs =
            [
                new NodePortDefinition { Id = "name", Label = "Name", DataType = DemoTextType },
                new NodePortDefinition { Id = "punctuation", Label = "Punctuation", DataType = DemoTextType },
            ],
            Outputs =
            [
                new NodePortDefinition { Id = "text", Label = "Greeting", DataType = DemoTextType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "prefix",
                    Label = "Prefix",
                    Kind = "text",
                    DefaultValue = "Hello, ",
                    Placeholder = "Text inserted before the name.",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#eff6ff",
                BorderColor = "#2563eb",
                TextColor = "#1e3a8a",
            },
            ExecuteAsync = context =>
            {
                if (HasEmittedOnce(context))
                {
                    return Task.CompletedTask;
                }

                var name = context.ReadInput("name");
                var punctuation = context.ReadInput("punctuation");
                if (name is null || punctuation is null)
                {
                    return Task.CompletedTask;
                }

                var prefix = CoerceNonBlankString(context.Values.TryGetValue("prefix", out var value) ? value : null, "Hello, ");
                context.Emit("text", $"{prefix}{CoerceString(name, "World")}{CoerceString(punctuation, "!")}");
                MarkEmittedOnce(context);
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "math_add",
            DisplayName = "Add Numbers",
            Description = "Add two numeric inputs and emit a sum once both values are available.",
            Category = "Math",
            Inputs =
            [
                new NodePortDefinition { Id = "a", Label = "A", DataType = DemoNumberType },
                new NodePortDefinition { Id = "b", Label = "B", DataType = DemoNumberType },
            ],
            Outputs =
            [
                new NodePortDefinition { Id = "sum", Label = "Sum", DataType = DemoNumberType },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#fff7ed",
                BorderColor = "#f97316",
                TextColor = "#7c2d12",
            },
            ExecuteAsync = context =>
            {
                if (HasEmittedOnce(context))
                {
                    return Task.CompletedTask;
                }

                var a = context.ReadInput("a");
                var b = context.ReadInput("b");
                if (a is null || b is null)
                {
                    return Task.CompletedTask;
                }

                context.Emit("sum", CoerceDouble(a, 0) + CoerceDouble(b, 0));
                MarkEmittedOnce(context);
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "if_text",
            DisplayName = "If (Text)",
            Description = "Select between two text branches based on a boolean condition.",
            Category = "Logic",
            Inputs =
            [
                new NodePortDefinition { Id = "condition", Label = "Condition", DataType = DemoBooleanType },
                new NodePortDefinition { Id = "whenTrue", Label = "When True", DataType = DemoTextType },
                new NodePortDefinition { Id = "whenFalse", Label = "When False", DataType = DemoTextType },
            ],
            Outputs =
            [
                new NodePortDefinition { Id = "text", Label = "Text", DataType = DemoTextType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "fallback",
                    Label = "Fallback",
                    Kind = "text",
                    DefaultValue = "(disabled)",
                    Placeholder = "Used when condition=false and the whenFalse port is not connected.",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#f0fdf4",
                BorderColor = "#22c55e",
                TextColor = "#14532d",
            },
            ExecuteAsync = context =>
            {
                if (HasEmittedOnce(context))
                {
                    return Task.CompletedTask;
                }

                var condition = context.ReadInput("condition");
                var whenTrue = context.ReadInput("whenTrue");
                if (condition is null || whenTrue is null)
                {
                    return Task.CompletedTask;
                }

                if (CoerceBoolean(condition, false))
                {
                    context.Emit("text", CoerceString(whenTrue, string.Empty));
                    MarkEmittedOnce(context);
                    return Task.CompletedTask;
                }

                var whenFalse = context.ReadInput("whenFalse");
                if (whenFalse is not null && !string.IsNullOrWhiteSpace(CoerceString(whenFalse, string.Empty)))
                {
                    context.Emit("text", CoerceString(whenFalse, string.Empty));
                }
                else
                {
                    var fallback = CoerceString(context.Values.TryGetValue("fallback", out var value) ? value : null, "(disabled)");
                    context.Emit("text", fallback);
                }

                MarkEmittedOnce(context);
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "text_interpolate",
            DisplayName = "Text Interpolate",
            Description = "Render a multi-line template by interpolating typed inputs.",
            Category = "Text",
            Inputs =
            [
                new NodePortDefinition { Id = "greeting", Label = "Greeting", DataType = DemoTextType },
                new NodePortDefinition { Id = "lucky", Label = "Lucky Number", DataType = DemoNumberType },
                new NodePortDefinition { Id = "today", Label = "Date", DataType = DemoDateType },
                new NodePortDefinition { Id = "theme", Label = "Theme Color", DataType = DemoColorType },
                new NodePortDefinition { Id = "amount", Label = "Amount", DataType = DemoDecimalType },
            ],
            Outputs =
            [
                new NodePortDefinition { Id = "text", Label = "Text", DataType = DemoTextType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "template",
                    Label = "Template",
                    Kind = "textarea",
                    DefaultValue = DefaultTemplate,
                    Placeholder = "Use tokens like {greeting} to interpolate values.",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#eff6ff",
                BorderColor = "#2563eb",
                TextColor = "#1e3a8a",
            },
            ExecuteAsync = context =>
            {
                if (HasEmittedOnce(context))
                {
                    return Task.CompletedTask;
                }

                var greeting = context.ReadInput("greeting");
                var lucky = context.ReadInput("lucky");
                var today = context.ReadInput("today");
                var theme = context.ReadInput("theme");
                var amount = context.ReadInput("amount");

                if (greeting is null || lucky is null || today is null || theme is null || amount is null)
                {
                    return Task.CompletedTask;
                }

                var template = CoerceNonBlankString(context.Values.TryGetValue("template", out var value) ? value : null, DefaultTemplate);
                var rendered = template
                    .Replace("{greeting}", CoerceString(greeting, string.Empty), StringComparison.Ordinal)
                    .Replace("{lucky}", CoerceDouble(lucky, 0).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    .Replace("{today}", CoerceString(today, string.Empty), StringComparison.Ordinal)
                    .Replace("{theme}", CoerceString(theme, string.Empty), StringComparison.Ordinal)
                    .Replace("{amount}", CoerceString(amount, string.Empty), StringComparison.Ordinal);

                context.Emit("text", rendered);
                MarkEmittedOnce(context);
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_text",
            DisplayName = "Const (Text)",
            Description = "Emit a constant text value.",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "text", Label = "Text", DataType = DemoTextType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "text",
                    Label = "Text",
                    Kind = "textarea",
                    DefaultValue = "Hello",
                    Placeholder = "Constant text emitted by this node.",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#eff6ff",
                BorderColor = "#2563eb",
                TextColor = "#1e3a8a",
            },
            ExecuteAsync = context =>
            {
                context.Emit("text", CoerceString(context.Values.TryGetValue("text", out var value) ? value : null, string.Empty));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_number",
            DisplayName = "Const (Number)",
            Description = "Emit a constant numeric value.",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "number", Label = "Number", DataType = DemoNumberType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "value",
                    Label = "Value",
                    Kind = "double",
                    DefaultValue = 0,
                    Placeholder = "Constant number emitted by this node.",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#fff7ed",
                BorderColor = "#f97316",
                TextColor = "#7c2d12",
            },
            ExecuteAsync = context =>
            {
                context.Emit("number", CoerceDouble(context.Values.TryGetValue("value", out var value) ? value : null, 0));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_boolean",
            DisplayName = "Const (Boolean)",
            Description = "Emit a constant boolean value.",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "value", Label = "Value", DataType = DemoBooleanType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "value",
                    Label = "Value",
                    Kind = "boolean",
                    DefaultValue = true,
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#f0fdf4",
                BorderColor = "#22c55e",
                TextColor = "#14532d",
            },
            ExecuteAsync = context =>
            {
                context.Emit("value", CoerceBoolean(context.Values.TryGetValue("value", out var value) ? value : null, true));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_date",
            DisplayName = "Const (Date)",
            Description = "Emit a constant date string (YYYY-MM-DD).",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "date", Label = "Date", DataType = DemoDateType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "date",
                    Label = "Date",
                    Kind = "date",
                    DefaultValue = "2026-03-21",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#faf5ff",
                BorderColor = "#a855f7",
                TextColor = "#581c87",
            },
            ExecuteAsync = context =>
            {
                context.Emit("date", CoerceString(context.Values.TryGetValue("date", out var value) ? value : null, "2026-03-21"));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_color",
            DisplayName = "Const (Color)",
            Description = "Emit a constant color string (#RRGGBB).",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "color", Label = "Color", DataType = DemoColorType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "color",
                    Label = "Color",
                    Kind = "color",
                    DefaultValue = "#2563eb",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#fff1f2",
                BorderColor = "#e11d48",
                TextColor = "#881337",
            },
            ExecuteAsync = context =>
            {
                context.Emit("color", CoerceString(context.Values.TryGetValue("color", out var value) ? value : null, "#2563eb"));
                return Task.CompletedTask;
            },
        });

        runtime.RegisterNode(new NodeDefinition
        {
            Type = "const_decimal",
            DisplayName = "Const (Decimal)",
            Description = "Emit a decimal value as a string to demonstrate the decimal field editor.",
            Category = "Inputs",
            Outputs =
            [
                new NodePortDefinition { Id = "decimal", Label = "Decimal", DataType = DemoDecimalType },
            ],
            Fields =
            [
                new NodeLibraryFieldDefinition
                {
                    Key = "value",
                    Label = "Value",
                    Kind = "decimal",
                    DefaultValue = "123.45",
                    Placeholder = "A decimal string like 123.45",
                },
            ],
            Appearance = new NodeAppearance
            {
                BgColor = "#ecfeff",
                BorderColor = "#06b6d4",
                TextColor = "#164e63",
            },
            ExecuteAsync = context =>
            {
                context.Emit("decimal", CoerceString(context.Values.TryGetValue("value", out var value) ? value : null, "123.45"));
                return Task.CompletedTask;
            },
        });

        return runtime;
    }

    /// <summary>
    /// 创建默认图或空白图。
    /// </summary>
    public static NodeGraphDocument CreateGraph(string? graphName, string? graphMode)
    {
        var mode = string.Equals(graphMode, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "existing";
        var resolvedName = string.IsNullOrWhiteSpace(graphName)
            ? mode == "new" ? "Blank Demo Showcase Graph" : "Demo Showcase Pipeline"
            : graphName.Trim();

        if (mode == "new")
        {
            return new NodeGraphDocument
            {
                Name = resolvedName,
                Description = "Start from a blank Demo Showcase graph.",
                Nodes = [],
                Edges = [],
                Viewport = new NodeGraphViewport { X = 0, Y = 0, Zoom = 1 },
            };
        }

        return new NodeGraphDocument
        {
            GraphId = "demo-showcase-graph",
            Name = resolvedName,
            Description = "A runnable Demo Showcase graph hosted by the .NET SDK demo.",
            Nodes =
            [
                new NodeGraphNode
                {
                    Id = "node_source",
                    Type = "default",
                    Position = new Position { X = 80, Y = 220 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Demo Source",
                        ["description"] = "Emit typed values to drive the showcase pipeline.",
                        ["category"] = "Playground",
                        ["nodeType"] = "demo_source",
                        ["inputs"] = Array.Empty<object?>(),
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "name",
                                ["label"] = "Name",
                                ["dataType"] = DemoTextType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "punctuation",
                                ["label"] = "Punctuation",
                                ["dataType"] = DemoTextType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "enabled",
                                ["label"] = "Enabled",
                                ["dataType"] = DemoBooleanType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "baseNumber",
                                ["label"] = "Base",
                                ["dataType"] = DemoNumberType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "delta",
                                ["label"] = "Delta",
                                ["dataType"] = DemoNumberType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "today",
                                ["label"] = "Date",
                                ["dataType"] = DemoDateType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "theme",
                                ["label"] = "Theme",
                                ["dataType"] = DemoColorType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "amount",
                                ["label"] = "Amount",
                                ["dataType"] = DemoDecimalType,
                            },
                        },
                        ["values"] = new Dictionary<string, object?>
                        {
                            ["name"] = "Codex",
                            ["punctuation"] = "!",
                            ["enabled"] = true,
                            ["baseNumber"] = 7,
                            ["delta"] = 5,
                            ["today"] = "2026-03-21",
                            ["theme"] = "#2563eb",
                            ["amount"] = "123.45",
                        },
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#0b1220",
                            ["borderColor"] = "#38bdf8",
                            ["textColor"] = "#e0f2fe",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_greet",
                    Type = "default",
                    Position = new Position { X = 420, Y = 140 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Greeting Builder",
                        ["description"] = "Build the greeting message.",
                        ["category"] = "Text",
                        ["nodeType"] = "greeting_builder",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "name",
                                ["label"] = "Name",
                                ["dataType"] = DemoTextType,
                            },
                            new Dictionary<string, object?>
                            {
                                ["id"] = "punctuation",
                                ["label"] = "Punctuation",
                                ["dataType"] = DemoTextType,
                            },
                        },
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Greeting",
                                ["dataType"] = DemoTextType,
                            },
                        },
                        ["values"] = new Dictionary<string, object?>
                        {
                            ["prefix"] = "Hello, ",
                        },
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#eff6ff",
                            ["borderColor"] = "#2563eb",
                            ["textColor"] = "#1e3a8a",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_add",
                    Type = "default",
                    Position = new Position { X = 420, Y = 360 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Add Numbers",
                        ["description"] = "Compute a derived lucky number.",
                        ["category"] = "Math",
                        ["nodeType"] = "math_add",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "a", ["label"] = "A", ["dataType"] = DemoNumberType },
                            new Dictionary<string, object?> { ["id"] = "b", ["label"] = "B", ["dataType"] = DemoNumberType },
                        },
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "sum", ["label"] = "Sum", ["dataType"] = DemoNumberType },
                        },
                        ["values"] = new Dictionary<string, object?>(),
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#fff7ed",
                            ["borderColor"] = "#f97316",
                            ["textColor"] = "#7c2d12",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_gate",
                    Type = "default",
                    Position = new Position { X = 720, Y = 140 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "If (Text)",
                        ["description"] = "Gate the greeting pipeline with a boolean condition.",
                        ["category"] = "Logic",
                        ["nodeType"] = "if_text",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "condition", ["label"] = "Condition", ["dataType"] = DemoBooleanType },
                            new Dictionary<string, object?> { ["id"] = "whenTrue", ["label"] = "When True", ["dataType"] = DemoTextType },
                            new Dictionary<string, object?> { ["id"] = "whenFalse", ["label"] = "When False", ["dataType"] = DemoTextType },
                        },
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "text", ["label"] = "Text", ["dataType"] = DemoTextType },
                        },
                        ["values"] = new Dictionary<string, object?>
                        {
                            ["fallback"] = "(disabled)",
                        },
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#f0fdf4",
                            ["borderColor"] = "#22c55e",
                            ["textColor"] = "#14532d",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_format",
                    Type = "default",
                    Position = new Position { X = 980, Y = 240 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Text Interpolate",
                        ["description"] = "Combine typed inputs into a multi-line summary.",
                        ["category"] = "Text",
                        ["nodeType"] = "text_interpolate",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "greeting", ["label"] = "Greeting", ["dataType"] = DemoTextType },
                            new Dictionary<string, object?> { ["id"] = "lucky", ["label"] = "Lucky Number", ["dataType"] = DemoNumberType },
                            new Dictionary<string, object?> { ["id"] = "today", ["label"] = "Date", ["dataType"] = DemoDateType },
                            new Dictionary<string, object?> { ["id"] = "theme", ["label"] = "Theme Color", ["dataType"] = DemoColorType },
                            new Dictionary<string, object?> { ["id"] = "amount", ["label"] = "Amount", ["dataType"] = DemoDecimalType },
                        },
                        ["outputs"] = new object?[]
                        {
                            new Dictionary<string, object?> { ["id"] = "text", ["label"] = "Text", ["dataType"] = DemoTextType },
                        },
                        ["values"] = new Dictionary<string, object?>
                        {
                            ["template"] = DefaultTemplate,
                        },
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#eff6ff",
                            ["borderColor"] = "#2563eb",
                            ["textColor"] = "#1e3a8a",
                        },
                    },
                },
                new NodeGraphNode
                {
                    Id = "node_output",
                    Type = "default",
                    Position = new Position { X = 1320, Y = 240 },
                    Data = new Dictionary<string, object?>
                    {
                        ["label"] = "Console Output",
                        ["description"] = "Collect the final text into the runtime result buffer.",
                        ["category"] = "Debug",
                        ["nodeType"] = "console_output",
                        ["inputs"] = new object?[]
                        {
                            new Dictionary<string, object?>
                            {
                                ["id"] = "text",
                                ["label"] = "Text",
                                ["dataType"] = DemoTextType,
                            },
                        },
                        ["outputs"] = Array.Empty<object?>(),
                        ["values"] = new Dictionary<string, object?>(),
                        ["appearance"] = new Dictionary<string, object?>
                        {
                            ["bgColor"] = "#f0fdf4",
                            ["borderColor"] = "#16a34a",
                            ["textColor"] = "#14532d",
                        },
                    },
                },
            ],
            Edges =
            [
                new NodeGraphEdge
                {
                    Id = "edge_source_name",
                    Source = "node_source",
                    SourceHandle = "name",
                    Target = "node_greet",
                    TargetHandle = "name",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_punct",
                    Source = "node_source",
                    SourceHandle = "punctuation",
                    Target = "node_greet",
                    TargetHandle = "punctuation",
                },
                new NodeGraphEdge
                {
                    Id = "edge_greet_text",
                    Source = "node_greet",
                    SourceHandle = "text",
                    Target = "node_gate",
                    TargetHandle = "whenTrue",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_enabled",
                    Source = "node_source",
                    SourceHandle = "enabled",
                    Target = "node_gate",
                    TargetHandle = "condition",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_base",
                    Source = "node_source",
                    SourceHandle = "baseNumber",
                    Target = "node_add",
                    TargetHandle = "a",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_delta",
                    Source = "node_source",
                    SourceHandle = "delta",
                    Target = "node_add",
                    TargetHandle = "b",
                },
                new NodeGraphEdge
                {
                    Id = "edge_add_sum",
                    Source = "node_add",
                    SourceHandle = "sum",
                    Target = "node_format",
                    TargetHandle = "lucky",
                },
                new NodeGraphEdge
                {
                    Id = "edge_gate_text",
                    Source = "node_gate",
                    SourceHandle = "text",
                    Target = "node_format",
                    TargetHandle = "greeting",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_today",
                    Source = "node_source",
                    SourceHandle = "today",
                    Target = "node_format",
                    TargetHandle = "today",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_theme",
                    Source = "node_source",
                    SourceHandle = "theme",
                    Target = "node_format",
                    TargetHandle = "theme",
                },
                new NodeGraphEdge
                {
                    Id = "edge_source_amount",
                    Source = "node_source",
                    SourceHandle = "amount",
                    Target = "node_format",
                    TargetHandle = "amount",
                },
                new NodeGraphEdge
                {
                    Id = "edge_format_text",
                    Source = "node_format",
                    SourceHandle = "text",
                    Target = "node_output",
                    TargetHandle = "text",
                },
            ],
            Viewport = new NodeGraphViewport { X = 40, Y = 80, Zoom = 0.85 },
        };
    }
}

/// <summary>
/// Demo 宿主的运行与状态管理器。
/// </summary>
internal sealed class DemoHost
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly NodeGraphRuntime _runtime;
    private readonly NodeGraphClient _client;
    private DemoRegistrationRecord? _lastRegistration;
    private DemoSessionRecord? _lastSession;
    private DemoExecutionRecord? _lastExecution;
    private DemoDebugRecord? _lastDebug;
    private DemoCompletionRecord? _latestCompletion;
    private readonly List<DemoCompletionRecord> _callbackHistory = [];

    public DemoHost(DemoConfig config, NodeGraphRuntime runtime, NodeGraphClient client)
    {
        Config = config;
        _runtime = runtime;
        _client = client;
    }

    /// <summary>
    /// 当前宿主配置。
    /// </summary>
    public DemoConfig Config { get; }

    /// <summary>
    /// 返回 Demo 首页所需的概览数据。
    /// </summary>
    public object GetOverview()
    {
        return new
        {
            message = "NodeGraph .NET Demo Showcase host",
            runtime = BuildRuntimeInfo(),
            library = _runtime.GetLibrary(),
            sampleGraph = HelloWorldFactory.CreateGraph(null, "existing"),
            endpoints = new[]
            {
                "/api/health",
                "/api/runtime/library",
                "/api/runtime/field-options",
                "/api/runtime/register",
                "/api/runtime/execute",
                "/api/runtime/debug/sample",
                "/api/create-session",
                "/api/completed",
                "/api/results/latest",
            },
        };
    }

    /// <summary>
    /// 返回健康检查响应。
    /// </summary>
    public object GetHealth()
    {
        return new
        {
            status = "ok",
            service = "NodeGraph Demo Client (.NET)",
            demoClientBaseUrl = Config.DemoClientBaseUrl,
            nodeGraphBaseUrl = Config.NodeGraphBaseUrl,
            demoDomain = Config.DemoDomain,
            runtime = BuildRuntimeInfo(),
        };
    }

    /// <summary>
    /// 返回当前运行时节点库。
    /// </summary>
    public object GetLibraryPayload()
    {
        return new
        {
            runtime = BuildRuntimeInfo(),
            library = _runtime.GetLibrary(),
        };
    }

    /// <summary>
    /// 注册当前运行时并保存最近一次结果。
    /// </summary>
    public async Task<RuntimeRegistrationResponse> RegisterRuntimeAsync(bool force)
    {
        await _gate.WaitAsync();
        try
        {
            var response = await _runtime.EnsureRegisteredAsync(_client, force);
            _lastRegistration = new DemoRegistrationRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                force,
                _runtime.CreateRegistrationRequest(),
                response);
            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 创建编辑会话，并确保注册信息在会话前完成。
    /// </summary>
    public async Task<CreateSessionResponse> CreateSessionAsync(string? graphMode, string? graphName, bool forceRefresh)
    {
        await _gate.WaitAsync();
        try
        {
            var registration = await _runtime.EnsureRegisteredAsync(_client, forceRefresh);
            _lastRegistration = new DemoRegistrationRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                forceRefresh,
                _runtime.CreateRegistrationRequest(),
                registration);

            var mode = string.Equals(graphMode, "new", StringComparison.OrdinalIgnoreCase) ? "new" : "existing";
            var resolvedName = string.IsNullOrWhiteSpace(graphName)
                ? mode == "new" ? "Blank Demo Showcase Graph" : "Demo Showcase Pipeline"
                : graphName.Trim();
            var response = await _client.CreateSessionAsync(new CreateSessionRequest
            {
                RuntimeId = _runtime.RuntimeId,
                CompletionWebhook = $"{Config.DemoClientBaseUrl}/api/completed",
                Graph = HelloWorldFactory.CreateGraph(resolvedName, mode),
                Metadata = new Dictionary<string, string>
                {
                    ["graphMode"] = mode,
                    ["source"] = "NodeGraph.DemoClient.DotNet.Showcase",
                },
            });

            _lastSession = new DemoSessionRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                new DemoSessionRequest(mode, resolvedName, forceRefresh),
                registration,
                response);

            return response;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 执行 Hello World 图并返回 profiler 快照。
    /// </summary>
    public async Task<DemoExecutionRecord> ExecuteAsync(string? graphMode, string? graphName)
    {
        await _gate.WaitAsync();
        try
        {
            var graph = HelloWorldFactory.CreateGraph(graphName, graphMode);
            var snapshot = await _runtime.ExecuteGraphAsync(graph);
            _lastExecution = new DemoExecutionRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                graph,
                snapshot);
            return _lastExecution;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 运行一次带断点的调试样例。
    /// </summary>
    public async Task<DemoDebugRecord> DebugSampleAsync(string? graphMode, string? graphName)
    {
        await _gate.WaitAsync();
        try
        {
            var graph = HelloWorldFactory.CreateGraph(graphName, graphMode);
            var debugger = _runtime.CreateDebugger(graph, new NodeGraphExecutionOptions
            {
                Breakpoints = ["node_output"],
            });

            var firstStep = await debugger.StepAsync();
            var paused = await debugger.ContinueAsync();
            var completed = await debugger.ContinueAsync();

            _lastDebug = new DemoDebugRecord(
                DateTimeOffset.UtcNow.ToString("O"),
                graph,
                new[] { "node_output" },
                firstStep,
                paused,
                completed);
            return _lastDebug;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 存储最近一次编辑完成回调。
    /// </summary>
    public async Task<object> StoreCompletionAsync(JsonElement payload)
    {
        await _gate.WaitAsync();
        try
        {
            var record = new DemoCompletionRecord(DateTimeOffset.UtcNow.ToString("O"), payload);
            _latestCompletion = record;
            _callbackHistory.Add(record);

            while (_callbackHistory.Count > 10)
            {
                _callbackHistory.RemoveAt(0);
            }

            return new
            {
                success = true,
                receivedAt = record.ReceivedAt,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// 返回最近一次注册、执行、调试与回调状态。
    /// </summary>
    public async Task<object> GetLatestAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return new
            {
                runtime = BuildRuntimeInfo(),
                lastRegistration = _lastRegistration,
                lastSession = _lastSession,
                lastExecution = _lastExecution,
                lastDebug = _lastDebug,
                latestCompletion = _latestCompletion,
                callbackCount = _callbackHistory.Count,
            };
        }
        finally
        {
            _gate.Release();
        }
    }

    private object BuildRuntimeInfo()
    {
        return new
        {
            runtimeId = _runtime.RuntimeId,
            domain = _runtime.Domain,
            libraryVersion = _runtime.LibraryVersion,
            controlBaseUrl = _runtime.ControlBaseUrl,
            capabilities = _runtime.Capabilities,
        };
    }
}

/// <summary>
/// 运行时注册接口的请求体。
/// </summary>
internal sealed record RegisterRequest(bool Force);

/// <summary>
/// 基于图模式和图名称的请求体。
/// </summary>
internal sealed record GraphRequest(string? GraphMode, string? GraphName);

/// <summary>
/// 创建会话时的扩展请求体。
/// </summary>
internal sealed record CreateSessionPayload(string? GraphMode, string? GraphName, bool ForceRefresh);

/// <summary>
/// 最近一次注册记录。
/// </summary>
internal sealed record DemoRegistrationRecord(
    string RegisteredAt,
    bool Force,
    RuntimeRegistrationRequest Request,
    RuntimeRegistrationResponse Response);

/// <summary>
/// 最近一次会话创建请求。
/// </summary>
internal sealed record DemoSessionRequest(
    string GraphMode,
    string GraphName,
    bool ForceRefresh);

/// <summary>
/// 最近一次会话创建记录。
/// </summary>
internal sealed record DemoSessionRecord(
    string CreatedAt,
    DemoSessionRequest Request,
    RuntimeRegistrationResponse Registration,
    CreateSessionResponse Response);

/// <summary>
/// 最近一次执行记录。
/// </summary>
internal sealed record DemoExecutionRecord(
    string ExecutedAt,
    NodeGraphDocument Graph,
    NodeGraphExecutionSnapshot Snapshot);

/// <summary>
/// 最近一次断点调试记录。
/// </summary>
internal sealed record DemoDebugRecord(
    string DebuggedAt,
    NodeGraphDocument Graph,
    IReadOnlyList<string> Breakpoints,
    NodeGraphExecutionSnapshot FirstStep,
    NodeGraphExecutionSnapshot Paused,
    NodeGraphExecutionSnapshot Completed);

/// <summary>
/// 最近一次编辑完成回调记录。
/// </summary>
internal sealed record DemoCompletionRecord(
    string ReceivedAt,
    JsonElement Payload);
