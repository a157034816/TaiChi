# TaiChi.UpgradeKit.Client

## 概述

TaiChi.UpgradeKit.Client 是一个灵活的客户端升级组件，支持通过继承基类来定制API调用方式，使其能够适配各种不同的C#项目。

## 特性

- 支持增量式更新和完整更新
- 支持断点续传
- 支持从备份恢复
- 支持自我更新
- **支持通过继承定制API调用方式**
- 文件校验确保更新完整性

## 基础类

### UpgradeClient

`UpgradeClient` 类是基础升级客户端，提供了基本的升级功能实现和可被重写的API调用方法。

```csharp
public class UpgradeClient
{
    // 构造函数
    public UpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerUrl);
    
    // 可重写的API调用方法
    protected virtual Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request);
    protected virtual Task DownloadPackageFileAsync(UpdateRequest request, UpdatePackageInfo packageInfo, string downloadFilePath, long existingFileSize, Action<long, long> progressCallback);
    
    // 公共方法
    public Task<UpdateResponse> CheckUpdateAsync();
    public Task<string> DownloadPackageAsync(UpdatePackageInfo packageInfo, Action<long, long> progressCallback = null);
    public Task<bool> ApplyUpdateAsync(UpdatePackageInfo packageInfo, string packagePath);
    public Task<bool> ExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo, string packagePath);
    public Task<bool> DownloadAndExecuteSelfUpdateAsync(UpdatePackageInfo packageInfo, Action<long, long> progressCallback = null);
}
```

## 派生类示例

### WebApiUpgradeClient

`WebApiUpgradeClient` 类通过HTTP API与远程升级服务器通信。

```csharp
public class WebApiUpgradeClient : UpgradeClient
{
    public WebApiUpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerUrl)
        : base(appId, appDirectory, currentVersion, upgradeServerUrl);
    
    // 重写API调用方法
    protected override async Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request);
    protected override async Task DownloadPackageFileAsync(UpdateRequest request, UpdatePackageInfo packageInfo, string downloadFilePath, long existingFileSize, Action<long, long> progressCallback);
}
```

### FileSystemUpgradeClient

`FileSystemUpgradeClient` 类从本地文件系统获取更新。

```csharp
public class FileSystemUpgradeClient : UpgradeClient
{
    public FileSystemUpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerPath)
        : base(appId, appDirectory, currentVersion, upgradeServerPath);
    
    // 重写API调用方法
    protected override async Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request);
    protected override async Task DownloadPackageFileAsync(UpdateRequest request, UpdatePackageInfo packageInfo, string downloadFilePath, long existingFileSize, Action<long, long> progressCallback);
}
```

## 使用示例

### 使用WebAPI升级客户端

```csharp
// 创建基于WebAPI的升级客户端
var upgradeClient = new WebApiUpgradeClient(
    "YourAppId",
    "C:\\YourApp",
    new Version(1, 0, 0, 0),
    "https://your-upgrade-server.com"
);

// 设置可执行文件路径（用于自我更新）
upgradeClient.ExecutablePath = "C:\\YourApp\\YourApp.exe";

// 检查更新
var updateResponse = await upgradeClient.CheckUpdateAsync();

if (updateResponse.HasUpdate)
{
    // 下载并应用更新
    bool success = await upgradeClient.DownloadAndExecuteSelfUpdateAsync(
        updateResponse.SuggestedPackage,
        (downloaded, total) => Console.WriteLine($"下载进度: {downloaded}/{total} 字节")
    );
    
    if (success)
    {
        Console.WriteLine("更新已启动，应用程序将重启");
    }
}
```

### 使用文件系统升级客户端

```csharp
// 创建基于文件系统的升级客户端
var upgradeClient = new FileSystemUpgradeClient(
    "YourAppId",
    "C:\\YourApp",
    new Version(1, 0, 0, 0),
    "D:\\UpdateRepository"
);

// 设置可执行文件路径（用于自我更新）
upgradeClient.ExecutablePath = "C:\\YourApp\\YourApp.exe";

// 检查更新
var updateResponse = await upgradeClient.CheckUpdateAsync();

if (updateResponse.HasUpdate)
{
    // 下载并应用更新
    bool success = await upgradeClient.DownloadAndExecuteSelfUpdateAsync(
        updateResponse.SuggestedPackage,
        (downloaded, total) => Console.WriteLine($"下载进度: {downloaded}/{total} 字节")
    );
    
    if (success)
    {
        Console.WriteLine("更新已启动，应用程序将重启");
    }
}
```

### 自定义升级客户端

要创建自定义的升级客户端，只需继承 `UpgradeClient` 类并重写相关API调用方法：

```csharp
public class CustomUpgradeClient : UpgradeClient
{
    public CustomUpgradeClient(string appId, string appDirectory, Version currentVersion, string upgradeServerUrl)
        : base(appId, appDirectory, currentVersion, upgradeServerUrl)
    {
    }
    
    // 重写检查更新方法
    protected override async Task<UpdateResponse> SendCheckUpdateRequestAsync(UpdateRequest request)
    {
        // 实现自定义的更新检查逻辑
    }
    
    // 重写下载方法
    protected override async Task DownloadPackageFileAsync(
        UpdateRequest request, 
        UpdatePackageInfo packageInfo, 
        string downloadFilePath, 
        long existingFileSize,
        Action<long, long> progressCallback)
    {
        // 实现自定义的下载逻辑
    }
}
```

## 适用场景

- 需要定制化升级流程的应用程序
- 需要支持多种不同更新源的应用程序（WebAPI、文件系统等）
- 具有特殊网络环境或安全要求的应用程序

## 注意事项

- 重写API调用方法时，请确保处理好异常情况
- 建议在应用程序启动时检查更新
- 执行自我更新前，请确保设置了正确的可执行文件路径 