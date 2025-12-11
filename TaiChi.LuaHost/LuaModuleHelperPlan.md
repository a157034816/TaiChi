# LuaModuleHelper 设计与优化计划

## 1. 现状评估
- `TaiChi.LuaHost.LuaFileHelperModule.CreateModuleTable` 通过逐条赋值把 14 个 C# 处理函数包装成 `LuaFunction`，随着模块扩展手写“胶水代码”迅速膨胀。
- handler 中重复 `context.GetArgument<T>`、默认值回退、异常包装等逻辑，模块间风格与鲁棒性不一致。
- 新增或改名时需要手动同步 `table["name"] = ...` 与方法体，极易遗漏单元测试。

## 2. 核心痛点
1. **异步 handler 映射不足**：Builder 仅接受 `ValueTask<int>` 委托，`Task`/`Task<T>`/`ValueTask<T>` 无法直接注册，示例与真实能力存在落差。
2. **反射映射范围失控**：默认扫描所有公共方法自动生成 `snake_case` 名称，缺少白名单、命名冲突检测，易将内部 API 暴露给 Lua。
3. **默认值与 `params` 丢失**：反射调用未读取 `ParameterInfo.DefaultValue`，Lua 侧必须显式传参；`params` 无法展开，体验割裂。
4. **缺少自动化回归**：Builder 的边界场景没有测试覆盖，迁移新模块存在暗坑。

## 3. 总体目标
- 编写模块时专注业务 handler，最小化样板代码。
- 提供统一注册入口，内建异常处理、参数解析、结果返回约定。
- 默认只映射明确声明的 API，并对命名冲突给出清晰错误。
- 支持同步与异步统一映射，完整恢复默认值、`params` 与特殊注入参数。
- 建立完善的单元测试与 Lua 端到端回归，保障模块迁移稳定。
- 可渐进迁移：优先让 `LuaFileHelperModule` 落地验证，再推广其他模块。

## 4. 通用帮助类设计
### 4.1 核心组件
- `LuaModuleHelper`
  - `RegisterModule(LuaState state, string globalName, Action<LuaModuleBuilder> buildAction)`：构建 `LuaTable` 并写入 `state.Environment`。
  - `CreateTable(Action<LuaModuleBuilder> buildAction)`：返回 `LuaTable`，方便独立测试或特殊场景。
- `LuaModuleBuilder`
  - 维护 `Dictionary<string, LuaModuleFunctionDescriptor>` 与命名冲突检测。
  - `AddFunction(string name, Func<LuaFunctionExecutionContext, CancellationToken, ValueTask<int>> handler)`：注册底层委托。
  - `AddSyncFunction(string name, Func<LuaCallContext, object?> handler)`：同步委托统一包装。
  - `AddAsyncFunction(string name, Func<LuaCallContext, CancellationToken, Task> handler)` 与 `AddAsyncFunction<T>`：统一转换为 `ValueTask<int>` 写栈逻辑。
  - `MapObject(object target)` / `MapStaticClass<T>()`：结合 `MapOptions` 进行批量映射。
  - 支持注入 `ILuaValueWriter`（或后续 `ILuaValueConverter`）扩展写栈能力。
- `LuaModuleMethodAttribute`：描述 Lua 暴露名称及参数策略，例如 `[LuaModuleMethod("read_text", MinArgs = 1, MaxArgs = 3)]`。
- `LuaCallContext`：包装 `LuaFunctionExecutionContext`，提供强类型取参、默认值、必填校验与 `Return()` 辅助。

### 4.2 参数与结果处理
- `LuaCallContext.Get<T>(int index, T? defaultValue = default)` 封装 `HasArgument`/`GetArgument<T>`，缺参返回默认值。
- 解析 `ParameterInfo.DefaultValue` 与 `HasDefaultValue`，必要时使用 `Activator.CreateInstance` 生成值类型默认值。
- 对 `params T[]` 参数执行聚合：Lua 端多余实参打包成数组，缺失时提供空数组。
- `LuaArgAttribute` 支持指定 Lua 名称、默认值表达式（如 `DateTime.Now`）及可空性。
- 返回值统一通过 `ILuaValueWriter` 写入 Lua 堆栈，`void`/`Task` 默认 `context.Return()`，`Task<T>`/`ValueTask<T>` 自动 await。
- 捕获所有非 `LuaMappingException` 并转换为带上下文的 `LuaMappingException`，确保模块间表现一致。

### 4.3 线程与生命周期
- Builder 在 `Build()` 时创建 `LuaTable` 与 `LuaFunction`，由 `LuaFunction` 持有 handler 委托，`LuaTable` 生命周期交由调用方。
- 透传 `CancellationToken` 以兼容原有 async handler。
- 在反射阶段缓存 `MethodDescriptor`，避免运行期重复分析并降低性能开销。

### 4.4 示例
```csharp
LuaModuleHelper.RegisterModule(state, "FileHelper", builder =>
{
    builder.MapStaticClass<FileHelperBindings>();
});

public static class FileHelperBindings
{
    [LuaModuleMethod("read_text")]
    public static string ReadText(string filePath, Encoding? encoding = null)
        => FileHelper.ReadFile(filePath, encoding ?? Encoding.UTF8) ?? string.Empty;

    [LuaModuleMethod("write_text")]
    public static void WriteText(string filePath, string content, Encoding? encoding = null, bool append = false)
        => FileHelper.WriteFile(filePath, content, encoding ?? Encoding.UTF8, append);
}
```
`MapStaticClass` 负责 LuaFunction 包裹、参数默认值、返回值与异常转换，新增同步/异步方法仅需在绑定类中定义并加特性即可。

