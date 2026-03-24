using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Xunit;

namespace NodeGraph.DemoClient.DotNet.Tests;

/// <summary>
/// 验证 .NET Demo 宿主的调试会话接口支持在同一会话内动态修改断点。
/// </summary>
public sealed class DebugSessionEndpointsTests
{
    [Fact]
    public async Task RuntimeDebugSessionEndpointsSupportDynamicBreakpointUpdatesAsync()
    {
        await using var process = await DemoHostProcess.StartAsync();
        using var client = new HttpClient
        {
            BaseAddress = process.BaseAddress,
        };

        using var overviewResponse = await client.GetAsync("/");
        overviewResponse.EnsureSuccessStatusCode();
        using var overviewPayload = JsonDocument.Parse(await overviewResponse.Content.ReadAsStringAsync());
        var sampleGraph = overviewPayload.RootElement.GetProperty("sampleGraph").Clone();

        using var createdResponse = await client.PostAsync(
            "/api/runtime/debug/sessions",
            CreateJsonContent(new
            {
                graph = sampleGraph,
                breakpoints = Array.Empty<string>(),
            }));
        Assert.Equal(System.Net.HttpStatusCode.Created, createdResponse.StatusCode);
        using var createdPayload = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync());
        Assert.Equal("idle", createdPayload.RootElement.GetProperty("snapshot").GetProperty("status").GetString());
        Assert.Empty(createdPayload.RootElement.GetProperty("breakpoints").EnumerateArray());

        var debugSessionId = createdPayload.RootElement.GetProperty("debugSessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(debugSessionId));

        using var updateResponse = await client.PutAsync(
            $"/api/runtime/debug/sessions/{debugSessionId}/breakpoints",
            CreateJsonContent(new
            {
                breakpoints = new[] { "node_output" },
            }));
        Assert.Equal(System.Net.HttpStatusCode.OK, updateResponse.StatusCode);
        using var updatePayload = JsonDocument.Parse(await updateResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            new[] { "node_output" },
            updatePayload.RootElement.GetProperty("breakpoints").EnumerateArray().Select(entry => entry.GetString()).ToArray());

        using var pausedResponse = await client.PostAsync($"/api/runtime/debug/sessions/{debugSessionId}/continue", content: null);
        Assert.Equal(System.Net.HttpStatusCode.OK, pausedResponse.StatusCode);
        using var pausedPayload = JsonDocument.Parse(await pausedResponse.Content.ReadAsStringAsync());
        Assert.Equal("paused", pausedPayload.RootElement.GetProperty("snapshot").GetProperty("status").GetString());
        Assert.Equal("breakpoint", pausedPayload.RootElement.GetProperty("snapshot").GetProperty("pauseReason").GetString());
        Assert.Equal("node_output", pausedPayload.RootElement.GetProperty("snapshot").GetProperty("pendingNodeId").GetString());

        using var clearResponse = await client.PutAsync(
            $"/api/runtime/debug/sessions/{debugSessionId}/breakpoints",
            CreateJsonContent(new
            {
                breakpoints = Array.Empty<string>(),
            }));
        Assert.Equal(System.Net.HttpStatusCode.OK, clearResponse.StatusCode);
        using var clearPayload = JsonDocument.Parse(await clearResponse.Content.ReadAsStringAsync());
        Assert.Empty(clearPayload.RootElement.GetProperty("breakpoints").EnumerateArray());

        using var completedResponse = await client.PostAsync($"/api/runtime/debug/sessions/{debugSessionId}/continue", content: null);
        Assert.Equal(System.Net.HttpStatusCode.OK, completedResponse.StatusCode);
        using var completedPayload = JsonDocument.Parse(await completedResponse.Content.ReadAsStringAsync());
        Assert.Equal("completed", completedPayload.RootElement.GetProperty("snapshot").GetProperty("status").GetString());
        Assert.Equal(
            "Greeting: Hello, Codex!\nLucky: 12\nDate: 2026-03-21\nTheme: #2563eb\nAmount: 123.45",
            completedPayload.RootElement.GetProperty("snapshot").GetProperty("results").GetProperty("console")[0].GetString());

        using var closedResponse = await client.DeleteAsync($"/api/runtime/debug/sessions/{debugSessionId}");
        Assert.Equal(System.Net.HttpStatusCode.OK, closedResponse.StatusCode);
        using var closedPayload = JsonDocument.Parse(await closedResponse.Content.ReadAsStringAsync());
        Assert.True(closedPayload.RootElement.GetProperty("closed").GetBoolean());
    }

