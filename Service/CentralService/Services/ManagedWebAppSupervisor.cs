using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace CentralService.Services;

public sealed class ManagedWebAppSupervisor : BackgroundService
{
    private readonly ManagedWebAppHostOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IManagedProcessLifetimeBinder _processLifetimeBinder;
    private readonly CentralServiceBackgroundTaskMonitor _taskMonitor;
    private readonly ILogger<ManagedWebAppSupervisor> _logger;
    private readonly Dictionary<string, ManagedWebAppRuntime> _runtimes = new(StringComparer.OrdinalIgnoreCase);

    public ManagedWebAppSupervisor(
        IOptions<ManagedWebAppHostOptions> options,
        IHostEnvironment hostEnvironment,
        IHttpClientFactory httpClientFactory,
        IManagedProcessLifetimeBinder processLifetimeBinder,
        CentralServiceBackgroundTaskMonitor taskMonitor,
        ILogger<ManagedWebAppSupervisor> logger)
    {
        _options = options?.Value ?? new ManagedWebAppHostOptions();
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _processLifetimeBinder = processLifetimeBinder ?? throw new ArgumentNullException(nameof(processLifetimeBinder));
        _taskMonitor = taskMonitor ?? throw new ArgumentNullException(nameof(taskMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var definitions = GetEnabledDefinitions();
        if (definitions.Count == 0)
        {
            _logger.LogInformation("未配置启用的托管 Web 站点，跳过宿主管理。");
            return;
        }

        _logger.LogInformation("托管 Web 站点宿主已启动，共 {Count} 个站点。", definitions.Count);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var definition in definitions)
                {
                    try
                    {
                        await EnsureHealthyAsync(definition, stoppingToken);
                        _taskMonitor.MarkSuccess(definition.GetTaskName());
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _taskMonitor.MarkError(definition.GetTaskName(), ex);
                        _logger.LogError(ex, "托管站点 {Name} 守护失败。", definition.Name);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(GetPollIntervalSeconds()), stoppingToken);
            }
        }
        finally
        {
            await StopAllAsync(CancellationToken.None);
            _logger.LogInformation("托管 Web 站点宿主已停止。");
        }
    }

