using System.Runtime.InteropServices;
using MCServerLauncher.Common.Helpers;
using MCServerLauncher.Common.ProtoType.Status;
using Serilog;

namespace MCServerLauncher.Daemon.Utils.Status;

public static class MemoryInfoHelper
{
    public static readonly ulong TotalPhysicalMemory;

    static MemoryInfoHelper()
    {
        TotalPhysicalMemory = GetTotalPhysicalMemory();
    }

    private static ulong GetTotalPhysicalMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return TryGetWindowsMemory(out var totalKb, out _) ? totalKb : 0UL;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var task = SystemInfoHelper.RunCommandAsync("sh", "-c \"awk '/MemTotal/ {print $2}' /proc/meminfo\"");
            task.Wait();
            if (!ulong.TryParse(task.Result.Trim(), out var totalKb))
                throw new InvalidOperationException("Failed to parse total memory");
            return totalKb;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var task = SystemInfoHelper.RunCommandAsync("sysctl", "-n hw.memsize");
            task.Wait();
            if (!ulong.TryParse(task.Result.Trim(), out var total))
                throw new InvalidOperationException("Failed to parse total memory");
            return total / 1024;
        }

        throw new PlatformNotSupportedException("Unsupported OS");
    }

    public static async Task<MemInfo> GetMemInfo()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            TryGetWindowsMemory(out var totalKb, out var availableKb);
            return new MemInfo(totalKb > 0 ? totalKb : TotalPhysicalMemory, availableKb);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var availableMemRaw = await SystemInfoHelper.RunCommandAsync("sh",
                    "-c \"awk '/MemAvailable/ {print $2}' /proc/meminfo\"")
                .ConfigureAwait(false);
            if (!ulong.TryParse(availableMemRaw.Trim(), out var availableKb))
                throw new InvalidOperationException("Failed to parse available memory");
            return new MemInfo(TotalPhysicalMemory, availableKb);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // 获取页面大小（字节）并转换为 KB
            var pageSize = await SystemInfoHelper.RunCommandAsync("getconf", "PAGESIZE").MapTask(ulong.Parse);
            var pageSizeKb = pageSize / 1024; // 例如 4096 / 1024 = 4

            // 获取空闲页面和非活跃页面数
            var pagesFree = await SystemInfoHelper
                .RunCommandAsync("sh", "-c \"vm_stat | grep 'Pages free' | awk '{print $3}'\"")
                .MapTask(s => ulong.Parse(s.Trim('.')));
            var pagesInactive = await SystemInfoHelper
                .RunCommandAsync("sh", "-c \"vm_stat | grep 'Pages inactive' | awk '{print $3}'\"")
                .MapTask(s => ulong.Parse(s.Trim('.')));

            // 计算可用内存（KB）
            var availablePages = pagesFree + pagesInactive;
            var freeKb = availablePages * pageSizeKb;

            return new MemInfo(TotalPhysicalMemory, freeKb);
        }

        throw new PlatformNotSupportedException("Unsupported OS");
    }

    private static bool TryGetWindowsMemory(out ulong totalKb, out ulong availableKb)
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (!GlobalMemoryStatusEx(ref status))
        {
            Log.Warning("GlobalMemoryStatusEx failed with error {ErrorCode}", Marshal.GetLastWin32Error());
            totalKb = 0;
            availableKb = 0;
            return false;
        }

        totalKb = status.TotalPhysical / 1024;
        availableKb = status.AvailablePhysical / 1024;
        return true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