    /// <summary>
    /// 构造 JSON 请求体，避免每个断言都重复序列化逻辑。
    /// </summary>
    private static StringContent CreateJsonContent(object payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    /// <summary>
    /// 以真实进程启动 Demo 宿主，覆盖最接近用户实际使用的接口交互路径。
    /// </summary>
    private sealed class DemoHostProcess : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StringBuilder _stdout = new();
        private readonly StringBuilder _stderr = new();

        private DemoHostProcess(Process process, Uri baseAddress)
        {
            _process = process;
            BaseAddress = baseAddress;
        }

        public Uri BaseAddress { get; }

        public static async Task<DemoHostProcess> StartAsync()
        {
            var port = ReserveLoopbackPort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}");
            var projectPath = ResolveProjectPath();
            var startInfo = new ProcessStartInfo("dotnet", $"run --project \"{projectPath}\" --no-launch-profile")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.Environment["DEMO_CLIENT_HOST"] = "127.0.0.1";
            startInfo.Environment["DEMO_CLIENT_PORT"] = port.ToString();
            startInfo.Environment["DEMO_CLIENT_BASE_URL"] = baseAddress.ToString().TrimEnd('/');
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true,
            };

            var handle = new DemoHostProcess(process, baseAddress);
            process.OutputDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is { Length: > 0 })
                {
                    handle._stdout.AppendLine(eventArgs.Data);
                }
            };
            process.ErrorDataReceived += (_, eventArgs) =>
            {
                if (eventArgs.Data is { Length: > 0 })
                {
                    handle._stderr.AppendLine(eventArgs.Data);
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException("Failed to start the .NET demo host process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await handle.WaitUntilHealthyAsync();
            return handle;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // 进程已经退出时无需再处理。
            }

            try
            {
                await _process.WaitForExitAsync();
            }
            finally
            {
                _process.Dispose();
            }
        }

        /// <summary>
        /// 轮询健康接口，确保测试只在宿主真正启动后才开始发起调试请求。
        /// </summary>
        private async Task WaitUntilHealthyAsync()
        {
            using var client = new HttpClient
            {
                BaseAddress = BaseAddress,
            };

            var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(45);
            while (DateTimeOffset.UtcNow < timeoutAt)
            {
                if (_process.HasExited)
                {
                    throw CreateStartupFailure();
                }

                try
                {
                    using var response = await client.GetAsync("/api/health");
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }
                }
                catch (HttpRequestException)
                {
                    // 宿主尚未完成监听时继续重试。
                }
                catch (SocketException)
                {
                    // 端口尚未就绪时继续重试。
                }

                await Task.Delay(250);
            }

            throw CreateStartupFailure("Timed out waiting for the .NET demo host to become healthy.");
        }

        /// <summary>
        /// 为调试进程失败保留 stdout/stderr，便于快速定位原因。
        /// </summary>
        private InvalidOperationException CreateStartupFailure(string? message = null)
        {
            var details = new StringBuilder();
            details.AppendLine(message ?? "The .NET demo host exited before the health check succeeded.");
            details.AppendLine("stdout:");
            details.AppendLine(_stdout.ToString());
            details.AppendLine("stderr:");
            details.AppendLine(_stderr.ToString());
            return new InvalidOperationException(details.ToString());
        }

        /// <summary>
        /// 向操作系统申请一个空闲环回端口，避免测试并发时端口冲突。
        /// </summary>
        private static int ReserveLoopbackPort()
        {
            var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// 从测试输出目录回溯到仓库根，再定位 Demo Web 项目。
        /// </summary>
        private static string ResolveProjectPath()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "Examples", "NodeGraph.DemoClient.DotNet", "NodeGraph.DemoClient.DotNet.csproj");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate Examples/NodeGraph.DemoClient.DotNet/NodeGraph.DemoClient.DotNet.csproj from the test output directory.");
        }
    }
}
