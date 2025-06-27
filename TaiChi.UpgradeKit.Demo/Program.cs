using System;
using System.IO;
using System.Threading.Tasks;
using TaiChi.Upgrade.Shared;
using TaiChi.UpgradeKit.Client;
using TaiChi.UpgradeKit.Server;

namespace TaiChi.UpgradeKit.Demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=================== 升级套件演示程序 ===================");
            Console.WriteLine("本程序演示升级服务器和客户端之间的交互");
            Console.WriteLine();

            // 设置路径
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string serverRootPath = Path.Combine(baseDir, "UpgradeServer");
            string clientAppDir = Path.Combine(baseDir, "ClientApp");
            string demoAppDir = Path.Combine(baseDir, "DemoApp");
            
            // 确保目录存在
            Directory.CreateDirectory(serverRootPath);
            Directory.CreateDirectory(clientAppDir);
            Directory.CreateDirectory(demoAppDir);

            // 创建示例应用文件
            Demo_CreateAppFiles(demoAppDir, "1.0.0.0");

            try
            {
                // 启动升级服务器
                Console.WriteLine("正在启动升级服务器...");
                var server = new UpgradeServer(serverRootPath);
                server.Start();
                Console.WriteLine();

                // 注册应用信息
                Console.WriteLine("正在注册演示应用信息...");
                var appInfo = new AppInfo
                {
                    AppId = "demo-app",
                    AppName = "演示应用",
                    Description = "这是一个用于演示升级功能的应用"
                };
                server.RegisterApp(appInfo);
                Console.WriteLine();

                // 发布V1.0版本
                Console.WriteLine("正在发布V1.0版本...");
                await Server_PublishVersion(server, appInfo, demoAppDir, new Version(1, 0, 0, 0));
                Console.WriteLine();

                // 准备V1.1版本
                Console.WriteLine("正在准备V1.1版本...");
                Demo_CreateAppFiles(demoAppDir, "1.1.0.0");
                
                // 发布V1.1版本
                Console.WriteLine("正在发布V1.1版本...");
                await Server_PublishVersion(server, appInfo, demoAppDir, new Version(1, 1, 0, 0));
                Console.WriteLine();

                // 创建并发布V1.0到V1.1的增量更新包
                Console.WriteLine("正在创建并发布V1.0到V1.1的增量更新包...");
                await Server_PublishIncrementalVersion(server, appInfo, new Version(1, 0, 0, 0), new Version(1, 1, 0, 0));
                Console.WriteLine();

                // 初始化客户端(V1.0)
                Console.WriteLine("正在初始化客户端(V1.0)...");
                await Client_Initialize(server, clientAppDir, appInfo.AppId, new Version(1, 0, 0, 0));
                
                // 检查V1.0升级到V1.1
                Console.WriteLine("正在检查V1.0升级到V1.1...");
                await Client_CheckAndUpgrade(server, clientAppDir, appInfo.AppId, new Version(1, 0, 0, 0));
                Console.WriteLine();

                // 准备V2.0版本
                Console.WriteLine("正在准备V2.0版本...");
                Demo_CreateAppFiles(demoAppDir, "2.0.0.0");

                // 发布V2.0版本
                Console.WriteLine("正在发布V2.0版本...");
                await Server_PublishVersion(server, appInfo, demoAppDir, new Version(2, 0, 0, 0));
                Console.WriteLine();

                // 创建并发布V1.1到V2.0的增量更新包
                Console.WriteLine("正在创建并发布V1.1到V2.0的增量更新包...");
                await Server_PublishIncrementalVersion(server, appInfo, new Version(1, 1, 0, 0), new Version(2, 0, 0, 0));
                Console.WriteLine();

                // 检查V1.1升级到V2.0
                Console.WriteLine("正在检查V1.1升级到V2.0...");
                await Client_CheckAndUpgrade(server, clientAppDir, appInfo.AppId, new Version(1, 1, 0, 0));
                Console.WriteLine();

                // 初始化另一个客户端(V1.0)，演示直接升级到V2.0
                Console.WriteLine("正在初始化另一个客户端(V1.0)，演示直接升级到V2.0...");
                string clientApp2Dir = Path.Combine(baseDir, "ClientApp2");
                Directory.CreateDirectory(clientApp2Dir);
                await Client_Initialize(server, clientApp2Dir, appInfo.AppId, new Version(1, 0, 0, 0));
                
                // 检查V1.0直接升级到V2.0
                Console.WriteLine("正在检查V1.0直接升级到V2.0...");
                await Client_CheckAndUpgrade(server, clientApp2Dir, appInfo.AppId, new Version(1, 0, 0, 0));
                Console.WriteLine();
                
                // 演示自我更新功能
                Console.WriteLine("正在演示自我更新功能...");
                string selfUpdateDir = Path.Combine(baseDir, "SelfUpdateDemo");
                Directory.CreateDirectory(selfUpdateDir);
                await Client_DemoSelfUpdate(server, selfUpdateDir, appInfo.AppId);
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生错误: {ex.Message}");
            }

            Console.WriteLine("演示程序结束，按任意键退出...");
            Console.ReadKey();
        }

        // 创建示例应用文件
        static void Demo_CreateAppFiles(string appDir, string version)
        {
            // 创建版本文件
            File.WriteAllText(Path.Combine(appDir, "version.txt"), version);
            
            // 创建示例文件
            File.WriteAllText(Path.Combine(appDir, "app.exe"), $"假装这是一个可执行文件 V{version}");
            File.WriteAllText(Path.Combine(appDir, "app.dll"), $"假装这是一个DLL文件 V{version}");
            
            // 创建配置目录和文件
            string configDir = Path.Combine(appDir, "config");
            Directory.CreateDirectory(configDir);
            File.WriteAllText(Path.Combine(configDir, "settings.json"), $"{{ \"version\": \"{version}\", \"settings\": {{ \"key\": \"value\" }} }}");
            
            // 创建数据目录
            string dataDir = Path.Combine(appDir, "data");
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(Path.Combine(dataDir, "data.bin"), $"假装这是一个二进制数据文件 V{version}");
            File.WriteAllText(Path.Combine(dataDir, "data_everything.bin"), $"假装这是一个不改变的二进制数据文件");
            
            // V1.1 特有文件
            if (version == "1.1.0.0")
            {
                File.WriteAllText(Path.Combine(appDir, "new_in_v1.1.txt"), "这个文件是V1.1版本新增的");
            }
            
            // V2.0 特有文件
            if (version == "2.0.0.0")
            {
                File.WriteAllText(Path.Combine(appDir, "new_in_v2.0.txt"), "这个文件是V2.0版本新增的");
                
                // V2.0删除V1.1特有文件
                string v11File = Path.Combine(appDir, "new_in_v1.1.txt");
                if (File.Exists(v11File))
                {
                    File.Delete(v11File);
                }
            }
        }

        // 发布新版本
        static async Task Server_PublishVersion(IUpgradeServer server, AppInfo appInfo, string appDir, Version version)
        {
            // 创建临时目录用于制作完整包
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            
            try
            {
                // 复制应用文件到临时目录
                foreach (var dirPath in Directory.GetDirectories(appDir, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(appDir, tempDir));
                }

                foreach (var filePath in Directory.GetFiles(appDir, "*", SearchOption.AllDirectories))
                {
                    File.Copy(filePath, filePath.Replace(appDir, tempDir), true);
                }
                
                // 创建完整包
                string packageName = $"app_{version}_full.zip";
                string packagePath = Path.Combine(Path.GetTempPath(), packageName);
                
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
                
                // 创建完整包
                await Task.Run(() => System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, packagePath));
                
                // 发布版本
                var versionInfo = new VersionInfo
                {
                    AppId = appInfo.AppId,
                    Version = version,
                    ReleaseDate = DateTime.Now,
                    Description = $"版本 {version} 的发布说明"
                };
                
                server.PublishVersion(versionInfo, packagePath);
                
                Console.WriteLine($"已发布版本 {version}");
            }
            finally
            {
                // 清理临时目录
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
        
        // 发布增量版本
        static async Task Server_PublishIncrementalVersion(IUpgradeServer server, AppInfo appInfo, Version sourceVersion, Version targetVersion)
        {
            string rootPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // 从服务器获取版本信息
            var versions = server.GetAppVersions(appInfo.AppId);
            
            // 查找源版本和目标版本
            var sourceVersionInfo = versions.Find(v => v.Version.Equals(sourceVersion));
            var targetVersionInfo = versions.Find(v => v.Version.Equals(targetVersion));
            
            if (sourceVersionInfo == null || targetVersionInfo == null)
            {
                throw new InvalidOperationException("找不到源版本或目标版本");
            }
            
            // 获取源版本和目标版本的目录
            string appDir = Path.Combine(rootPath, "DemoApp");
            string sourceVersionDir = Path.Combine(rootPath, "SourceVersion");
            string targetVersionDir = Path.Combine(rootPath, "TargetVersion");
            
            // 清理和创建目录
            if (Directory.Exists(sourceVersionDir))
                Directory.Delete(sourceVersionDir, true);
            if (Directory.Exists(targetVersionDir))
                Directory.Delete(targetVersionDir, true);
            
            Directory.CreateDirectory(sourceVersionDir);
            Directory.CreateDirectory(targetVersionDir);
            
            try
            {
                // 创建源版本目录
                Demo_CreateAppFiles(sourceVersionDir, sourceVersion.ToString());
                
                // 创建目标版本目录
                Demo_CreateAppFiles(targetVersionDir, targetVersion.ToString());
                
                // 创建增量包
                string packageName = $"app_{sourceVersion}_to_{targetVersion}_incremental.zip";
                string packagePath = Path.Combine(Path.GetTempPath(), packageName);
                
                if (File.Exists(packagePath))
                    File.Delete(packagePath);
                
                // 创建增量包
                await Task.Run(() => UpgradeServerHelper.CreateIncrementalPackage(sourceVersionDir, targetVersionDir, packagePath));
                
                // 发布增量包
                server.PublishIncrementalPackage(appInfo.AppId, sourceVersionInfo.VersionId, targetVersionInfo.VersionId, packagePath);
                
                Console.WriteLine($"已发布从版本 {sourceVersion} 到 {targetVersion} 的增量更新包");
            }
            finally
            {
                // 清理目录
                if (Directory.Exists(sourceVersionDir))
                    Directory.Delete(sourceVersionDir, true);
                if (Directory.Exists(targetVersionDir))
                    Directory.Delete(targetVersionDir, true);
            }
        }
        
        // 初始化客户端
        static async Task Client_Initialize(IUpgradeServer server, string clientAppDir, string appId, Version version)
        {
            // 清理目录并创建初始版本
            if (Directory.Exists(clientAppDir))
            {
                // 保留Downloads和Backups目录
                var downloadsDir = Path.Combine(clientAppDir, "Downloads");
                var backupsDir = Path.Combine(clientAppDir, "Backups");
                
                foreach (var dir in Directory.GetDirectories(clientAppDir))
                {
                    if (dir != downloadsDir && dir != backupsDir)
                    {
                        Directory.Delete(dir, true);
                    }
                }
                
                foreach (var file in Directory.GetFiles(clientAppDir))
                {
                    File.Delete(file);
                }
            }
            else
            {
                Directory.CreateDirectory(clientAppDir);
            }
            
            // 创建初始版本文件
            Demo_CreateAppFiles(clientAppDir, version.ToString());
            
            Console.WriteLine($"已初始化客户端，版本: {version}");
        }
        
        // 检查并升级
        static async Task Client_CheckAndUpgrade(IUpgradeServer server, string clientAppDir, string appId, Version currentVersion)
        {
            // 创建客户端实例
            var client = new UpgradeClient(appId, clientAppDir, currentVersion, "http://localhost:5000");
            client.Start();
            
            // 检查更新
            Console.WriteLine($"检查更新，当前版本: {currentVersion}");
            var request = new UpdateRequest
            {
                AppId = appId,
                CurrentVersion = currentVersion
            };
            
            var response = server.CheckUpdate(request);
            
            if (!response.HasUpdate)
            {
                Console.WriteLine("没有可用的更新");
                return;
            }
            
            Console.WriteLine($"发现可用更新，最新版本: {response.LatestVersion?.Version}");
            Console.WriteLine($"推荐的更新包类型: {response.SuggestedPackage?.PackageType}");
            
            if (response.SuggestedPackage == null)
            {
                Console.WriteLine("没有可用的更新包");
                return;
            }
            
            // 模拟下载过程(实际使用中应该通过HTTP下载)
            Console.WriteLine("正在下载更新包...");
            string packageFileName = $"downloaded_{Path.GetFileName(response.SuggestedPackage.PackagePath)}";
            string downloadFilePath = Path.Combine(clientAppDir, "Downloads", packageFileName);
            
            // 确保下载目录存在
            Directory.CreateDirectory(Path.GetDirectoryName(downloadFilePath) ?? throw new InvalidOperationException("无效的下载路径"));
            
            // 模拟下载：直接复制文件
            File.Copy(response.SuggestedPackage.PackagePath, downloadFilePath, true);
            
            Console.WriteLine($"已下载更新包到: {downloadFilePath}");
            
            // 应用更新
            Console.WriteLine("正在应用更新...");
            bool success = await client.ApplyUpdateAsync(response.SuggestedPackage, downloadFilePath);
            
            if (success)
            {
                Console.WriteLine("更新成功应用");
            }
            else
            {
                Console.WriteLine("更新应用失败");
            }
        }

        /// <summary>
        /// 演示自我更新功能
        /// </summary>
        static async Task Client_DemoSelfUpdate(IUpgradeServer server, string appDir, string appId)
        {
            try
            {
                // 初始化V1.0版本客户端
                await Client_Initialize(server, appDir, appId, new Version(1, 0, 0, 0));
                
                // 创建客户端可执行文件
                string exePath = Path.Combine(appDir, "app.exe");
                if (!File.Exists(exePath))
                {
                    File.WriteAllText(exePath, "模拟应用可执行文件内容");
                }
                
                // 创建升级客户端实例
                var upgradeClient = new UpgradeClient(appId, appDir, new Version(1, 0, 0, 0), "http://localhost:5000");
                
                // 设置可执行文件路径
                upgradeClient.ExecutablePath = exePath;
                
                // 检查更新
                Console.WriteLine("检查更新");
                var updateResponse = await upgradeClient.CheckUpdateAsync();
                
                // 模拟从服务器获取更新信息
                var versions = server.GetAppVersions(appId);
                var latestVersion = versions.Find(v => v.IsLatest);
                
                if (latestVersion != null)
                {
                    Console.WriteLine($"检测到新版本: {latestVersion.Version}");
                    
                    // 获取适用于当前版本的更新包
                    var updatePackages = server.CheckUpdate(new UpdateRequest 
                    { 
                        AppId = appId, 
                        CurrentVersion = new Version(1, 0, 0, 0) 
                    });
                    
                    if (updatePackages.HasUpdate && updatePackages.SuggestedPackage != null)
                    {
                        UpdatePackageInfo packageInfo = updatePackages.SuggestedPackage;
                        
                        // 根据包类型确定更新方式
                        string updateType = packageInfo.PackageType == UpdatePackageType.Full ? "完整" : "增量";
                        Console.WriteLine($"推荐的更新包类型: {updateType}");
                        
                        // 获取更新包的路径
                        string packagePath = packageInfo.PackagePath;
                        Console.WriteLine($"更新包路径: {packagePath}");
                        
                        // 确保本地存在更新包
                        if (File.Exists(packagePath))
                        {
                            Console.WriteLine("准备执行自我更新...");
                            Console.WriteLine("(在实际应用中，这将下载更新包并启动单独的进程进行更新操作)");
                            
                            // 准备执行自我更新
                            bool canExecuteSelfUpdate = await upgradeClient.ExecuteSelfUpdateAsync(packageInfo, packagePath);
                            
                            if (canExecuteSelfUpdate)
                            {
                                Console.WriteLine("已启动自我更新流程。应用将在退出后更新。");
                                Console.WriteLine("(模拟应用退出...)");
                            }
                            else
                            {
                                Console.WriteLine("启动自我更新流程失败。");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"无法找到更新包: {packagePath}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("未找到适用的更新包");
                    }
                }
                else
                {
                    Console.WriteLine("没有可用的更新");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自我更新演示出错: {ex.Message}");
            }
        }
    }
}