    private IReadOnlyList<ManagedWebAppDefinitionOptions> GetEnabledDefinitions()
    {
        var definitions = new List<ManagedWebAppDefinitionOptions>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in _options.Definitions ?? new List<ManagedWebAppDefinitionOptions>())
        {
            if (definition == null || !definition.Enabled)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.Name))
            {
                _logger.LogWarning("发现未命名的托管站点配置，已跳过。");
                continue;
            }

            if (!names.Add(definition.Name))
            {
                _logger.LogWarning("托管站点 {Name} 存在重复配置，后续项已跳过。", definition.Name);
                continue;
            }

            definitions.Add(definition);
        }

        return definitions;
    }

    private async Task EnsureHealthyAsync(ManagedWebAppDefinitionOptions definition, CancellationToken cancellationToken)
    {
        var runtime = GetOrCreateRuntime(definition.Name);
        var process = runtime.Process;

        if (process == null || process.HasExited)
        {
            runtime.ConsecutiveHealthFailures = 0;
            await RestartAsync(definition, runtime, "进程未运行", cancellationToken);
            return;
        }

        if (await ProbeHealthAsync(definition, cancellationToken))
        {
            runtime.ConsecutiveHealthFailures = 0;
            runtime.LastHealthyAtUtc = DateTime.UtcNow;
            return;
        }

        runtime.ConsecutiveHealthFailures++;
        var maxFailures = Math.Max(1, definition.MaxConsecutiveHealthFailures);

        if (runtime.ConsecutiveHealthFailures < maxFailures)
        {
            throw new InvalidOperationException(
                $"托管站点 {definition.Name} 健康检查失败 ({runtime.ConsecutiveHealthFailures}/{maxFailures})。");
        }

        await RestartAsync(
            definition,
            runtime,
            $"连续健康检查失败达到阈值 ({runtime.ConsecutiveHealthFailures}/{maxFailures})",
            cancellationToken);
    }

    private async Task RestartAsync(
        ManagedWebAppDefinitionOptions definition,
        ManagedWebAppRuntime runtime,
        string reason,
        CancellationToken cancellationToken)
    {
        await StopProcessAsync(runtime, reason, cancellationToken);

        var restartDelaySeconds = Math.Max(0, definition.RestartDelaySeconds);
        if (restartDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(restartDelaySeconds), cancellationToken);
        }

        await StartAndWaitUntilReadyAsync(definition, runtime, cancellationToken);
    }

    private async Task StartAndWaitUntilReadyAsync(
        ManagedWebAppDefinitionOptions definition,
        ManagedWebAppRuntime runtime,
        CancellationToken cancellationToken)
    {
        var workingDirectory = definition.ResolveWorkingDirectory(_hostEnvironment);
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"托管站点 {definition.Name} 的工作目录不存在: {workingDirectory}");
        }

        if (string.IsNullOrWhiteSpace(definition.Command))
        {
            throw new InvalidOperationException($"托管站点 {definition.Name} 未配置启动命令。");
        }

        await EnsurePreparationAsync(definition, workingDirectory, cancellationToken);

        var startInfo = ManagedWebAppProcessStartInfoFactory.Create(definition.Command, definition.Arguments);
        startInfo.WorkingDirectory = workingDirectory;
        ApplyEnvironmentVariables(startInfo, definition.EnvironmentVariables);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        process.OutputDataReceived += (_, args) => LogProcessLine(definition.Name, args.Data, isError: false);
        process.ErrorDataReceived += (_, args) => LogProcessLine(definition.Name, args.Data, isError: true);
        process.Exited += (_, _) =>
        {
            _logger.LogWarning(
                "托管站点 {Name} 进程已退出，ExitCode={ExitCode}。",
                definition.Name,
                SafeGetExitCode(process));
        };

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"托管站点 {definition.Name} 启动失败，进程未进入运行态。");
        }

        // 进程启动后立刻绑定到宿主生命周期，确保宿主异常死亡时由 OS 统一回收整棵进程树。
        await EnsureBoundToHostLifetimeAsync(definition.Name, process, isPreparation: false);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        runtime.Process = process;
        runtime.LastStartedAtUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "托管站点 {Name} 已启动，PID={Pid}，命令={Command}。",
            definition.Name,
            process.Id,
            BuildCommandPreview(startInfo));

        await WaitUntilReadyAsync(definition, runtime, cancellationToken);
    }

    private async Task EnsurePreparationAsync(
        ManagedWebAppDefinitionOptions definition,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        var missingPaths = GetMissingRequiredPaths(definition, workingDirectory);
        if (missingPaths.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(definition.PrepareCommand))
        {
            throw new InvalidOperationException(
                $"托管站点 {definition.Name} 缺少启动前置产物: {string.Join(", ", missingPaths)}。请先准备产物，或为该站点配置 PrepareCommand。");
        }

        _logger.LogInformation(
            "托管站点 {Name} 缺少前置产物 {MissingPaths}，开始执行预启动步骤。",
            definition.Name,
            string.Join(", ", missingPaths));

        var prepareStartInfo = ManagedWebAppProcessStartInfoFactory.Create(definition.PrepareCommand, definition.PrepareArguments);
        prepareStartInfo.WorkingDirectory = workingDirectory;
        ApplyEnvironmentVariables(prepareStartInfo, definition.EnvironmentVariables);

        using var prepareProcess = new Process
        {
            StartInfo = prepareStartInfo,
            EnableRaisingEvents = true,
        };

        prepareProcess.OutputDataReceived += (_, args) => LogPreparationLine(definition.Name, args.Data, isError: false);
        prepareProcess.ErrorDataReceived += (_, args) => LogPreparationLine(definition.Name, args.Data, isError: true);

        if (!prepareProcess.Start())
        {
            throw new InvalidOperationException($"托管站点 {definition.Name} 的预启动步骤未能成功启动。");
        }

        await EnsureBoundToHostLifetimeAsync(definition.Name, prepareProcess, isPreparation: true);
        prepareProcess.BeginOutputReadLine();
        prepareProcess.BeginErrorReadLine();

        var timeout = TimeSpan.FromSeconds(Math.Clamp(definition.PrepareTimeoutSeconds, 5, 3600));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await prepareProcess.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await StopPreparationProcessAsync(definition.Name, prepareProcess, "预启动步骤超时");
            throw new TimeoutException(
                $"托管站点 {definition.Name} 的预启动步骤超时，{timeout.TotalSeconds:0} 秒内未完成。命令: {BuildCommandPreview(prepareStartInfo)}");
        }

        if (prepareProcess.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"托管站点 {definition.Name} 的预启动步骤失败，ExitCode={prepareProcess.ExitCode}，命令: {BuildCommandPreview(prepareStartInfo)}");
        }

        var missingAfterPrepare = GetMissingRequiredPaths(definition, workingDirectory);
        if (missingAfterPrepare.Count > 0)
        {
            throw new InvalidOperationException(
                $"托管站点 {definition.Name} 的预启动步骤已完成，但仍缺少前置产物: {string.Join(", ", missingAfterPrepare)}。");
        }

        _logger.LogInformation("托管站点 {Name} 的预启动步骤已完成。", definition.Name);
    }

    private async Task WaitUntilReadyAsync(
        ManagedWebAppDefinitionOptions definition,
        ManagedWebAppRuntime runtime,
        CancellationToken cancellationToken)
    {
        var startupTimeout = TimeSpan.FromSeconds(Math.Clamp(definition.StartupTimeoutSeconds, 5, 300));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(startupTimeout);

        Exception? lastError = null;
        while (!timeoutCts.IsCancellationRequested)
        {
            var process = runtime.Process;
            if (process == null || process.HasExited)
            {
                throw new InvalidOperationException(
                    $"托管站点 {definition.Name} 在完成就绪探测前已退出，ExitCode={SafeGetExitCode(process)}。");
            }

            try
            {
                if (await ProbeHealthAsync(definition, timeoutCts.Token))
                {
                    runtime.ConsecutiveHealthFailures = 0;
                    runtime.LastHealthyAtUtc = DateTime.UtcNow;
                    _logger.LogInformation("托管站点 {Name} 已通过就绪检查。", definition.Name);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token);
        }

        await StopProcessAsync(runtime, "启动超时", CancellationToken.None);
        throw new TimeoutException(
            $"托管站点 {definition.Name} 在 {startupTimeout.TotalSeconds:0} 秒内未通过健康检查。最近错误: {lastError?.Message ?? "无"}");
    }

    private async Task<bool> ProbeHealthAsync(ManagedWebAppDefinitionOptions definition, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(definition.HealthCheckUrl))
        {
            return true;
        }

        var timeoutSeconds = Math.Clamp(definition.HealthCheckTimeoutSeconds, 1, 60);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        using var client = _httpClientFactory.CreateClient(nameof(ManagedWebAppSupervisor));
        using var response = await client.GetAsync(definition.HealthCheckUrl, timeoutCts.Token);
        return response.IsSuccessStatusCode;
    }

    private static List<string> GetMissingRequiredPaths(
        ManagedWebAppDefinitionOptions definition,
        string workingDirectory)
    {
        var missingPaths = new List<string>();

        foreach (var rawPath in definition.RequiredPaths ?? new List<string>())
        {
            var relativePath = (rawPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                continue;
            }

            var absolutePath = Path.IsPathRooted(relativePath)
                ? Path.GetFullPath(relativePath)
                : Path.GetFullPath(Path.Combine(workingDirectory, relativePath));

            if (!File.Exists(absolutePath) && !Directory.Exists(absolutePath))
            {
                missingPaths.Add(relativePath);
            }
        }

        return missingPaths;
    }

    private async Task StopAllAsync(CancellationToken cancellationToken)
    {
        foreach (var runtime in _runtimes.Values)
        {
            await StopProcessAsync(runtime, "宿主停止", cancellationToken);
        }
    }

    private async Task StopProcessAsync(
        ManagedWebAppRuntime runtime,
        string reason,
        CancellationToken cancellationToken)
    {
        var process = runtime.Process;
        runtime.Process = null;
        runtime.ConsecutiveHealthFailures = 0;

        if (process == null)
        {
            return;
        }

        try
        {
            await StopExternalProcessAsync(runtime.Name, process, reason, cancellationToken, isPreparation: false);
        }
        catch (OperationCanceledException)
        {
            // 停机路径无需再次抛出。
        }
        catch (InvalidOperationException)
        {
            // 进程可能已经退出。
        }
        finally
        {
            process.Dispose();
        }
    }

    private async Task StopPreparationProcessAsync(string name, Process process, string reason)
    {
        try
        {
            await StopExternalProcessAsync(name, process, reason, CancellationToken.None, isPreparation: true);
        }
        catch (InvalidOperationException)
        {
            // 进程可能已经退出。
        }
    }

    private ManagedWebAppRuntime GetOrCreateRuntime(string name)
    {
        if (!_runtimes.TryGetValue(name, out var runtime))
        {
            runtime = new ManagedWebAppRuntime(name);
            _runtimes[name] = runtime;
        }

        return runtime;
    }

    private int GetPollIntervalSeconds()
    {
        return Math.Clamp(_options.PollIntervalSeconds, 1, 300);
    }

    private void LogProcessLine(string name, string? line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (isError)
        {
            _logger.LogWarning("[ManagedWebApp:{Name}] {Line}", name, line);
            return;
        }

        _logger.LogInformation("[ManagedWebApp:{Name}] {Line}", name, line);
    }

    private void LogPreparationLine(string name, string? line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (isError)
        {
            _logger.LogWarning("[ManagedWebApp:{Name}:Prepare] {Line}", name, line);
            return;
        }

        _logger.LogInformation("[ManagedWebApp:{Name}:Prepare] {Line}", name, line);
    }

    private static void ApplyEnvironmentVariables(
        ProcessStartInfo startInfo,
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        foreach (var pair in environmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(pair.Key))
            {
                startInfo.Environment[pair.Key] = pair.Value ?? string.Empty;
            }
        }
    }

    private static int? SafeGetExitCode(Process? process)
    {
        if (process == null)
        {
            return null;
        }

        try
        {
            return process.HasExited ? process.ExitCode : null;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildCommandPreview(ProcessStartInfo startInfo)
    {
        var parts = new List<string> { startInfo.FileName };
        parts.AddRange(startInfo.ArgumentList);
        return string.Join(' ', parts.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private async Task EnsureBoundToHostLifetimeAsync(string name, Process process, bool isPreparation)
    {
        try
        {
            _processLifetimeBinder.Attach(process);
        }
        catch
        {
            await StopExternalProcessAsync(name, process, "宿主生命周期绑定失败", CancellationToken.None, isPreparation);
            throw;
        }
    }

    private async Task StopExternalProcessAsync(
        string name,
        Process process,
        string reason,
        CancellationToken cancellationToken,
        bool isPreparation)
    {
        if (process.HasExited)
        {
            return;
        }

        if (isPreparation)
        {
            _logger.LogWarning(
                "准备停止托管站点 {Name} 的预启动步骤进程，原因: {Reason}，PID={Pid}。",
                name,
                reason,
                process.Id);
        }
        else
        {
            _logger.LogWarning(
                "准备停止托管站点 {Name}，原因: {Reason}，PID={Pid}。",
                name,
                reason,
                process.Id);
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync(cancellationToken);
    }

    private sealed class ManagedWebAppRuntime
    {
        public ManagedWebAppRuntime(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Process? Process { get; set; }

        public int ConsecutiveHealthFailures { get; set; }

        public DateTime? LastStartedAtUtc { get; set; }

        public DateTime? LastHealthyAtUtc { get; set; }
    }
}
