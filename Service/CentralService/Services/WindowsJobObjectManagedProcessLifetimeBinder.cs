using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace CentralService.Services;

internal sealed class WindowsJobObjectManagedProcessLifetimeBinder : IManagedProcessLifetimeBinder, IDisposable
{
    private const uint JobObjectLimitKillOnJobClose = 0x00002000;

    private readonly SafeJobHandle _jobHandle;
    private bool _disposed;

    public WindowsJobObjectManagedProcessLifetimeBinder()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Windows Job Object 仅支持在 Windows 上使用。");
        }

        _jobHandle = CreateConfiguredJobObject();
    }

    public void Attach(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsJobObjectManagedProcessLifetimeBinder));
        }

        if (process.HasExited)
        {
            throw new InvalidOperationException("无法绑定已退出的进程。");
        }

        if (!AssignProcessToJobObject(_jobHandle, process.SafeHandle))
        {
            var errorCode = Marshal.GetLastWin32Error();
            if (process.HasExited)
            {
                throw new InvalidOperationException("待绑定的进程在加入 Job Object 前已退出。");
            }

            throw new Win32Exception(
                errorCode,
                $"无法将进程 PID={process.Id} 绑定到 Windows Job Object。");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _jobHandle.Dispose();
        _disposed = true;
    }

    private static SafeJobHandle CreateConfiguredJobObject()
    {
        var jobHandle = CreateJobObject(IntPtr.Zero, null);
        if (jobHandle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "创建 Windows Job Object 失败。");
        }

        var limitInfo = new JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new JobObjectBasicLimitInformation
            {
                LimitFlags = JobObjectLimitKillOnJobClose,
            },
        };

        if (!SetInformationJobObject(
                jobHandle,
                JobObjectInfoClass.ExtendedLimitInformation,
                ref limitInfo,
                (uint)Marshal.SizeOf<JobObjectExtendedLimitInformation>()))
        {
            var errorCode = Marshal.GetLastWin32Error();
            jobHandle.Dispose();
            throw new Win32Exception(errorCode, "配置 Windows Job Object 失败。");
        }

        return jobHandle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeJobHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        SafeJobHandle job,
        JobObjectInfoClass jobObjectInfoClass,
        ref JobObjectExtendedLimitInformation jobObjectInfo,
        uint jobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(SafeJobHandle job, SafeProcessHandle process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeJobHandle()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return CloseHandle(handle);
        }
    }

    private enum JobObjectInfoClass
    {
        ExtendedLimitInformation = 9,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimitInformation
    {
        public long PerProcessUserTimeLimit;

        public long PerJobUserTimeLimit;

        public uint LimitFlags;

        public nuint MinimumWorkingSetSize;

        public nuint MaximumWorkingSetSize;

        public uint ActiveProcessLimit;

        public nuint Affinity;

        public uint PriorityClass;

        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;

        public ulong WriteOperationCount;

        public ulong OtherOperationCount;

        public ulong ReadTransferCount;

        public ulong WriteTransferCount;

        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimitInformation
    {
        public JobObjectBasicLimitInformation BasicLimitInformation;

        public IoCounters IoInfo;

        public nuint ProcessMemoryLimit;

        public nuint JobMemoryLimit;

        public nuint PeakProcessMemoryUsed;

        public nuint PeakJobMemoryUsed;
    }
}
