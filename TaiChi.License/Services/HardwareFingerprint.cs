using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace TaiChi.License.Services
{
    /// <summary>
    /// 硬件指纹服务：跨平台收集可用的稳定硬件标识，并计算 SHA-256 指纹。
    /// 设计目标：
    /// - Windows 优先：MachineGuid、WMI(ProcessorId/BaseBoard/Bios)、物理网卡 MAC。
    /// - Linux/macOS：/sys(/proc)/sysctl 等来源 + 物理网卡 MAC。
    /// - 尽可能避免硬依赖，WMI 通过反射调用 System.Management，如不可用则降级。
    /// </summary>
    public static class HardwareFingerprint
    {
        public static string GetFingerprint()
        {
            var components = CollectComponents();
            var joined = string.Join("\n", components
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(joined));
            return ToHex(hash);
        }

        public static bool Verify(string expected)
        {
            if (string.IsNullOrWhiteSpace(expected)) return false;
            var actual = GetFingerprint();
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 收集用于计算指纹的组件键值对（仅包含可用且非空的项）。
        /// </summary>
        public static Dictionary<string, string> CollectComponents()
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["arch"] = RuntimeInformation.OSArchitecture.ToString(),
            };

            // Windows MachineGuid（注册表）在 netstandard2.1 需额外包依赖，此处跳过，转而依赖 WMI 与 MAC
            try { var cpu = GetCpuId(); if (!string.IsNullOrWhiteSpace(cpu)) dict["cpu_id"] = cpu!; } catch { }
            try { var mb = GetBaseBoardSerial(); if (!string.IsNullOrWhiteSpace(mb)) dict["board_sn"] = mb!; } catch { }
            try { var bios = GetBiosSerial(); if (!string.IsNullOrWhiteSpace(bios)) dict["bios_sn"] = bios!; } catch { }

            try
            {
                var macs = GetPhysicalMacAddresses();
                if (macs.Count > 0) dict["mac_list"] = string.Join(",", macs);
            }
            catch { }

            // Linux/macOS 补充：DMI 与产品/主板序列
            if (!IsWindows())
            {
                TryAddFile(dict, "dmi_board_serial", "/sys/class/dmi/id/board_serial");
                TryAddFile(dict, "dmi_product_uuid", "/sys/class/dmi/id/product_uuid");
                TryAddFile(dict, "dmi_product_serial", "/sys/class/dmi/id/product_serial");
            }

            return dict
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.Ordinal);
        }

        private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool IsLinux() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        private static bool IsMacOS() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        private static void TryAddFile(Dictionary<string, string> dict, string key, string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var val = File.ReadAllText(path).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) dict[key] = val;
                }
            }
            catch { }
        }

        private static string? GetCpuId()
        {
            if (IsWindows())
            {
                // 通过反射调用 System.Management 的 WMI 查询，避免编译期硬依赖
                var id = QueryWmiSingle("Win32_Processor", "ProcessorId");
                if (!string.IsNullOrWhiteSpace(id)) return id;
            }
            if (IsLinux())
            {
                try
                {
                    if (File.Exists("/proc/cpuinfo"))
                    {
                        foreach (var line in File.ReadAllLines("/proc/cpuinfo"))
                        {
                            var kv = line.Split(':');
                            if (kv.Length == 2)
                            {
                                var k = kv[0].Trim().ToLowerInvariant();
                                var v = kv[1].Trim();
                                if (k == "serial" && !string.IsNullOrWhiteSpace(v)) return v;
                            }
                        }
                    }
                }
                catch { }
            }
            if (IsMacOS())
            {
                // macOS 无直接 CPU 序列，返回 null
            }
            return null;
        }

        private static string? GetBaseBoardSerial()
        {
            if (IsWindows())
            {
                var sn = QueryWmiSingle("Win32_BaseBoard", "SerialNumber");
                if (!string.IsNullOrWhiteSpace(sn)) return sn;
            }
            // Linux: /sys/class/dmi/id/board_serial 已在 CollectComponents 中补充
            return null;
        }

        private static string? GetBiosSerial()
        {
            if (IsWindows())
            {
                var sn = QueryWmiSingle("Win32_BIOS", "SerialNumber");
                if (!string.IsNullOrWhiteSpace(sn)) return sn;
            }
            return null;
        }

        private static string? QueryWmiSingle(string wmiClass, string property)
        {
            try
            {
                var asm = Assembly.Load(new AssemblyName("System.Management"));
                if (asm == null) return null;
                var searcherType = asm.GetType("System.Management.ManagementObjectSearcher");
                var pathType = asm.GetType("System.Management.ManagementPath");
                if (searcherType == null) return null;

                var query = $"SELECT {property} FROM {wmiClass}";
                var searcher = Activator.CreateInstance(searcherType, new object?[] { query });
                var getMethod = searcherType.GetMethod("Get", Type.EmptyTypes);
                var col = getMethod?.Invoke(searcher, null) as System.Collections.IEnumerable;
                if (col == null) return null;

                foreach (var obj in col)
                {
                    var t = obj.GetType();
                    var prop = t.GetProperty(property);
                    var val = prop?.GetValue(obj)?.ToString();
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }
            catch { }
            return null;
        }

        private static List<string> GetPhysicalMacAddresses()
        {
            var list = new List<string>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;
                    if (nic.OperationalStatus != OperationalStatus.Up)
                        continue;
                    var name = nic.Name?.ToLowerInvariant() ?? string.Empty;
                    if (name.StartsWith("veth") || name.StartsWith("docker") || name.StartsWith("virbr") || name.StartsWith("vmnet") || name == "lo")
                        continue;
                    var mac = nic.GetPhysicalAddress()?.ToString();
                    if (!string.IsNullOrWhiteSpace(mac))
                    {
                        // 标准化为 12 位十六进制，按两位分隔
                        var norm = string.Join(":", Enumerable.Range(0, mac!.Length / 2).Select(i => mac.Substring(i * 2, 2)));
                        list.Add(norm);
                    }
                }
            }
            catch { }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string ToHex(byte[] bytes)
        {
            char[] c = new char[bytes.Length * 2];
            int b;
            for (int i = 0; i < bytes.Length; i++)
            {
                b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }
            return new string(c).ToLowerInvariant();
        }
    }
}