## 5. MapObject / MapStaticClass 策略
- **白名单优先**：默认只处理 `[LuaModuleMethod]` 成员，通过 `MapOptions.IncludeImplicitMembers` 才 opt-in 其他公共方法。
- **命名策略抽象**：引入 `ILuaNamingStrategy`，默认 `SnakeCaseNamingStrategy`，可通过 `MapOptions` 替换；`[LuaModuleIgnore]` 协助 opt-in 模式排除个别方法。
- **命名冲突检测**：构建阶段对名称做大小写不敏感比较，一旦重复立即抛出异常并指明来源。
- **参数推断**：`CancellationToken`、`LuaCallContext` 等特殊参数自动注入，其余参数依赖 `LuaArgAttribute`、默认值推断，支持 `params`。
- **返回值写栈**：通过 `ILuaValueWriter` 统一写入 Lua，便于扩展自定义类型。
- **异常策略**：所有异常统一转译为 `LuaMappingException` 并附带上下文。
- **适用场景**：`MapObject` 适合需要实例状态或依赖注入的模块，可配合 `[LuaModuleProperty]` 导出属性；`MapStaticClass` 适合纯工具类。

## 6. 优化方案
### 6.1 异步能力补齐
1. 为 `LuaModuleBuilder` 增加 `AddAsyncFunction`/`AddAsyncFunction<T>` 重载，内部统一转换为 `ValueTask<int>`。
2. `MapObject/MapStaticClass` 根据方法返回类型选择同步或异步包装：
   - `void`/`Task` → 调用后 `context.Return()`。
   - `T`/`Task<T>`/`ValueTask<T>` → await 结果后写入 Lua。
3. 引入 `ILuaValueWriter` 作为可插拔写栈组件，便于未来扩展。
4. 新增测试用例：同步/异步混合注册、异常传播、取消令牌透传、多线程压力。

### 6.2 映射范围与命名策略
1. 默认仅映射 `[LuaModuleMethod]`；通过 `MapOptions.IncludeImplicitMembers` 明确放宽。
2. 引入 `[LuaModuleIgnore]` 特性，在 opt-in 模式下排除个别方法。
3. 构建阶段检测重复导出（大小写不敏感），冲突时抛出包含类型与方法信息的异常。
4. 抽象 `ILuaNamingStrategy`，默认 `SnakeCaseNamingStrategy`，支持自定义策略。

### 6.3 参数默认值与 `params`
1. 解析 `ParameterInfo.DefaultValue` 并在 `LuaCallContext.Get<T>` 中透传；遇到 `DBNull` 值类型使用 `Activator.CreateInstance`。
2. 专门处理 `params T[]`：Lua 端多余参数组装成数组，没有则提供空数组。
3. 扩展 `LuaArgAttribute`，允许声明默认值表达式与 Lua 名称，覆盖 `DateTime.Now` 等运行时默认。
4. 增强单元测试：覆盖默认值缺省、`params` 混用、必填校验、特殊注入参数共存等场景。

### 6.4 回归与迁移
1. 在 `LuaModuleHelper.Tests` 中新增面向 Lua 的端到端测试（`LuaState.DoString`），验证 `read_text`、`write_text`、`exists` 等核心函数。
2. 发布迁移指南：演示从 `AddFunction` 过渡到 `MapStaticClass`，包含异步、默认值、异常处理说明。
3. 先在 `LuaFileHelperModule` 试点新能力，通过代码评审后推广至 `LuaNetworkModule` 等模块。
4. 维护脚本化回归流程，纳入 CI 并提供命令行入口。

## 7. 实施步骤
1. 在 `TaiChi.LuaHost` 中新增 `LuaModuleHelper`、`LuaModuleBuilder`、`LuaModuleMethodAttribute`、`LuaCallContext`、`ILuaNamingStrategy`、`ILuaValueWriter` 等核心类型。
2. 补齐 builder 单元测试：单函数注册、批量映射、命名冲突、默认值/必填、`params` 与异步路径。
3. 在反射阶段实现 `MethodDescriptor` 缓存与配置化映射，验证命名冲突检测。
4. 改造 `LuaFileHelperModule`，使用 `LuaModuleHelper.CreateTable(builder => ...)` 替换逐条赋值逻辑。
5. 为 `LuaFileHelperModule` 编写回归脚本（至少覆盖读取、写入、存在性判断、异常场景）。
6. 输出迁移指引与 FAQ，指导后续模块按统一约定迁移。

## 8. 验证与风险
- **验证**：单元测试 + Lua 集成测试覆盖，`LuaModuleHelper.Tests` 纳入 CI；关键模块运行脚本回归。
- **风险与缓解**：
  - 反射性能开销增加 → 在 Builder 构造阶段缓存 `MethodDescriptor`，避免运行期重复分析。
  - 异步包装错误导致栈污染 → 通过多线程压力测试与 Lua GC 观察，确保 `LuaFunction` 生命周期一致。
  - 默认值恢复兼容性差异 → CI 中启用 `net6.0`/`net8.0` 双目标测试，提前发现差异。

## 9. 输出物
- 迭代后的 `LuaModuleHelper` 及其单元测试。
- 《LuaModuleHelper 迁移指南 v2》与示例代码片段。
- Lua 回归脚本与自动化执行流程，保证功能可验证。

## 10. 里程碑
- **M1**：完成 Builder 能力扩展（异步、命名策略、默认值）及单元测试。
- **M2**：完成 `LuaFileHelperModule` 迁移验证，Lua 回归测试进入 CI。
- **M3**：整理迁移文档、示例仓库片段，并在其他模块推广。
